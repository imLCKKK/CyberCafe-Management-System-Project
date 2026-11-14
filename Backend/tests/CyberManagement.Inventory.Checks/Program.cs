using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using CyberManagement.API;
using CyberManagement.API.Controller;
using CyberManagement.Application;
using CyberManagement.Application.DTOs.Identity;
using CyberManagement.Application.DTOs.Inventory;
using CyberManagement.Application.Interfaces.Repositories;
using CyberManagement.Application.Interfaces.Services;
using CyberManagement.Application.Security;
using CyberManagement.Domain.Entities;
using CyberManagement.Infrastructure;
using CyberManagement.Infrastructure.Persistance;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Hosting;

if (args.Contains("--export-schema") || args.Contains("--check-schema"))
{
    SchemaExport.Run(args.Contains("--check-schema"));
    return;
}

var fixture = new InventoryCheckDatabase(Environment.GetEnvironmentVariable("INVENTORY_CHECKS_SQL_CONNECTION"));
var serve = args.Contains("--serve");
if (serve && !fixture.IsSql)
    throw new InvalidOperationException("--serve requires INVENTORY_CHECKS_SQL_CONNECTION; the browser SQL checks must not use InMemory.");
var checks = 0;
const string password = "Inventory-check-only-2026!";
var signingKey = Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(48));
try
{
    await using (var app = CreateApp(serve))
    {
        await using (var scope = app.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            await fixture.InitializeAsync(db);
            await scope.ServiceProvider.GetRequiredService<IIdentitySeeder>().SeedAsync(
                new IdentitySeedOptions(true, RolePermissionCatalog.Roles.ToDictionary(
                    role => role.Name, _ => (string?)password)), CancellationToken.None);
            var customer = new Customer { FullName = "Inventory Check Customer", Phone = "0900000013", Status = "Active" };
            customer.PasswordHash = scope.ServiceProvider.GetRequiredService<ICustomerPasswordHasher>()
                .HashPassword(customer, password);
            db.Customers.Add(customer);
            // Role ngoài catalog chứng minh API quyết định theo permission, không theo tên role.
            foreach (var (username, permissionName) in new[]
            {
                ("inventory-reader", "stockreceipt.view"),
                ("inventory-adjuster", "inventory.adjust")
            })
            {
                var permission = await db.Permissions.SingleAsync(item => item.PermissionName == permissionName);
                var role = new Role { RoleName = username };
                role.RolePermissions.Add(new RolePermission { Permission = permission });
                var employee = new Employee { Username = username, FullName = username, Role = role };
                employee.PasswordHash = scope.ServiceProvider.GetRequiredService<IEmployeePasswordHasher>()
                    .HashPassword(employee, password);
                db.Employees.Add(employee);
            }
            await db.SaveChangesAsync();
        }
        await app.StartAsync();
        if (serve)
        {
            await SeedTwice(app);
            Console.WriteLine("INVENTORY_HOST_READY");
            // Host kiểm thử chờ Ctrl+C hoặc POST /__checks/stop bằng token Admin.
            await app.WaitForShutdownAsync();
        }
        else
        {
            using var client = app.GetTestClient();
            await ExpectStatus("Anonymous inventory denied", HttpStatusCode.Unauthorized,
                await client.GetAsync("/api/admin/inventory"));
            var admin = await Login(client, "admin");
            Check("Empty inventory returns []", (await ReadInventory(client, admin)).Length == 0);
            await SeedTwice(app);
            var items = await ReadInventory(client, admin);
            CheckItems(items);
            var reread = await ReadInventory(client, admin);
            Check("Repeated request reads persisted values", items.SequenceEqual(reread));
            var manager = await Login(client, "manager01");
            var managerItems = await ReadInventory(client, manager);
            Check("Manager inventory allowed", items.SequenceEqual(managerItems));
            var readerItems = await ReadInventory(client, await Login(client, "inventory-reader"));
            Check("Custom role with view permission can read inventory", items.SequenceEqual(readerItems));
            await ExpectStatus("Adjust permission alone does not grant inventory read", HttpStatusCode.Forbidden,
                await Send(client, await Login(client, "inventory-adjuster")));
            foreach (var username in new[] { "cashier01", "waiter01", "tech01" })
                await ExpectStatus($"{username} inventory denied", HttpStatusCode.Forbidden,
                    await Send(client, await Login(client, username)));
            using var customerLogin = await client.PostAsJsonAsync("/api/auth/customer/login", new { phone = "0900000013", password });
            customerLogin.EnsureSuccessStatusCode();
            var customer = await customerLogin.Content.ReadFromJsonAsync<CustomerLoginResponse>();
            await ExpectStatus("Customer inventory denied", HttpStatusCode.Forbidden,
                await Send(client, customer!.AccessToken));
            await ExpectStatus("Invalid JWT denied", HttpStatusCode.Unauthorized, await Send(client, admin + "invalid"));
            await using var scope = app.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            await scope.ServiceProvider.GetRequiredService<IInventoryQueries>().GetInventoryAsync();
            Check("Read does not track or mutate products", !db.ChangeTracker.Entries<Product>().Any());
            Check("Read does not create inventory movements", !await db.InventoryTransactions.AnyAsync());
            if (fixture.IsSql) await SchemaContractChecks.RunAsync(db, Check);
        }
        await app.StopAsync();
    }

    if (!serve)
    {
        await using var restarted = CreateApp(false);
        await restarted.StartAsync();
        using var client = restarted.GetTestClient();
        CheckItems(await ReadInventory(client, await Login(client, "admin")));
        Check("Data survives a new HTTP host and DbContext", true);
        await restarted.StopAsync();
        await using var failed = CreateApp(false, true);
        await failed.StartAsync();
        using var failedClient = failed.GetTestClient();
        await ExpectStatus("Query failure returns 500 instead of demo data", HttpStatusCode.InternalServerError,
            await Send(failedClient, await Login(failedClient, "admin")));
        await failed.StopAsync();
        if (fixture.IsSql)
        {
            await SchemaContractChecks.RunUpgradeGuardsAsync(Environment.GetEnvironmentVariable("INVENTORY_CHECKS_SQL_CONNECTION")!, Check);
            await using var movementHost = CreateApp(false);
            await InventoryMovementChecks.RunAsync(movementHost.Services, Check);
        }
        Console.WriteLine($"{checks} inventory checks passed ({(fixture.IsSql ? "SQL Server" : "InMemory; not migration evidence")}).");
    }
}
finally { await fixture.CleanupAsync(); }

