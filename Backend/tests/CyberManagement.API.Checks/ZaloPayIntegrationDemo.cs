using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using CyberManagement.API;
using CyberManagement.API.Controller;
using CyberManagement.API.Realtime;
using CyberManagement.Application;
using CyberManagement.Application.DTOs.Identity;
using CyberManagement.Application.Interfaces.Services;
using CyberManagement.Application.Security;
using CyberManagement.Domain.Entities;
using CyberManagement.Infrastructure;
using CyberManagement.Infrastructure.Payments;
using CyberManagement.Infrastructure.Persistance;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

// Developer-only host. Retains a private SQL database and uses the official provider adapter.
// The optional Order is an explicit contract fixture; it is not Khang's checkout implementation.
internal static class ZaloPayIntegrationDemo
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    public static async Task RunAsync(bool prepareOrder)
    {
        var server = Environment.GetEnvironmentVariable("AUTH_CHECKS_SQL_CONNECTION");
        if (string.IsNullOrWhiteSpace(server)) throw new InvalidOperationException("AUTH_CHECKS_SQL_CONNECTION is required.");
        var workspace = Path.GetFullPath(Directory.GetCurrentDirectory());
        if (!File.Exists(Path.Combine(workspace, "Backend", "CyberManagement.slnx")))
            throw new InvalidOperationException("Run the integration demo from this checkout's root.");
        var directory = Path.Combine(workspace, ".tools", "integration-sandbox");
        Directory.CreateDirectory(directory);
        await using var startupLock = new FileStream(Path.Combine(directory, "startup.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        var path = Path.Combine(directory, "state.json");
        var previous = File.Exists(path);
        var state = previous ? JsonSerializer.Deserialize<State>(await File.ReadAllTextAsync(path), Json)!
            : new State { Workspace = workspace, Database = "CyberManagement_ZaloPayIntegration_" + Guid.NewGuid().ToString("N") };
        if (state is null || !string.Equals(Path.GetFullPath(state.Workspace), workspace, StringComparison.OrdinalIgnoreCase)
            || !Regex.IsMatch(state.Database, "^CyberManagement_ZaloPayIntegration_[a-f0-9]{32}$"))
            throw new InvalidOperationException("Invalid retained integration database or checkout.");
        if (prepareOrder && !state.Initialized) throw new InvalidOperationException("Start the integration host once before preparing an Order fixture.");
        var connection = new SqlConnectionStringBuilder(server) { InitialCatalog = state.Database, ConnectTimeout = 10 };
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = "Development", ApplicationName = typeof(CyberManagement.API.Program).Assembly.FullName
        });
        builder.Logging.ClearProviders();
        builder.Configuration.AddUserSecrets(typeof(CyberManagement.API.Program).Assembly, optional: true).AddEnvironmentVariables();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = connection.ConnectionString,
            ["Authentication:Jwt:SigningKey"] = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48))
        });
        builder.WebHost.UseUrls("http://127.0.0.1:5099", "http://127.0.0.1:5100");
        builder.Services.AddApplicationServices();
        builder.Services.AddApiServices(builder.Configuration);
        builder.Services.AddInfrastructureServices(builder.Configuration);
        builder.Services.AddZaloPayGateway(builder.Configuration, "Development", enableWorker: !prepareOrder);
        builder.Services.AddControllers().AddApplicationPart(typeof(AuthController).Assembly);
        await using var app = builder.Build();
        app.Use(async (context, next) =>
        {
            context.Response.Headers.CacheControl = "no-store";
            // Only this restricted listener may be exposed through a temporary HTTPS tunnel.
            if (context.Connection.LocalPort == 5100
                && (!HttpMethods.IsPost(context.Request.Method) || context.Request.Path != ZaloPaySandboxOptions.CallbackPath))
            { context.Response.StatusCode = 404; return; }
            await next(context);
        });
        app.UseExceptionHandler();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapGet("/health", () => Results.Ok(new { status = "ok", scope = "SCRUM-23 private Sandbox demo" })).AllowAnonymous();
        app.MapControllers();
        app.MapHub<RealtimeHub>(RealtimeHub.Endpoint, options => options.CloseOnAuthenticationExpiration = true);

        await using (var scope = app.Services.CreateAsyncScope())
        {
            var services = scope.ServiceProvider;
            if (!services.GetRequiredService<IZaloPayGateway>().Enabled)
                throw new InvalidOperationException("Official Sandbox must be enabled with server User Secrets. No mock fallback is used.");
            await using var master = new SqlConnection(new SqlConnectionStringBuilder(connection.ConnectionString) { InitialCatalog = "master" }.ConnectionString);
            await master.OpenAsync();
            await using var exists = master.CreateCommand();
            exists.CommandText = "SELECT DB_ID(@name)";
            exists.Parameters.AddWithValue("@name", state.Database);
            var found = await exists.ExecuteScalarAsync() is not DBNull and not null;
            if (state.Initialized && !found) throw new InvalidOperationException("Retained database missing. Restore it; do not recreate paid intents.");
            if (!previous && found) throw new InvalidOperationException("Generated database already exists and was not adopted.");
            await SaveState();
            var db = services.GetRequiredService<ApplicationDbContext>();
            await db.Database.MigrateAsync();
            await services.GetRequiredService<IIdentitySeeder>().SeedAsync(new IdentitySeedOptions(true,
                RolePermissionCatalog.Roles.ToDictionary(role => role.Name, _ => (string?)"SandboxAdmin-2026!")), CancellationToken.None);
            if (state.CustomerId == 0)
            {
                var customer = await db.Customers.SingleOrDefaultAsync(row => row.Phone == "0900000023");
                if (customer is null)
                {
                    customer = new Customer { FullName = "Khách thử ZaloPay SCRUM-23", Phone = "0900000023", Status = "Active", WalletBalance = 0 };
                    customer.PasswordHash = services.GetRequiredService<ICustomerPasswordHasher>().HashPassword(customer, "SandboxCustomer-2026!");
                    db.Customers.Add(customer);
                    await db.SaveChangesAsync();
                }
                state.CustomerId = customer.CustomerId;
            }
            state.Initialized = true;
            await SaveState();
        }

        if (prepareOrder)
        {
            await using var scope = app.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            if (state.OrderId != 0)
            {
                var prior = await db.Set<ZaloPayIntent>().SingleOrDefaultAsync(row => row.OrderId == state.OrderId);
                if (prior is not null && prior.AppliedAtUtc is not null)
                    throw new InvalidOperationException("The retained Order fixture is already paid. Its evidence is preserved; no extra fixture was created.");
                if (prior is not null && prior.ExpiresAtUtc <= DateTimeOffset.UtcNow)
                    throw new InvalidOperationException("The retained Order fixture expired. Inspect its proof before explicitly preparing another fixture.");
            }
            if (state.OrderId == 0)
            {
                var product = new Product { Category = new Category { CategoryName = "SCRUM-23 contract fixture" },
                    ProductName = "Món thử điểm nối thanh toán", Price = 30000, Stock = 19 };
                var order = new Order { CustomerId = state.CustomerId, EmployeeId = await db.Employees.Where(row => row.Username == "admin")
                    .Select(row => row.EmployeeId).SingleAsync(), TotalAmount = 30000, Status = "AwaitingPayment" };
                order.Details.Add(new OrderDetail { Product = product, ProductNameSnapshot = product.ProductName,
                    Quantity = 1, UnitPrice = 30000, Status = "Pending" });
                db.Orders.Add(order);
                await db.SaveChangesAsync();
                state.OrderId = order.OrderId;
                state.OrderRequestKey = Guid.NewGuid();
                await SaveState();
            }
            var intent = await scope.ServiceProvider.GetRequiredService<IZaloPayPaymentService>()
                .CreateOrderAsync(state.CustomerId, state.OrderId, state.OrderRequestKey, CancellationToken.None);
            state.OrderIntentId = intent.Id;
            await SaveState();
            await using var transaction = await db.Database.BeginTransactionAsync();
            await scope.ServiceProvider.GetRequiredService<IZaloPayOrderPaymentBridge>()
                .RegisterAsync(state.CustomerId, state.OrderId, intent.Id, intent.ExpiresAtUtc, CancellationToken.None);
            await db.SaveChangesAsync();
            await transaction.CommitAsync();
            Console.WriteLine($"ORDER_CONTRACT_FIXTURE_READY Order #{state.OrderId}, intent #{intent.Id}, 30000 VND. Reload the wallet to view its QR.");
            Console.WriteLine("This checks the shared confirmation port; Khang's checkout/stock/voucher flow is not claimed.");
            return;
        }

        // Startup mutations are complete; a separate explicit preparation command can reuse the state.
        await startupLock.DisposeAsync();
        await app.StartAsync();
        Console.WriteLine("SCRUM23_READY API http://127.0.0.1:5099; callback-only listener http://127.0.0.1:5100");
        Console.WriteLine("Customer 0900000023 / SandboxCustomer-2026!; retained private SQL; no real-money payment.");
        await app.WaitForShutdownAsync();
        Task SaveState() => File.WriteAllTextAsync(path, JsonSerializer.Serialize(state, Json));
    }

    private sealed class State
    {
        public string Workspace { get; set; } = "";
        public string Database { get; set; } = "";
        public bool Initialized { get; set; }
        public int CustomerId { get; set; }
        public int OrderId { get; set; }
        public Guid OrderRequestKey { get; set; }
        public int OrderIntentId { get; set; }
    }
}
