using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using CyberManagement.API;
using CyberManagement.API.Controller;
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

// Explicit developer tool: real Sandbox HTTP, private retained SQL, no wallet or Order settlement.
internal static class ZaloPayLiveChecks
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    public static async Task RunAsync()
    {
        var server = Environment.GetEnvironmentVariable("AUTH_CHECKS_SQL_CONNECTION");
        if (string.IsNullOrWhiteSpace(server)) throw new InvalidOperationException("AUTH_CHECKS_SQL_CONNECTION is required for the isolated live Sandbox evidence.");
        var workspace = Path.GetFullPath(Directory.GetCurrentDirectory());
        if (!File.Exists(Path.Combine(workspace, "Backend", "CyberManagement.slnx")))
            throw new InvalidOperationException("Run live Sandbox checks from this checkout's root.");
        var output = Path.Combine(workspace, ".tools", "live-sandbox");
        Directory.CreateDirectory(output);
        // Prevent two evidence tools from creating conflicting local source IDs or request keys.
        await using var startupLock = new FileStream(Path.Combine(output, "run.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        var statePath = Path.Combine(output, "state.json");
        var previousState = File.Exists(statePath);
        var state = previousState
            ? JsonSerializer.Deserialize<LiveState>(await File.ReadAllTextAsync(statePath), JsonOptions) ?? throw new InvalidOperationException("Invalid live Sandbox state.")
            : new LiveState { Workspace = workspace, Database = "CyberManagement_GatewaySandbox_" + Guid.NewGuid().ToString("N"), TopUpRequestKey = Guid.NewGuid(), OrderRequestKey = Guid.NewGuid() };
        if (!string.Equals(Path.GetFullPath(state.Workspace), workspace, StringComparison.OrdinalIgnoreCase)
            || !Regex.IsMatch(state.Database, "^CyberManagement_GatewaySandbox_[a-f0-9]{32}$")
            || state.TopUpRequestKey == Guid.Empty || state.OrderRequestKey == Guid.Empty)
            throw new InvalidOperationException("The saved live Sandbox state belongs to another checkout or has invalid identifiers.");

        var connection = new SqlConnectionStringBuilder(server) { InitialCatalog = state.Database, ConnectTimeout = 10 };
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = "Development", ApplicationName = typeof(CyberManagement.API.Program).Assembly.FullName
        });
        builder.Logging.ClearProviders();
        builder.Configuration.AddUserSecrets(typeof(CyberManagement.API.Program).Assembly, optional: true);
        // Explicit process environment takes priority over any saved User Secrets.
        builder.Configuration.AddEnvironmentVariables();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = connection.ConnectionString,
            ["Authentication:Jwt:SigningKey"] = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48))
        });
        builder.WebHost.UseUrls("http://127.0.0.1:5097");
        builder.Services.AddApplicationServices();
        builder.Services.AddApiServices(builder.Configuration);
        builder.Services.AddInfrastructureServices(builder.Configuration);
        builder.Services.AddZaloPayGateway(builder.Configuration, "Development", enableWorker: false);
        builder.Services.AddControllers().AddApplicationPart(typeof(AuthController).Assembly);
        await using var app = builder.Build();
        // Only this callback path is exposed to a possible HTTPS tunnel. No auth/admin/health API is forwarded.
        app.Use(async (context, next) =>
        {
            if (!HttpMethods.IsPost(context.Request.Method) || context.Request.Path != ZaloPaySandboxOptions.CallbackPath)
            { context.Response.StatusCode = 404; return; }
            await next(context);
        });
        app.UseExceptionHandler();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapControllers();

        await using (var scope = app.Services.CreateAsyncScope())
        {
            if (!scope.ServiceProvider.GetRequiredService<IZaloPayGateway>().Enabled)
                throw new InvalidOperationException("Enable official Sandbox and configure its server User Secrets before this live tool. No fixture fallback is used.");
            var masterSettings = new SqlConnectionStringBuilder(connection.ConnectionString) { InitialCatalog = "master" };
            await using var master = new SqlConnection(masterSettings.ConnectionString);
            await master.OpenAsync();
            await using var exists = master.CreateCommand();
            exists.CommandText = "SELECT DB_ID(@database);";
            exists.Parameters.AddWithValue("@database", state.Database);
            var databaseExists = await exists.ExecuteScalarAsync() is not DBNull and not null;
            if (state.Initialized && !databaseExists)
                throw new InvalidOperationException("The retained evidence database is missing. Restore it before retrying; existing provider intents were not recreated.");
            if (!previousState && databaseExists)
                throw new InvalidOperationException("The generated evidence database already exists and was not adopted.");
            await SaveState();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            await db.Database.MigrateAsync();
            state.Initialized = true;
            await SaveState();
            await scope.ServiceProvider.GetRequiredService<IIdentitySeeder>().SeedAsync(new IdentitySeedOptions(true,
                RolePermissionCatalog.Roles.ToDictionary(role => role.Name, _ => (string?)"GatewayFixture-2026!")), CancellationToken.None);
            if (state.CustomerId == 0)
            {
                var customer = await db.Customers.SingleOrDefaultAsync(row => row.Phone == "0900000022");
                if (customer is null)
                {
                    customer = new Customer { FullName = "ZaloPay gateway live fixture", Phone = "0900000022", Status = "Active", WalletBalance = 0 };
                    customer.PasswordHash = scope.ServiceProvider.GetRequiredService<ICustomerPasswordHasher>().HashPassword(customer, "GatewayFixture-2026!");
                    db.Customers.Add(customer); await db.SaveChangesAsync();
                }
                state.CustomerId = customer.CustomerId;
                await SaveState();
            }
            if (state.OrderId == 0)
            {
                var employee = await db.Employees.Where(row => row.Username == "admin").Select(row => row.EmployeeId).SingleAsync();
                var order = new Order { CustomerId = state.CustomerId, EmployeeId = employee, TotalAmount = 30000, Status = "Pending" };
                db.Orders.Add(order); await db.SaveChangesAsync();
                state.OrderId = order.OrderId;
                await SaveState();
            }
        }

        await app.StartAsync();
        try
        {
            await using var scope = app.Services.CreateAsyncScope();
            var payments = scope.ServiceProvider.GetRequiredService<IZaloPayPaymentService>();
            var topup = await payments.CreateTopUpAsync(state.CustomerId, 20000, state.TopUpRequestKey, CancellationToken.None);
            state.TopUpIntentId = topup.Id; await SaveState();
            var order = await payments.CreateOrderAsync(state.CustomerId, state.OrderId, state.OrderRequestKey, CancellationToken.None);
            state.OrderIntentId = order.Id; await SaveState();
            await Task.Delay(1000);
            topup = await payments.SyncOwnAsync(state.CustomerId, topup.Id, CancellationToken.None);
            order = await payments.SyncOwnAsync(state.CustomerId, order.Id, CancellationToken.None);
            var proof = new
            {
                checkedAtUtc = DateTimeOffset.UtcNow, environment = "Official ZaloPay Sandbox", fixtureOnly = false, requestKeysReused = previousState,
                scope = "Create URL acceptance and Query observations; repeated runs reuse saved intents. No phone payment, wallet credit or Order fulfillment is claimed",
                topUp = new { topup.Id, topup.Purpose, topup.ExpectedAmountVnd, topup.Status, topup.CreatedAtUtc, topup.ExpiresAtUtc, paymentUrlPresent = topup.PaymentUrl is not null, topup.ProviderTransactionId, topup.VerifiedAtUtc, topup.RequiresReconciliation, topup.FailureReason },
                order = new { order.Id, order.Purpose, order.ExpectedAmountVnd, order.Status, order.CreatedAtUtc, order.ExpiresAtUtc, paymentUrlPresent = order.PaymentUrl is not null, order.ProviderTransactionId, order.VerifiedAtUtc, order.RequiresReconciliation, order.FailureReason }
            };
            var proofPath = Path.Combine(output, "proof.json");
            await File.WriteAllTextAsync(proofPath, JsonSerializer.Serialize(proof, JsonOptions));
            var accepted = topup.PaymentUrl is not null && order.PaymentUrl is not null;
            Console.WriteLine($"Official Sandbox Create/Query evidence saved: .tools/live-sandbox/proof.json; both Create URLs accepted: {accepted}.");
            Console.WriteLine($"TopUp #{topup.Id}: {topup.ExpectedAmountVnd:0} VND, {topup.Status}; Order #{order.Id}: {order.ExpectedAmountVnd:0} VND, {order.Status}.");
            Console.WriteLine("The private SQL database, source IDs and request keys were retained. No keys, MACs or payment URLs were printed.");
            if (!accepted)
                throw new InvalidOperationException("Sandbox did not return an accepted checkout URL for both intents. Inspect the redacted proof; live Create was not marked as passed.");
            if (Environment.GetEnvironmentVariable("ZALOPAY_LIVE_STAY_OPEN") == "1")
            {
                Console.WriteLine("GATEWAY_CALLBACK_HOST_READY http://127.0.0.1:5097 (callback POST only)");
                await app.WaitForShutdownAsync();
            }
        }
        finally { await app.StopAsync(); }

        Task SaveState() => File.WriteAllTextAsync(statePath, JsonSerializer.Serialize(state, JsonOptions));
    }

    private sealed class LiveState
    {
        public string Workspace { get; set; } = "";
        public string Database { get; set; } = "";
        public bool Initialized { get; set; }
        public Guid TopUpRequestKey { get; set; }
        public Guid OrderRequestKey { get; set; }
        public int CustomerId { get; set; }
        public int OrderId { get; set; }
        public int TopUpIntentId { get; set; }
        public int OrderIntentId { get; set; }
    }
}