WebApplication CreateApp(bool realHttp, bool failQuery = false)
{
    var builder = WebApplication.CreateBuilder(new WebApplicationOptions
    {
        EnvironmentName = "Development", ApplicationName = typeof(CyberManagement.API.Program).Assembly.FullName
    });
    builder.Logging.ClearProviders();
    builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
    {
        ["ConnectionStrings:DefaultConnection"] = fixture.ConnectionString,
        ["Authentication:Jwt:SigningKey"] = signingKey,
    });
    builder.Services.AddApplicationServices();
    builder.Services.AddApiServices(builder.Configuration);
    builder.Services.AddInfrastructureServices(builder.Configuration);
    builder.Services.RemoveAll<DbContextOptions<ApplicationDbContext>>();
    builder.Services.RemoveAll<Microsoft.EntityFrameworkCore.Infrastructure.IDbContextOptionsConfiguration<ApplicationDbContext>>();
    builder.Services.AddDbContext<ApplicationDbContext>(fixture.Configure);
    if (failQuery)
    {
        builder.Services.RemoveAll<IInventoryQueries>();
        builder.Services.AddScoped<IInventoryQueries, FailedInventoryQuery>();
    }
    builder.Services.AddControllers().AddApplicationPart(typeof(InventoryController).Assembly);
    if (realHttp) builder.WebHost.UseUrls("http://127.0.0.1:5093");
    else builder.WebHost.UseTestServer();
    var app = builder.Build();
    app.UseExceptionHandler();
    app.UseAuthentication();
    app.UseAuthorization();
    app.MapControllers();
    if (realHttp) app.MapPost("/__checks/stop", () =>
    {
        app.Lifetime.StopApplication();
        return Microsoft.AspNetCore.Http.Results.NoContent();
    }).RequireAuthorization("permission:role.manage");
    return app;
}

async Task SeedTwice(WebApplication app)
{
    await using var scope = app.Services.CreateAsyncScope();
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    await fixture.SeedInventoryAsync(db);
    await fixture.SeedInventoryAsync(db);
    Check("Seed twice keeps exactly three products", await db.Products.CountAsync() == 3);
}

async Task<string> Login(HttpClient client, string username)
{
    using var response = await client.PostAsJsonAsync("/api/auth/login", new { username, password });
    response.EnsureSuccessStatusCode();
    return (await response.Content.ReadFromJsonAsync<LoginResponse>())!.AccessToken;
}

static Task<HttpResponseMessage> Send(HttpClient client, string token)
{
    var request = new HttpRequestMessage(HttpMethod.Get, "/api/admin/inventory");
    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
    return client.SendAsync(request);
}

async Task<InventoryItemDto[]> ReadInventory(HttpClient client, string token)
{
    using var response = await Send(client, token);
    response.EnsureSuccessStatusCode();
    return (await response.Content.ReadFromJsonAsync<InventoryItemDto[]>())!;
}

void CheckItems(InventoryItemDto[] items)
{
    Check("Inventory returns three SQL-shaped rows", items.Length == 3);
    Check("Rows sorted by integer product ID", items.All(item => item.Id > 0 && item.CategoryId > 0)
        && items.Select(item => item.Id).SequenceEqual(items.Select(item => item.Id).Order()));
    Check("Includes in-stock, empty and inactive products", items.Select(item => item.StockQuantity).SequenceEqual(new[] { 20, 0, 5 })
        && items.Select(item => item.IsActive).SequenceEqual(new[] { true, true, false }));
    Check("Prices and names match seeded source", items.Select(item => item.CurrentPrice).SequenceEqual(new[] { 10000m, 20000m, 15000m })
        && items[0].Name == "TEST_Nước suối");
}

async Task ExpectStatus(string name, HttpStatusCode expected, HttpResponseMessage response)
{
    using (response)
    {
        if (response.StatusCode != expected) throw new InvalidOperationException($"{name}: expected {(int)expected}, got {(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        Check(name, true);
    }
}

void Check(string name, bool passed)
{
    if (!passed) throw new InvalidOperationException(name);
    checks++;
    Console.WriteLine($"PASS {name}");
}

internal sealed class FailedInventoryQuery : IInventoryQueries
{
    public Task<IReadOnlyList<InventoryItemDto>> GetInventoryAsync(CancellationToken cancellationToken = default)
        => throw new InvalidOperationException("Injected query failure for handler verification.");
}
