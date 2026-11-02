using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using CyberManagement.API;
using CyberManagement.API.Controller;
using CyberManagement.Application;
using CyberManagement.Application.DTOs.Identity;
using CyberManagement.Application.Interfaces.Repositories;
using CyberManagement.Application.Interfaces.Services;
using CyberManagement.Application.Security;
using CyberManagement.Domain.Entities;
using CyberManagement.Infrastructure.Repositories;
using CyberManagement.Infrastructure.Security;
using CyberManagement.Infrastructure.Persistance;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;

var databaseName = $"auth-checks-{Guid.NewGuid():N}";
var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    EnvironmentName = "Development",
    ApplicationName = typeof(CyberManagement.API.Program).Assembly.FullName
});
builder.WebHost.UseTestServer();
builder.Logging.ClearProviders();
builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
{
    ["Authentication:Jwt:SigningKey"] = "checks-only-signing-key-that-is-at-least-32-bytes",
    ["SeedAccounts:Admin:Password"] = "Admin-check-password-2026!",
    ["SeedAccounts:Manager:Password"] = "Manager-check-password-2026!",
    ["SeedAccounts:Cashier:Password"] = "Cashier-check-password-2026!",
    ["SeedAccounts:Waiter:Password"] = "Waiter-check-password-2026!",
    ["SeedAccounts:Technician:Password"] = "Technician-check-password-2026!"
});
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseInMemoryDatabase(databaseName));
builder.Services.AddApplicationServices();
builder.Services.AddApiServices(builder.Configuration);
builder.Services.AddScoped<IIdentityRepository, IdentityRepository>();
builder.Services.AddScoped<ICustomerIdentityRepository, CustomerIdentityRepository>();
builder.Services.AddScoped<IEmployeePasswordHasher, EmployeePasswordHasher>();
builder.Services.AddScoped<ICustomerPasswordHasher, CustomerPasswordHasher>();
builder.Services
    .AddControllers()
    .AddApplicationPart(typeof(AuthController).Assembly);

await using var app = builder.Build();
await using (var scope = app.Services.CreateAsyncScope())
{
    await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>()
        .Database.EnsureCreatedAsync();
    var accountPasswords = RolePermissionCatalog.Roles.ToDictionary(
        role => role.Name,
        role => app.Configuration[$"SeedAccounts:{role.Name}:Password"],
        StringComparer.Ordinal);
    await scope.ServiceProvider.GetRequiredService<IIdentitySeeder>().SeedAsync(
        new IdentitySeedOptions(true, accountPasswords),
        CancellationToken.None);
    var passwordHasher = scope.ServiceProvider.GetRequiredService<ICustomerPasswordHasher>();
    var activeCustomer = new Customer
    {
        FullName = "Test Customer",
        Phone = "0900000001",
        Email = "customer@example.test",
        WalletBalance = 100000,
        Status = "Active"
    };
    activeCustomer.PasswordHash = passwordHasher.HashPassword(activeCustomer, "Customer-check-password-2026!");
    var lockedCustomer = new Customer
    {
        FullName = "Locked Customer",
        Phone = "0900000002",
        Status = "Locked"
    };
    lockedCustomer.PasswordHash = passwordHasher.HashPassword(
        lockedCustomer, "Customer-check-password-2026!");
    var database = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    database.Customers.AddRange(activeCustomer, lockedCustomer);
    await database.SaveChangesAsync();
}

app.UseExceptionHandler();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
await app.StartAsync();

