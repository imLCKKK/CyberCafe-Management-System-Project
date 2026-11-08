using System.Text.Json;
using CyberManagement.API;
using CyberManagement.API.Controller;
using CyberManagement.Application;
using CyberManagement.Application.DTOs.Identity;
using CyberManagement.Application.Interfaces.Repositories;
using CyberManagement.Application.Interfaces.Services;
using CyberManagement.Application.Security;
using CyberManagement.Domain.Entities;
using CyberManagement.Infrastructure.Persistance;
using CyberManagement.Infrastructure.Repositories;
using CyberManagement.Infrastructure.Security;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

// Test assembly only: real HTTP, authentication, repositories and disposable SQL database.
internal static class CustomerBrowserHost
{
    public static async Task RunAsync()
    {
        var connection = Environment.GetEnvironmentVariable("AUTH_CHECKS_SQL_CONNECTION");
        if (string.IsNullOrWhiteSpace(connection))
            throw new InvalidOperationException("AUTH_CHECKS_SQL_CONNECTION is required; no InMemory fallback.");

        var fixture = new AuthCheckDatabase(connection);
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = "Development",
            ApplicationName = typeof(CyberManagement.API.Program).Assembly.FullName
        });
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Logging.ClearProviders();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Authentication:Jwt:SigningKey"] = "checks-only-signing-key-that-is-at-least-32-bytes"
        });
        builder.Services.AddDbContext<ApplicationDbContext>(fixture.Configure);
        builder.Services.AddApplicationServices();
        builder.Services.AddApiServices(builder.Configuration);
        builder.Services.AddScoped<IIdentityRepository, IdentityRepository>();
        builder.Services.AddScoped<ICustomerIdentityRepository, CustomerIdentityRepository>();
        builder.Services.AddScoped<IComputerRepository, ComputerRepository>();
        builder.Services.AddScoped<ICustomerRepository, CustomerRepository>();
        builder.Services.AddScoped<ICatalogRepository, CatalogRepository>();
        builder.Services.AddScoped<IInventoryQueries, InventoryQueries>();
        builder.Services.AddScoped<IEmployeePasswordHasher, EmployeePasswordHasher>();
        builder.Services.AddScoped<ICustomerPasswordHasher, CustomerPasswordHasher>();
        builder.Services.AddControllers().AddApplicationPart(typeof(AuthController).Assembly);
        await using var app = builder.Build();

        try
        {
            await using (var scope = app.Services.CreateAsyncScope())
            {
                var database = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                await fixture.InitializeAsync(database);
                var passwords = RolePermissionCatalog.Roles.ToDictionary(role => role.Name,
                    role => (string?)$"{role.Name}-check-password-2026!");
                await scope.ServiceProvider.GetRequiredService<IIdentitySeeder>()
                    .SeedAsync(new IdentitySeedOptions(true, passwords), CancellationToken.None);
                var hasher = scope.ServiceProvider.GetRequiredService<ICustomerPasswordHasher>();
                var customer = new Customer
                {
                    FullName = "SQL Wallet Customer", Phone = "0908000001", Email = "wallet@sql.example",
                    WalletBalance = 123456, Status = "Active", CreatedAt = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc)
                };
                customer.PasswordHash = hasher.HashPassword(customer, "Customer-check-password-2026!");
                customer.WalletTransactions.Add(new WalletTransaction
                {
                    Amount = 150000, BalanceAfter = 150000, Type = "CounterTopUp",
                    Description = "SQL counter collection", CreatedAt = new DateTime(2026, 11, 1, 9, 0, 0, DateTimeKind.Utc)
                });
                customer.WalletTransactions.Add(new WalletTransaction
                {
                    Amount = -26544, BalanceAfter = 123456, Type = "OrderPayment",
                    Description = "SQL order payment", CreatedAt = new DateTime(2026, 11, 1, 10, 0, 0, DateTimeKind.Utc)
                });
                var emptyCustomer = new Customer
                {
                    FullName = "SQL Empty Wallet Customer", Phone = "0908000002", WalletBalance = 0,
                    Status = "Active", CreatedAt = new DateTime(2026, 10, 2, 0, 0, 0, DateTimeKind.Utc)
                };
                emptyCustomer.PasswordHash = hasher.HashPassword(emptyCustomer, "Customer-check-password-2026!");
                database.Customers.AddRange(customer, emptyCustomer);
                await database.SaveChangesAsync();
            }

            var walletFailure = new WalletFailureSwitch();
            app.UseExceptionHandler();
            app.UseAuthentication();
            app.UseAuthorization();
            app.Use(async (context, next) =>
            {
                if (walletFailure.Enabled && context.Request.Path.Value is { } requestPath
                    && requestPath.StartsWith("/api/", StringComparison.Ordinal)
                    && requestPath.EndsWith("/wallet", StringComparison.Ordinal))
                {
                    context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
                    await context.Response.WriteAsJsonAsync(new
                    {
                        title = "Wallet temporarily unavailable", detail = "SQL browser check failure mode."
                    });
                    return;
                }
                await next(context);
            });
            app.MapControllers();

            // Independent SQL reads bypass the customer service. Neither endpoint exists in production.
            app.MapGet("/api/admin/__checks/customers-sql", async (ApplicationDbContext database) =>
                Results.Ok(new
                {
                    customers = await database.Customers.FromSqlRaw("SELECT * FROM Customers").AsNoTracking()
                        .OrderBy(row => row.CustomerId)
                        .Select(row => new { row.CustomerId, row.FullName, row.Phone, row.Email, row.WalletBalance, row.Status })
                        .ToListAsync(),
                    transactions = await database.WalletTransactions.FromSqlRaw("SELECT * FROM WalletTransactions").AsNoTracking()
                        .OrderBy(row => row.TransactionId)
                        .Select(row => new { row.TransactionId, row.CustomerId, row.Amount, row.BalanceAfter, row.Type, row.Description })
                        .ToListAsync()
                }))
                .RequireAuthorization(policy => policy.RequireRole("Admin"));
            app.MapPost("/api/admin/__checks/wallet-failure", (HttpContext context) =>
                {
                    walletFailure.Enabled = context.Request.Query["enabled"] == "true";
                    return Results.NoContent();
                })
                .RequireAuthorization(policy => policy.RequireRole("Admin"));

            await app.StartAsync();
            Console.WriteLine(JsonSerializer.Serialize(new { type = "ready", url = app.Urls.Single() }));
            await Console.In.ReadLineAsync();
        }
        finally
        {
            try { await app.StopAsync(); }
            finally { await fixture.CleanupAsync(); }
        }
    }

    private sealed class WalletFailureSwitch
    {
        public volatile bool Enabled;
    }
}
