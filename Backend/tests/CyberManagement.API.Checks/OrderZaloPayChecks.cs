using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text;
using System.Security.Cryptography;
using CyberManagement.API;
using CyberManagement.API.Controller;
using CyberManagement.Application;
using CyberManagement.Application.DTOs.Identity;
using CyberManagement.Application.DTOs.Orders;
using CyberManagement.Application.DTOs.Payments;
using CyberManagement.Application.DTOs.Realtime;
using CyberManagement.Application.Interfaces.Repositories;
using CyberManagement.Application.Interfaces.Services;
using CyberManagement.Application.Security;
using CyberManagement.Application.Services;
using CyberManagement.Domain.Entities;
using CyberManagement.Infrastructure;
using CyberManagement.Infrastructure.Persistance;
using CyberManagement.Infrastructure.Repositories;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

internal static class OrderZaloPayChecks
{
    public static async Task<int> RunAsync(string? connection)
    {
        if (string.IsNullOrWhiteSpace(connection)) throw new ArgumentException("--order-zalopay-only requires AUTH_CHECKS_SQL_CONNECTION.");
        var fixture = new AuthCheckDatabase(connection);
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions {
            EnvironmentName = "Development", ApplicationName = typeof(CyberManagement.API.Program).Assembly.FullName });
        builder.WebHost.UseTestServer(); builder.Logging.ClearProviders();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?> {
            ["Authentication:Jwt:SigningKey"] = "order-zalopay-check-signing-key-at-least-32-bytes",
            ["ConnectionStrings:DefaultConnection"] = connection });
        builder.Services.AddInfrastructureServices(builder.Configuration);
        var failure = new SettlementFailure();
        builder.Services.AddDbContext<ApplicationDbContext>(options => { fixture.Configure(options); options.AddInterceptors(failure); });
        builder.Services.AddApplicationServices(); builder.Services.AddApiServices(builder.Configuration);
        var clock = new CheckoutClock(); builder.Services.AddSingleton<TimeProvider>(clock);
        var events = new CheckoutEvents(); builder.Services.AddSingleton<IRealtimePublisher>(events);
        builder.Services.AddSingleton<GatewayChecksGateway>();
        builder.Services.AddSingleton<IZaloPayGateway>(p => p.GetRequiredService<GatewayChecksGateway>());
        builder.Services.AddScoped<IZaloPayIntentRepository, ZaloPayIntentRepository>();
        builder.Services.AddScoped<IZaloPayPaymentService, ZaloPayPaymentService>();
        builder.Services.AddControllers().AddApplicationPart(typeof(AuthController).Assembly);
        await using var app = builder.Build();
        var checks = 0; int owner = 0, product = 0, adminId = 0;
        const string password = "Order-ZaloPay-fixture-2026!";
        try
        {
            await using (var scope = app.Services.CreateAsyncScope())
            {
                var db = Db(scope); await fixture.InitializeAsync(db);
                await scope.ServiceProvider.GetRequiredService<IIdentitySeeder>().SeedAsync(new IdentitySeedOptions(true,
                    RolePermissionCatalog.Roles.ToDictionary(x => x.Name, _ => (string?)password)), default);
                var customer = new Customer { FullName = "Checkout customer", Phone = "0900028001", WalletBalance = 90000 };
                var food = new Product { ProductName = "Reserved food", Price = 10000, Stock = 100,
                    Category = new Category { CategoryName = "Checkout category" } };
                db.AddRange(customer, food);
                db.Promotions.AddRange(new Promotion { Code = "HOLDONE", PromoName = "One held use", DiscountType = "Fixed",
                    DiscountValue = 1000, TotalUsageLimit = 1, StartDate = DateTimeOffset.UtcNow.AddDays(-1), EndDate = DateTimeOffset.UtcNow.AddDays(1) },
                    new Promotion { Code = "FREEZALO", PromoName = "Free", DiscountType = "Percent", DiscountValue = 100,
                    StartDate = DateTimeOffset.UtcNow.AddDays(-1), EndDate = DateTimeOffset.UtcNow.AddDays(1) });
                await db.SaveChangesAsync(); owner = customer.CustomerId; product = food.ProductId;
                adminId = await db.Employees.Where(x => x.Username == "admin").Select(x => x.EmployeeId).SingleAsync();
                Check("Model matches additive migration", !db.Database.HasPendingModelChanges());
            }
            app.UseExceptionHandler(); app.UseAuthentication(); app.UseAuthorization(); app.MapControllers(); await app.StartAsync();
            using var client = app.GetTestClient();
            using var login = await client.PostAsJsonAsync("/api/auth/login", new { username = "admin", password });
            var token = (await login.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("accessToken").GetString();
            var gateway = app.Services.GetRequiredService<GatewayChecksGateway>();
            var actor = new OrderActor("Staff", adminId);
            var body = new StaffCreateOrderRequest(owner, null, [new(product, 1)], "ZaloPay", "HOLDONE");
            using var create = await Send(body, "zalo-original");
            Check("HTTP creates ZaloPay reservation", create.StatusCode == HttpStatusCode.Created);
            var order = (await create.Content.ReadFromJsonAsync<OrderDto>())!;
            Check("Pending bill, URL and durable 15 minute deadline", order.Status == "AwaitingPayment"
                && order.Payment.PaymentStatus == "Pending" && order.Payment.PaymentUrl is not null
                && order.Payment.ExpiresAtUtc == new DateTimeOffset(order.CreatedAtUtc).AddMinutes(15));
            Check("Waiting order cannot be received", await CannotConfirm(order));
            await using (var scope = app.Services.CreateAsyncScope())
            {
                var db = Db(scope); var payment = await db.Payments.SingleAsync(x => x.OrderId == order.Id);
                var intent = await db.Set<ZaloPayIntent>().SingleAsync(x => x.OrderId == order.Id);
                Check("One bill uses committed merchant, amount and deadline", payment.MerchantTransactionId == intent.MerchantTransactionId
                    && payment.Amount == 9000 && intent.ExpectedAmountVnd == 9000 && intent.ExpiresAtUtc == order.Payment.ExpiresAtUtc
                    && (await db.Products.FindAsync(product))!.Stock == 99 && await db.PromotionUsages.CountAsync() == 1
                    && await db.WalletTransactions.CountAsync() == 0);
            }
            using (var replay = await Send(body, "zalo-original"))
                Check("Replay reuses bill and deadline", replay.StatusCode == HttpStatusCode.OK
                    && (await replay.Content.ReadFromJsonAsync<OrderDto>())!.Payment.ExpiresAtUtc == order.Payment.ExpiresAtUtc && gateway.CreateCount == 1);
            using (var limit = await Send(body, "zalo-voucher-limit")) Check("Held voucher blocks last-use competitor", limit.StatusCode == HttpStatusCode.Conflict);
            using (var spoof = await Send(new { customerId = owner, items = new[] { new { productId = product, quantity = 1 } }, paymentMethod = "ZaloPay", totalAmount = 1 }, "zalo-spoof"))
                Check("Browser cannot choose gateway amount", spoof.StatusCode == HttpStatusCode.BadRequest);

            await Succeed(order.Id, "261120000000001");
            var paid = await Read(order.Id);
            Check("Verified Query changes order to paid Pending", paid.Status == "Pending" && paid.Payment.PaymentStatus == "Paid");
            var snapshot = await Snapshot(order.Id);
            clock.Now = clock.Now.AddMinutes(16);
            await Apply(order.Id); await Apply(order.Id);
            Check("Success replay after TTL cannot expire or refund", snapshot == await Snapshot(order.Id));
            clock.Reset();

            var expiring = await New("zalo-expiring");
            clock.Now = expiring.Payment.ExpiresAtUtc!.Value; // Exact deadline is already expired.
            await Task.WhenAll(Apply(expiring.Id), Apply(expiring.Id));
            var expired = await Read(expiring.Id);
            Check("Two workers release once at exact deadline", expired.Status == "Expired" && expired.Items.All(x => x.Status == "Cancelled")
                && expired.Payment.PaymentStatus == "Expired" && expired.Payment.PaymentUrl is null);
            var expirySnapshot = await Snapshot(expiring.Id); await Apply(expiring.Id);
            Check("Restart-style expiry replay is idempotent", expirySnapshot == await Snapshot(expiring.Id));
            await Succeed(expiring.Id, "261120000000002");
            var late = await Read(expiring.Id);
            Check("Late money does not resurrect order", late.Status == "Expired" && late.Payment.PaymentStatus == "RefundPending"
                && late.Payment.CollectedAmount == 10000 && late.Payment.RefundPendingAmount == 10000);
            await using (var scope = app.Services.CreateAsyncScope())
            {
                var db = Db(scope); var refund = await db.Payments.SingleAsync(x => x.OrderId == late.Id && x.Type == "Refund");
                Check("Late obligation is unapproved and stock returned only once", refund.ApprovedByEmployeeId is null && refund.ApprovedAtUtc is null
                    && await db.InventoryTransactions.CountAsync(x => x.OrderId == late.Id && x.Type == "Return") == 1
                    && await db.WalletTransactions.CountAsync() == 0);
            }
            snapshot = await Snapshot(late.Id); await Task.WhenAll(Apply(late.Id), Apply(late.Id));
            Check("Late settlement replay does not duplicate refund/audit", snapshot == await Snapshot(late.Id));
            clock.Reset();

            // Keep the one-use voucher on an unpaid order, then prove the expiry releases it.
            await using (var scope = app.Services.CreateAsyncScope())
            {
                var db = Db(scope); var promo = await db.Promotions.SingleAsync(x => x.Code == "HOLDONE");
                promo.TotalUsageLimit = 2; await db.SaveChangesAsync();
            }
            var release = await New("zalo-release", "HOLDONE"); clock.Now = release.Payment.ExpiresAtUtc!.Value;
            await Apply(release.Id); clock.Reset();
            using (var reusable = await Send(body, "zalo-reuse-voucher")) Check("Expiry frees voucher use", reusable.StatusCode == HttpStatusCode.Created);

            var crash = await New("zalo-crash"); clock.Now = crash.Payment.ExpiresAtUtc!.Value;
            var eventsBefore = events.Messages.Count;
            failure.Return = true;
            await ExpectFailure(() => Apply(crash.Id));
            Check("Expiry rollback keeps reservation and sends no event", (await Read(crash.Id)).Status == "AwaitingPayment" && events.Messages.Count == eventsBefore);
            await Apply(crash.Id); Check("Expiry recovers after transaction failure", (await Read(crash.Id)).Status == "Expired");
            clock.Reset();

            var lateCrash = await New("zalo-audit-crash"); clock.Now = lateCrash.Payment.ExpiresAtUtc!.Value;
            failure.Audit = true; eventsBefore = events.Messages.Count;
            await ExpectFailure(() => Succeed(lateCrash.Id, "261120000000003"));
            Check("Money settlement/audit rollback emits no event", (await Read(lateCrash.Id)).Status == "AwaitingPayment" && events.Messages.Count == eventsBefore);
            await using (var scope = app.Services.CreateAsyncScope())
                Check("Verified receipt survives separate settlement failure", (await Db(scope).Set<ZaloPayIntent>().SingleAsync(x => x.OrderId == lateCrash.Id)).Status == "Succeeded");
            await Apply(lateCrash.Id);
            Check("Durable receipt settles after restart without another charge", (await Read(lateCrash.Id)).Payment.PaymentStatus == "RefundPending");
            clock.Reset();

            var invalid = await New("zalo-invalid-receipt");
            await using (var scope = app.Services.CreateAsyncScope())
            {
                var intent = await Db(scope).Set<ZaloPayIntent>().SingleAsync(x => x.OrderId == invalid.Id);
                gateway.Set(intent.MerchantTransactionId, new("Succeeded", new GatewayReceipt(intent.ProviderAppId,
                    "261120000000004", intent.ExpectedAmountVnd + 1, clock.Now), null));
                await scope.ServiceProvider.GetRequiredService<IZaloPayPaymentService>().SyncOwnAsync(owner, intent.IntentId, default);
                await scope.ServiceProvider.GetRequiredService<IOrderZaloPaySettlement>().ApplyAsync(invalid.Id, default);
            }
            Check("Wrong verified amount cannot pay or receive food", (await Read(invalid.Id)).Payment.CollectedAmount == 0 && await CannotConfirm(invalid));
            using (var badCallback = await client.PostAsJsonAsync("/api/payments/zalopay/callback", new { data = "{}", mac = "invalid", type = 1 }))
                Check("Invalid callback is not acknowledged as paid", (await badCallback.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("return_code").GetInt32() != 1);

            gateway.RejectNextCreate = 1;
            var rejected = await New("zalo-rejected"); await Apply(rejected.Id);
            Check("Definitive create rejection exposes Failed without receiving food", (await Read(rejected.Id)).Payment.PaymentStatus == "Failed" && await CannotConfirm(rejected));
            await using (var scope = app.Services.CreateAsyncScope())
            {
                var intent = await Db(scope).Set<ZaloPayIntent>().SingleAsync(x => x.OrderId == rejected.Id);
                Check("Failed checkout remains eligible for background reconciliation", (await scope.ServiceProvider.GetRequiredService<IZaloPayIntentRepository>()
                    .GetDueIdsAsync(clock.Now, 5, default)).Contains(intent.IntentId));
            }
            await Succeed(rejected.Id, "261120000000005");
            await using (var scope = app.Services.CreateAsyncScope())
            {
                var db = Db(scope); var paymentId = (await Read(rejected.Id)).Payment.PaymentId;
                Check("Independent verified Query recovers Failed with audit", (await Read(rejected.Id)).Payment.PaymentStatus == "Paid"
                    && await db.AuditLogs.AnyAsync(x => x.EntityType == "Payment" && x.EntityId == paymentId && x.Action.Contains("Failed->Succeeded")));
            }

            var racing = await New("zalo-expiry-query-race"); clock.Now = racing.Payment.ExpiresAtUtc!.Value;
            await Task.WhenAll(Apply(racing.Id), Succeed(racing.Id, "261120000000006"));
            Check("Query racing expiry creates one closed-order obligation", (await Read(racing.Id)).Payment.RefundPendingAmount == 10000);
            clock.Reset();

            gateway.Enabled = false;
            var unavailable = await New("zalo-disabled");
            Check("Disabled gateway preserves 201 order for recovery", unavailable.Payment.PaymentUrl is null && unavailable.Payment.RequiresReconciliation);
            gateway.Enabled = true;
            await using (var scope = app.Services.CreateAsyncScope()) await scope.ServiceProvider.GetRequiredService<OrderService>().StartGatewayAsync(unavailable.Id, default);
            Check("Worker recovers missing intent without a new order", (await Read(unavailable.Id)).Payment.PaymentUrl is not null);
            var boot = await New("zalo-worker-startup"); clock.Now = boot.Payment.ExpiresAtUtc!.Value;
            using (var worker = new CyberManagement.API.Services.OrderCheckoutWorker(app.Services.GetRequiredService<IServiceScopeFactory>(),
                Microsoft.Extensions.Logging.Abstractions.NullLogger<CyberManagement.API.Services.OrderCheckoutWorker>.Instance))
            {
                await worker.StartAsync(default);
                using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                while ((await Read(boot.Id)).Status != "Expired") await Task.Delay(25, deadline.Token);
                await worker.StopAsync(deadline.Token);
            }
            Check("Hosted worker expires saved orders immediately on startup", (await Read(boot.Id)).Status == "Expired");
            clock.Reset();
            var creates = gateway.CreateCount;
            var free = await New("zalo-zero", "FREEZALO");
            Check("Zero-value ZaloPay skips provider and deadline", free.Payment.PaymentStatus == "NotRequired" && free.Status == "Pending"
                && free.Payment.ExpiresAtUtc is null && gateway.CreateCount == creates);
            var callbackOrder = await New("zalo-callback");
            var callbackCode = await Callback(callbackOrder.Id, "261120000000007");
            Check("Valid callback ACK follows committed Order payment", callbackCode == 1 && (await Read(callbackOrder.Id)).Payment.PaymentStatus == "Paid");
            snapshot = await Snapshot(callbackOrder.Id);
            Check("Callback replay ACK does not duplicate money or events", await Callback(callbackOrder.Id, "261120000000007") == 1
                && snapshot == await Snapshot(callbackOrder.Id));
            await using (var scope = app.Services.CreateAsyncScope())
            { var db = Db(scope); (await db.Products.FindAsync(product))!.Stock = 1; await db.SaveChangesAsync(); }
            var stockRace = await Task.WhenAll(Send(body with { VoucherCode = null }, "zalo-last-stock-a"), Send(body with { VoucherCode = null }, "zalo-last-stock-b"));
            try { Check("Concurrent ZaloPay checkouts reserve last stock once", stockRace.Count(x => x.StatusCode == HttpStatusCode.Created) == 1
                && stockRace.Count(x => x.StatusCode == HttpStatusCode.Conflict) == 1); }
            finally { foreach (var response in stockRace) response.Dispose(); }
            await using (var scope = app.Services.CreateAsyncScope())
            {
                var waiter = await Db(scope).Employees.Where(x => x.Username == "waiter01").Select(x => x.EmployeeId).SingleAsync();
                try { await scope.ServiceProvider.GetRequiredService<OrderService>().CreateAsync(new("Staff", waiter), body, "waiter-zalo-denied"); throw new Exception("Waiter spent gateway"); }
                catch (UnauthorizedAccessException) { Check("Waiter cannot create ZaloPay bill", true); }
            }
            return checks;

            async Task<HttpResponseMessage> Send<T>(T data, string key)
            {
                var request = new HttpRequestMessage(HttpMethod.Post, "/api/admin/orders") { Content = JsonContent.Create(data) };
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                request.Headers.Add("Idempotency-Key", key); return await client.SendAsync(request);
            }
            async Task<OrderDto> New(string key, string? voucher = null)
            {
                using var response = await Send(body with { VoucherCode = voucher }, key);
                if (response.StatusCode != HttpStatusCode.Created) throw new Exception(await response.Content.ReadAsStringAsync());
                return (await response.Content.ReadFromJsonAsync<OrderDto>())!;
            }
            async Task<OrderDto> Read(int id)
            { await using var scope = app.Services.CreateAsyncScope(); return await scope.ServiceProvider.GetRequiredService<OrderService>().GetAsync(actor, id); }
            async Task Apply(int id)
            { await using var scope = app.Services.CreateAsyncScope(); await scope.ServiceProvider.GetRequiredService<IOrderZaloPaySettlement>().ApplyAsync(id, default); }
            async Task Succeed(int id, string providerId)
            {
                await using var scope = app.Services.CreateAsyncScope(); var db = Db(scope);
                var intent = await db.Set<ZaloPayIntent>().AsNoTracking().SingleAsync(x => x.OrderId == id);
                gateway.Set(intent.MerchantTransactionId, new("Succeeded", new GatewayReceipt(intent.ProviderAppId, providerId, intent.ExpectedAmountVnd, clock.Now), null));
                await scope.ServiceProvider.GetRequiredService<IZaloPayPaymentService>().SyncOwnAsync(owner, intent.IntentId, default);
            }
            async Task<int> Callback(int id, string providerId)
            {
                await using var scope = app.Services.CreateAsyncScope();
                var intent = await Db(scope).Set<ZaloPayIntent>().AsNoTracking().SingleAsync(x => x.OrderId == id);
                gateway.Set(intent.MerchantTransactionId, new("Succeeded", new GatewayReceipt(intent.ProviderAppId, providerId, intent.ExpectedAmountVnd, clock.Now), null));
                var data = JsonSerializer.Serialize(new { app_id = intent.ProviderAppId, app_trans_id = intent.MerchantTransactionId,
                    app_user = "customer:" + owner, app_time = intent.CreatedAtUtc.ToUnixTimeMilliseconds(), amount = (long)intent.ExpectedAmountVnd,
                    zp_trans_id = long.Parse(providerId, System.Globalization.CultureInfo.InvariantCulture), server_time = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() });
                var mac = Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(GatewayChecksGateway.CallbackKey), Encoding.UTF8.GetBytes(data)));
                using var response = await client.PostAsJsonAsync("/api/payments/zalopay/callback", new { data, mac, type = 1 });
                return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("return_code").GetInt32();
            }
            async Task<bool> CannotConfirm(OrderDto item)
            {
                await using var scope = app.Services.CreateAsyncScope();
                var fresh = await Read(item.Id);
                try { await scope.ServiceProvider.GetRequiredService<OrderService>().ConfirmAsync(actor, item.Id, item.Items[0].Id, new(fresh.Version), "zalo-unpaid-confirm"); return false; }
                catch (CyberManagement.Application.Exceptions.OrderRuleException e) { return e.Code == "PaymentRequired"; }
            }
            async Task<string> Snapshot(int id)
            {
                await using var scope = app.Services.CreateAsyncScope(); var db = Db(scope);
                return JsonSerializer.Serialize(new { order = await Read(id), payments = await db.Payments.Where(x => x.OrderId == id).CountAsync(),
                    movements = await db.InventoryTransactions.Where(x => x.OrderId == id).CountAsync(), stock = (await db.Products.FindAsync(product))!.Stock,
                    audits = await db.AuditLogs.CountAsync(), history = await db.OrderStatusHistories.CountAsync() });
            }
        }
        finally { await app.StopAsync(); await fixture.CleanupAsync(); }
        void Check(string name, bool value) { if (!value) throw new Exception("FAIL: " + name); checks++; Console.WriteLine("PASS: " + name); }
    }
    private static ApplicationDbContext Db(AsyncServiceScope scope) => scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    private static async Task ExpectFailure(Func<Task> action)
    { try { await action(); } catch (InvalidOperationException e) when (e.Message == "Injected checkout failure") { return; } throw new Exception("Expected injected failure"); }
    private sealed class CheckoutClock : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = DateTimeOffset.UtcNow;
        public override DateTimeOffset GetUtcNow() => Now;
        public void Reset() => Now = DateTimeOffset.UtcNow;
    }
    private sealed class SettlementFailure : SaveChangesInterceptor
    {
        public bool Return, Audit;
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData data, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            var db = data.Context!;
            if (Return && db.ChangeTracker.Entries<InventoryTransaction>().Any(x => x.State == EntityState.Added && x.Entity.Type == "Return")
                || Audit && db.ChangeTracker.Entries<AuditLog>().Any(x => x.State == EntityState.Added))
            { Return = false; Audit = false; throw new InvalidOperationException("Injected checkout failure"); }
            return ValueTask.FromResult(result);
        }
    }
    private sealed class CheckoutEvents : IRealtimePublisher
    {
        public ConcurrentQueue<RealtimeEvent> Messages { get; } = new();
        public Task PublishToPermissionAsync(string p, RealtimeEvent e, CancellationToken ct) { Messages.Enqueue(e); return Task.CompletedTask; }
        public Task PublishToAccountAsync(RealtimeAccountType t, int id, RealtimeEvent e, CancellationToken ct) { Messages.Enqueue(e); return Task.CompletedTask; }
        public Task PublishTournamentChangedAsync(RealtimeEvent e, CancellationToken ct) => Task.CompletedTask;
    }
}