var client = app.GetTestClient();
var checks = 0;
try
{
    await CheckStatus("Empty employee username is rejected", HttpStatusCode.BadRequest,
        await client.PostAsJsonAsync("/api/auth/login", new { username = "", password = "password" }));
    await CheckStatus("Employee username over limit is rejected", HttpStatusCode.BadRequest,
        await SendLogin(new string('u', 51), "password"));
    await CheckStatus("Wrong password is rejected", HttpStatusCode.Unauthorized,
        await SendLogin("admin", "incorrect-password"));
    await CheckStatus("Unknown account is rejected", HttpStatusCode.Unauthorized,
        await SendLogin("not-a-user", "incorrect-password"));

    for (var attempt = 1; attempt <= 6; attempt++)
    {
        await CheckStatus($"Failed login attempt {attempt}", HttpStatusCode.Unauthorized,
            await SendLogin("tech01", "incorrect-password"));
    }
    await CheckLogin("Correct password still works after repeated failures",
        "tech01", "Technician-check-password-2026!");

    using var anonymousRequest = new HttpRequestMessage(HttpMethod.Get, "/api/admin/roles");
    await CheckStatus("Anonymous access is rejected", HttpStatusCode.Unauthorized,
        await client.SendAsync(anonymousRequest));

    var admin = await CheckLogin("Admin login succeeds", "admin", "Admin-check-password-2026!");
    await CheckStatus("Admin permission is accepted", HttpStatusCode.OK,
        await SendWithToken(HttpMethod.Get, "/api/admin/roles", admin.AccessToken));
    await CheckStatus("Logout requires authentication", HttpStatusCode.Unauthorized,
        await client.PostAsync("/api/auth/logout", content: null));
    await CheckStatus("Logout succeeds", HttpStatusCode.NoContent,
        await SendWithToken(HttpMethod.Post, "/api/auth/logout", admin.AccessToken));

    var waiter = await CheckLogin("Waiter login succeeds", "waiter01", "Waiter-check-password-2026!");
    await CheckStatus("Waiter cannot manage roles", HttpStatusCode.Forbidden,
        await SendWithToken(HttpMethod.Get, "/api/admin/roles", waiter.AccessToken));
    await CheckStatus("Tampered access token is rejected", HttpStatusCode.Unauthorized,
        await SendWithToken(HttpMethod.Get, "/api/auth/me", admin.AccessToken + "tampered"));

    await CheckStatus("Empty customer phone is rejected", HttpStatusCode.BadRequest,
        await SendCustomerLogin("", "Customer-check-password-2026!"));
    await CheckStatus("Customer phone over limit is rejected", HttpStatusCode.BadRequest,
        await SendCustomerLogin(new string('1', 21), "Customer-check-password-2026!"));
    await CheckStatus("Empty customer password is rejected", HttpStatusCode.BadRequest,
        await SendCustomerLogin("0900000001", ""));
    for (var attempt = 1; attempt <= 6; attempt++)
    {
        await CheckStatus($"Customer wrong password attempt {attempt}", HttpStatusCode.Unauthorized,
            await SendCustomerLogin("0900000001", "incorrect-password"));
    }
    await CheckStatus("Customer without password is rejected", HttpStatusCode.Unauthorized,
        await SendCustomerLogin("0900000003", "Customer-check-password-2026!"));
    await CheckStatus("Locked customer cannot log in", HttpStatusCode.Forbidden,
        await SendCustomerLogin("0900000002", "Customer-check-password-2026!"));
    var customer = await CheckCustomerLogin(
        "Customer login by phone succeeds",
        "0900000001",
        "Customer-check-password-2026!");
    await CheckStatus("Customer can access own profile", HttpStatusCode.OK,
        await SendWithToken(HttpMethod.Get, "/api/auth/customer/me", customer.AccessToken));
    await CheckStatus("Customer cannot access staff profile", HttpStatusCode.Forbidden,
        await SendWithToken(HttpMethod.Get, "/api/auth/me", customer.AccessToken));
    await CheckStatus("Customer cannot manage roles", HttpStatusCode.Forbidden,
        await SendWithToken(HttpMethod.Get, "/api/admin/roles", customer.AccessToken));
    await CheckStatus("Customer logout succeeds", HttpStatusCode.NoContent,
        await SendWithToken(HttpMethod.Post, "/api/auth/customer/logout", customer.AccessToken));

    Console.WriteLine($"{checks} HTTP checks passed.");
}
finally
{
    client.Dispose();
    await app.StopAsync();
}

async Task<HttpResponseMessage> SendLogin(string username, string password) =>
    await client.PostAsJsonAsync("/api/auth/login", new { username, password });

async Task<HttpResponseMessage> SendCustomerLogin(string phone, string password) =>
    await client.PostAsJsonAsync("/api/auth/customer/login", new { phone, password });

async Task<LoginResponse> CheckLogin(string name, string username, string password)
{
    using var response = await SendLogin(username, password);
    if (response.StatusCode != HttpStatusCode.OK)
        throw new InvalidOperationException($"{name}: expected 200, received {(int)response.StatusCode}.");

    checks++;
    Console.WriteLine($"PASS {name}");
    return await response.Content.ReadFromJsonAsync<LoginResponse>()
        ?? throw new InvalidOperationException($"{name}: no response body.");
}

async Task<CustomerLoginResponse> CheckCustomerLogin(string name, string phone, string password)
{
    using var response = await SendCustomerLogin(phone, password);
    if (response.StatusCode != HttpStatusCode.OK)
        throw new InvalidOperationException($"{name}: expected 200, received {(int)response.StatusCode}.");

    checks++;
    Console.WriteLine($"PASS {name}");
    return await response.Content.ReadFromJsonAsync<CustomerLoginResponse>()
        ?? throw new InvalidOperationException($"{name}: no response body.");
}

async Task<HttpResponseMessage> SendWithToken(HttpMethod method, string path, string token)
{
    using var request = new HttpRequestMessage(method, path);
    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
    return await client.SendAsync(request);
}

async Task CheckStatus(string name, HttpStatusCode expected, HttpResponseMessage response)
{
    using (response)
    {
        if (response.StatusCode != expected)
        {
            throw new InvalidOperationException(
                $"{name}: expected {(int)expected}, received {(int)response.StatusCode}: " +
                await response.Content.ReadAsStringAsync());
        }

        checks++;
        Console.WriteLine($"PASS {name}");
    }
}
