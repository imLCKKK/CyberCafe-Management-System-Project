using System.Text.Json;
using System.Threading.Channels;
using CyberManagement.API;
using CyberManagement.API.Controller;
using CyberManagement.API.Realtime;
using CyberManagement.Application;
using CyberManagement.Application.DTOs.Identity;
using CyberManagement.Application.DTOs.Realtime;
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
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

// Test assembly only: real Kestrel/SQL/Hub, isolated database, no production test endpoints.
internal static class RealtimeIntegrationHost
{
    public static async Task RunAsync()
    {
        var serverConnection = Environment.GetEnvironmentVariable("AUTH_CHECKS_SQL_CONNECTION");
        if (string.IsNullOrWhiteSpace(serverConnection))
            throw new InvalidOperationException("AUTH_CHECKS_SQL_CONNECTION is required for the isolated realtime SQL host.");
        var fixture = new AuthCheckDatabase(serverConnection);
        var commands = Channel.CreateUnbounded<string>();
        var listenUrl = Environment.GetEnvironmentVariable("REALTIME_CHECKS_URL") ?? "http://127.0.0.1:0";
        WebApplication? app = null;
        var customerId = 0;
        var firstStart = true;
        ConsoleCancelEventHandler cancel = (_, args) => { args.Cancel = true; commands.Writer.TryWrite("stop"); };
        Console.CancelKeyPress += cancel;
        _ = Task.Run(async () =>
        {
            while (await Console.In.ReadLineAsync() is { } command)
                commands.Writer.TryWrite(command.Trim());
            commands.Writer.TryWrite("stop");
        });
        try
        {
            while (true)
            {
                var builder = WebApplication.CreateBuilder(new WebApplicationOptions
                {
                    EnvironmentName = "Development",
                    ApplicationName = typeof(CyberManagement.API.Program).Assembly.FullName
                });
                builder.WebHost.UseUrls(listenUrl);
                builder.Logging.ClearProviders();
                builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Authentication:Jwt:SigningKey"] = "checks-only-signing-key-that-is-at-least-32-bytes"
                });
                builder.Services.Configure<HostOptions>(options => options.ShutdownTimeout = TimeSpan.FromSeconds(2));
                builder.Services.AddDbContext<ApplicationDbContext>(fixture.Configure);
                builder.Services.AddApplicationServices();
                builder.Services.AddApiServices(builder.Configuration);
                builder.Services.AddScoped<IIdentityRepository, IdentityRepository>();
                builder.Services.AddScoped<ICustomerIdentityRepository, CustomerIdentityRepository>();
                builder.Services.AddScoped<ICustomerRepository, CustomerRepository>();
                builder.Services.AddScoped<IComputerRepository, ComputerRepository>();
                builder.Services.AddScoped<ICatalogRepository, CatalogRepository>();
                builder.Services.AddScoped<IInventoryQueries, InventoryQueries>();
                builder.Services.AddScoped<IEmployeePasswordHasher, EmployeePasswordHasher>();
                builder.Services.AddScoped<ICustomerPasswordHasher, CustomerPasswordHasher>();
                builder.Services.AddControllers().AddApplicationPart(typeof(AuthController).Assembly);
                app = builder.Build();
                if (firstStart)
                {
                    await using var scope = app.Services.CreateAsyncScope();
                    var database = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                    await fixture.InitializeAsync(database);
                    var passwords = RolePermissionCatalog.Roles.ToDictionary(role => role.Name,
                        role => (string?)$"{role.Name}-check-password-2026!");
                    await scope.ServiceProvider.GetRequiredService<IIdentitySeeder>()
                        .SeedAsync(new IdentitySeedOptions(true, passwords), CancellationToken.None);
                    var customer = new Customer { FullName = "Realtime check customer", Phone = "0900000010", Status = "Active" };
                    customer.PasswordHash = scope.ServiceProvider.GetRequiredService<ICustomerPasswordHasher>()
                        .HashPassword(customer, "Customer-check-password-2026!");
                    var room = new ComputerRoom { RoomName = "Realtime check room", Floor = 1 };
                    var type = new ComputerType { TypeName = "Realtime check type", PricePerHour = 10000 };
                    database.AddRange(customer, room, type, new Computer
                    {
                        ComputerName = "LAN-PC-01", Room = room, ComputerType = type, Status = "Available"
                    });
                    await database.SaveChangesAsync();
                    customerId = customer.CustomerId;
                }

                var instanceId = Guid.NewGuid();
                app.UseExceptionHandler();
                app.UseAuthentication();
                app.UseAuthorization();
                app.MapGet("/health", () => Results.Ok(new { status = "ok" })).AllowAnonymous();
                app.MapControllers();
                app.MapHub<RealtimeHub>(RealtimeHub.Endpoint, options => options.CloseOnAuthenticationExpiration = true);
                app.MapGet("/api/admin/__checks/instance", () => Results.Ok(new { instanceId }))
                    .RequireAuthorization(policy => policy.RequireRole("Admin"));
                foreach (var command in new[] { "restart", "stop" })
                {
                    var requestedCommand = command;
                    app.MapPost($"/api/admin/__checks/{command}", (HttpContext context) =>
                    {
                        context.Response.OnCompleted(() =>
                        {
                            commands.Writer.TryWrite(requestedCommand);
                            return Task.CompletedTask;
                        });
                        return Results.Accepted();
                    }).RequireAuthorization(policy => policy.RequireRole("Admin"));
                }
                app.MapPost("/api/admin/__checks/customer-barrier", async (IRealtimePublisher publisher) =>
                {
                    var message = new RealtimeEvent(Guid.NewGuid(), "CheckBarrier", customerId, DateTimeOffset.UtcNow);
                    await publisher.PublishToAccountAsync(RealtimeAccountType.Customer, customerId, message, CancellationToken.None);
                    return Results.Ok(new { message.EventId });
                }).RequireAuthorization(policy => policy.RequireRole("Admin"));

                await app.StartAsync();
                listenUrl = app.Urls.Single();
                Console.WriteLine(JsonSerializer.Serialize(new { type = firstStart ? "ready" : "restarted", url = listenUrl, instanceId }));
                firstStart = false;
                string instruction;
                do { instruction = await commands.Reader.ReadAsync(); }
                while (instruction is not ("restart" or "stop"));
                await StopAsync(app);
                app = null;
                if (instruction == "stop") break;
            }
        }
        finally
        {
            Console.CancelKeyPress -= cancel;
            if (app is not null) await StopAsync(app);
            await fixture.CleanupAsync();
        }
    }

    private static async Task StopAsync(WebApplication app)
    {
        try
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            await app.StopAsync(timeout.Token);
        }
        catch (OperationCanceledException) { }
        finally { await app.DisposeAsync(); }
    }
}
