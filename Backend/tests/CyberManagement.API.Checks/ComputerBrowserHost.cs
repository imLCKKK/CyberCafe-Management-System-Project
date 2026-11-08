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

// Test assembly only. Real HTTP/JWT/repositories/SQL, with a disposable database.
internal static class ComputerBrowserHost
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
        builder.Services.AddScoped<ICustomerRepository, CustomerRepository>();
        builder.Services.AddScoped<IComputerRepository, ComputerRepository>();
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
                database.Computers.Add(new Computer
                {
                    ComputerName = "SQL-PC-01", Status = "Available",
                    Room = new ComputerRoom { RoomName = "SQL room", Floor = 1 },
                    ComputerType = new ComputerType { TypeName = "SQL type", PricePerHour = 12345 }
                });
                await database.SaveChangesAsync();
            }
            app.UseExceptionHandler();
            app.UseAuthentication();
            app.UseAuthorization();
            app.MapControllers();
            // Independent SQL read in a fresh request scope, bypassing the computer service.
            // This endpoint is never mapped by the production API.
            app.MapGet("/api/admin/__checks/computers-sql", async (ApplicationDbContext database) =>
                Results.Ok(await database.Computers.FromSqlRaw("SELECT * FROM Computers")
                    .AsNoTracking().OrderBy(row => row.ComputerId)
                    .Select(row => new { row.ComputerId, row.ComputerName, row.Status, row.RoomId, row.ComputerTypeId })
                    .ToListAsync()))
                .RequireAuthorization(policy => policy.RequireRole("Admin"));
            await app.StartAsync();
            Console.WriteLine(JsonSerializer.Serialize(new { type = "ready", url = app.Urls.Single() }));
            // Parent closes stdin on success or failure; cleanup owns only the generated DB.
            await Console.In.ReadLineAsync();
        }
        finally
        {
            try { await app.StopAsync(); }
            finally { await fixture.CleanupAsync(); }
        }
    }
}
