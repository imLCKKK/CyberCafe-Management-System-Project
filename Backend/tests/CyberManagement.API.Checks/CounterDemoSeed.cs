using System.Text.RegularExpressions;
using CyberManagement.API;
using CyberManagement.Application;
using CyberManagement.Application.DTOs.Identity;
using CyberManagement.Application.Interfaces.Services;
using CyberManagement.Application.Security;
using CyberManagement.Domain.Entities;
using CyberManagement.Infrastructure;
using CyberManagement.Infrastructure.Persistance;
using Microsoft.AspNetCore.Builder;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

internal static class CounterDemoSeed
{
    public static async Task RunAsync()
    {
        var connection = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
            ?? throw new InvalidOperationException("Provide the isolated counter demo connection.");
        var settings = new SqlConnectionStringBuilder(connection);
        if (!Regex.IsMatch(settings.InitialCatalog, "^CyberManagement_CounterDemo_[a-f0-9]{32}$"))
            throw new InvalidOperationException("Counter demo setup only accepts its own named, isolated database.");
        var builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?> { ["ConnectionStrings:DefaultConnection"] = connection });
        builder.Services.AddApplicationServices();
        builder.Services.AddApiServices(builder.Configuration);
        builder.Services.AddInfrastructureServices(builder.Configuration);
        await using var app = builder.Build();
        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await db.Database.MigrateAsync();
        await scope.ServiceProvider.GetRequiredService<IIdentitySeeder>().SeedAsync(new IdentitySeedOptions(true,
            RolePermissionCatalog.Roles.ToDictionary(role => role.Name, _ => (string?)"CounterAdmin-2026!")), CancellationToken.None);
        if (!await db.Customers.AnyAsync(row => row.Phone == "0900000021"))
        {
            var customer = new Customer { FullName = "Khách thử nạp tại quầy", Phone = "0900000021", Status = "Active" };
            customer.PasswordHash = scope.ServiceProvider.GetRequiredService<ICustomerPasswordHasher>()
                .HashPassword(customer, "CounterCustomer-2026!");
            db.Customers.Add(customer);
            await db.SaveChangesAsync();
        }
        Console.WriteLine("Counter fixture is ready; existing wallet and receipts were retained.");
    }
}
