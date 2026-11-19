using System.Collections.Concurrent;
using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CyberManagement.API.Controller;
using CyberManagement.Application.DTOs.Payments;
using CyberManagement.Application.Interfaces.Services;
using CyberManagement.Application.Interfaces.Repositories;
using CyberManagement.Domain.Entities;
using CyberManagement.Infrastructure.Payments;
using CyberManagement.Infrastructure.Persistance;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

// Explicit fixture gateway, registered by the check executable only. Production never uses this type.
internal sealed class GatewayChecksGateway(IServiceScopeFactory scopes) : IZaloPayGateway, IDisposable
{
    public const string CallbackKey = "gateway-checks-only-key2-not-a-provider-secret";
    private readonly HttpClient unusedHttp = new(new NoNetwork());
    private readonly ConcurrentDictionary<string, GatewayQueryResult> results = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, GatewayCreateRequest> requests = new(StringComparer.Ordinal);
    private int creates;
    private int queries;
    public bool Enabled { get; set; } = true;
    public int AppId => 2553;
    public int CreateCount => Volatile.Read(ref creates);
    public int QueryCount => Volatile.Read(ref queries);
    public int RejectNextCreate;
    public int UnknownNextCreate;
    public Func<GatewayCreateRequest, Task>? OnCreate;
    public Func<string, Task>? OnQuery;
    public IReadOnlyCollection<GatewayCreateRequest> Requests => requests.Values.ToArray();
    public void Set(string merchantId, GatewayQueryResult result) => results[merchantId] = result;

    public async Task<GatewayCreateResult> CreateAsync(GatewayCreateRequest request, CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref creates);
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await db.Database.OpenConnectionAsync(cancellationToken);
        await db.Database.ExecuteSqlRawAsync("SET LOCK_TIMEOUT 1500;", cancellationToken);
        var stored = await db.Set<ZaloPayIntent>().AsNoTracking().SingleAsync(row => row.IntentId == request.IntentId, cancellationToken);
        if (stored.Status != "Pending" || stored.MerchantTransactionId != request.MerchantTransactionId
            || stored.ExpectedAmountVnd != request.AmountVnd || stored.CustomerId != request.CustomerId
            || stored.Purpose != request.Purpose || stored.OrderId != request.OrderId)
            throw new InvalidOperationException("The gateway was called before an immutable Pending intent committed.");
        // A new SQL connection must be able to take the intent's write lock while HTTP runs.
        // This fails instead of silently passing if the caller retained its SQL transaction.
        await using (var transaction = await db.Database.BeginTransactionAsync(cancellationToken))
        {
            _ = await db.Set<ZaloPayIntent>().FromSqlInterpolated(
                $"SELECT * FROM [ZaloPayIntents] WITH (UPDLOCK, HOLDLOCK) WHERE [IntentId] = {request.IntentId}")
                .AsNoTracking().SingleAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        requests[request.MerchantTransactionId] = request;
        if (OnCreate is { } hook) await hook(request);
        if (Interlocked.Exchange(ref RejectNextCreate, 0) == 1) return new(null, true, "ProviderRejected");
        if (Interlocked.Exchange(ref UnknownNextCreate, 0) == 1) return new(null, false, "CreateUnavailable");
        return new("https://qcgateway.zalopay.vn/openinapp?order=fixture-" + request.IntentId, false, null);
    }

    public async Task<GatewayQueryResult> QueryAsync(int appId, string merchantTransactionId, CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref queries);
        if (appId != AppId) throw new InvalidOperationException("Query must use the intent's saved merchant app.");
        if (OnQuery is { } hook) await hook(merchantTransactionId);
        return results.GetValueOrDefault(merchantTransactionId) ?? new("Pending", null, null);
    }

    public GatewayCallback? ReadCallback(string data, string mac, int type) => new ZaloPaySandboxGateway(unusedHttp,
        new FixtureOptions(new ZaloPaySandboxOptions
        {
            Enabled = true, AppId = AppId, Key1 = "gateway-checks-only-key1", Key2 = CallbackKey,
            CallbackUrl = "https://callback.example.test/api/payments/zalopay/callback",
            RedirectUrl = "http://127.0.0.1:5173/client/wallet"
        })).ReadCallback(data, mac, type);

    public void Dispose() => unusedHttp.Dispose();
    private sealed class NoNetwork : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Fixture callback parsing must never call the network.");
    }
    private sealed class FixtureOptions(ZaloPaySandboxOptions value) : IOptionsMonitor<ZaloPaySandboxOptions>
    {
        public ZaloPaySandboxOptions CurrentValue => value;
        public ZaloPaySandboxOptions Get(string? name) => value;
        public IDisposable? OnChange(Action<ZaloPaySandboxOptions, string?> listener) => null;
    }
}

internal static class ZaloPayPaymentChecks
{
    public static async Task<int> RunAsync(WebApplication app, HttpClient client,
        IReadOnlyDictionary<string, string> staffTokens, string customerToken, RealtimeSaveFailure saveFailure)
    {
        await using (var provider = app.Services.CreateAsyncScope())
            if (!provider.ServiceProvider.GetRequiredService<ApplicationDbContext>().Database.IsSqlServer())
            {
                Console.WriteLine("SKIP gateway intent/receipt checks require SQL Server; fixture/InMemory cannot prove SQL deduplication.");
                return 0;
            }
        var gateway = app.Services.GetRequiredService<GatewayChecksGateway>();
        const string own = "/api/customers/me/payments/zalopay";
        const string callbackPath = "/api/payments/zalopay/callback";
        const string password = "Gateway-check-customer-2026!";
        var checks = 0;
        var nextProviderId = 261115000000000L;
        var (owner, token) = await CreateCustomer("0782200001", "Gateway source owner");
        var (_, otherToken) = await CreateCustomer("0782200002", "Other gateway owner");
        var (lockedId, lockedToken) = await CreateCustomer("0782200003", "Locked gateway owner");
        await SetCustomerStatus(lockedId, "Locked");

        await RunCreateRecoveryChecks();

        await Expect("Anonymous cannot create a gateway TopUp", HttpStatusCode.Unauthorized,
            await Post(own + "/topups", null, new { amountVnd = 10000 }, Guid.NewGuid().ToString()));
        foreach (var (role, staffToken) in staffTokens)
            await Expect($"{role} cannot create an owned customer gateway intent", HttpStatusCode.Forbidden,
                await Post(own + "/topups", staffToken, new { amountVnd = 10000 }, Guid.NewGuid().ToString()));
        await Expect("A locked customer's issued JWT cannot create an intent", HttpStatusCode.Forbidden,
            await Post(own + "/topups", lockedToken, new { amountVnd = 10000 }, Guid.NewGuid().ToString()));
        foreach (var amount in new[] { 0m, -1m, 1.5m, 1_000_000_000m })
            await Expect("Gateway rejects invalid VND " + amount, HttpStatusCode.BadRequest,
                await Post(own + "/topups", token, new { amountVnd = amount }, Guid.NewGuid().ToString()));
        foreach (var key in new string?[] { null, "bad", Guid.Empty.ToString() })
            await Expect("Gateway requires a nonempty canonical idempotency UUID", HttpStatusCode.BadRequest,
                await Post(own + "/topups", token, new { amountVnd = 10000 }, key));
        await Expect("Browser cannot supply paid status, purpose or somebody else's customer", HttpStatusCode.BadRequest,
            await Post(own + "/topups", token, new { amountVnd = 10000, status = "Succeeded", purpose = "Order", customerId = 1 }, Guid.NewGuid().ToString()));

        var topUpKey = Guid.NewGuid().ToString();
        var createsBefore = gateway.CreateCount;
        var topUp = await CreateTopUp(35000m, topUpKey);
        Check("TopUp intent is Pending with an authoritative immutable amount", topUp.Status == "Pending" && topUp.ExpectedAmountVnd == 35000m
            && topUp.Purpose == "TopUp" && topUp.SourceId == topUp.Id && topUp.CustomerId == owner);
        Check("Intent commits before provider HTTP without retaining SQL write locks", gateway.CreateCount == createsBefore + 1);
        Check("Create redirect URL is only a checkout link, not paid evidence", topUp.PaymentUrl?.StartsWith("https://qcgateway.zalopay.vn/", StringComparison.Ordinal) == true
            && topUp.VerifiedAtUtc is null && topUp.ProviderTransactionId is null);
        using (var replayResponse = await Post(own + "/topups", token, new { amountVnd = 35000 }, topUpKey))
        {
            var replay = await Body(replayResponse);
            Check("Same request key returns the persisted intent and merchant ID", replay.Id == topUp.Id
                && replay.MerchantTransactionId == topUp.MerchantTransactionId && gateway.CreateCount == createsBefore + 1);
        }
        await Expect("Same gateway key with a different amount is rejected", HttpStatusCode.Conflict,
            await Post(own + "/topups", token, new { amountVnd = 35001 }, topUpKey));
        var racingKey = Guid.NewGuid().ToString();
        var createsBeforeRace = gateway.CreateCount;
        var createRaces = await Task.WhenAll(Post(own + "/topups", token, new { amountVnd = 35002 }, racingKey),
            Post(own + "/topups", token, new { amountVnd = 35002 }, racingKey), Post(own + "/topups", token, new { amountVnd = 35002 }, racingKey));
        var racingCreates = new List<ZaloPayIntentDto>();
        foreach (var response in createRaces) { using (response) racingCreates.Add(await Body(response)); }
        Check("Concurrent same-key requests reuse one persisted intent and one provider Create", racingCreates.Select(row => row.Id).Distinct().Count() == 1
            && racingCreates.Select(row => row.MerchantTransactionId).Distinct().Count() == 1 && gateway.CreateCount == createsBeforeRace + 1);
        await Expect("Other customer cannot read the intent", HttpStatusCode.NotFound, await Get(own + "/" + topUp.Id, otherToken));
        await Expect("Other customer cannot sync the intent", HttpStatusCode.NotFound, await Post(own + "/" + topUp.Id + "/sync", otherToken, new { }));
        await Expect("Staff cannot bypass customer ownership for gateway read", HttpStatusCode.Forbidden, await Get(own + "/" + topUp.Id, staffTokens["Admin"]));

        var orderId = await CreateOrder(owner, 43210);
        var orderKey = Guid.NewGuid().ToString();
        await Expect("Browser cannot replace the Order's server price", HttpStatusCode.BadRequest,
            await Post(own + "/orders/" + orderId, token, new { amountVnd = 1 }, orderKey));
        using var orderResponse = await Post(own + "/orders/" + orderId, token, new { }, orderKey);
        var orderIntent = await Body(orderResponse);
        Check("Shared adapter creates Order from its saved SQL price", orderIntent.Purpose == "Order" && orderIntent.SourceId == orderId
            && orderIntent.ExpectedAmountVnd == 43210m && gateway.Requests.Any(row => row.IntentId == orderIntent.Id && row.Purpose == "Order"
                && row.OrderId == orderId && row.AmountVnd == 43210m));
        Check("Checkout AwaitingPayment reaches the provider without changing its original expiry",
            orderIntent.ExpiresAtUtc <= orderIntent.CreatedAtUtc.AddMinutes(15) && orderIntent.PaymentUrl is not null);
        await Expect("A second key cannot open another gateway bill for one Order", HttpStatusCode.Conflict,
            await Post(own + "/orders/" + orderId, token, new { }, Guid.NewGuid().ToString()));
        await Expect("Order source ownership is checked on the server", HttpStatusCode.NotFound,
            await Post(own + "/orders/" + orderId, otherToken, new { }, Guid.NewGuid().ToString()));

        // Creation failures are classified without pretending that a timeout means no provider order exists.
        gateway.UnknownNextCreate = 1;
        var unknownKey = Guid.NewGuid().ToString();
        var unknown = await CreateTopUp(22001, unknownKey);
        Check("Ambiguous Create stays Pending for reconciliation", unknown.Status == "Pending" && unknown.RequiresReconciliation
            && unknown.PaymentUrl is null && unknown.VerifiedAtUtc is null);
        var unknownCreates = gateway.CreateCount;
        using (var unknownReplay = await Post(own + "/topups", token, new { amountVnd = 22001 }, unknownKey))
        {
            var saved = await Body(unknownReplay);
            Check("Ambiguous Create without NotFound evidence does not open another bill", saved.Id == unknown.Id
                && saved.MerchantTransactionId == unknown.MerchantTransactionId && gateway.CreateCount == unknownCreates);
        }
        gateway.RejectNextCreate = 1;
        var rejected = await CreateTopUp(22002);
        Check("Definitive invalid Create has a durable Failed status", rejected.Status == "Failed" && rejected.VerifiedAtUtc is null);

        gateway.Set(topUp.MerchantTransactionId, new("Pending", null, null));
        Check("Provider processing stays Pending", (await Sync(topUp.Id)).Status == "Pending");
        gateway.Set(topUp.MerchantTransactionId, new("Pending", null, "PaymentAttemptFailed", true));
        var attemptFailed = await Sync(topUp.Id);
        Check("A retryable failed payment attempt does not terminate the intent", attemptFailed.Status == "Pending"
            && attemptFailed.VerifiedAtUtc is null && attemptFailed.FailureReason is not null);
        gateway.Set(topUp.MerchantTransactionId, new("Unavailable", null, "QueryUnavailable"));
        var unavailable = await Sync(topUp.Id);
        Check("Provider outage preserves saved identity without a mock receipt", unavailable.Status == "Pending"
            && unavailable.MerchantTransactionId == topUp.MerchantTransactionId && unavailable.ProviderTransactionId is null);

        var providerId = ProviderId();
        gateway.Set(topUp.MerchantTransactionId, Paid(topUp, providerId));
        var validCallback = Callback(topUp, providerId);
        using (var wrongMac = await Post(callbackPath, null, validCallback with { mac = new string('0', 64) }))
            Check("Wrong callback MAC is rejected without acknowledging money", await Code(wrongMac) == 2);
        var callbackQueries = gateway.QueryCount;
        foreach (var invalid in new[]
        {
            Callback(topUp, providerId, amount: 35001), Callback(topUp, providerId, appUser: "customer:999999"),
            Callback(topUp, providerId, appTime: topUp.CreatedAtUtc.ToUnixTimeMilliseconds() + 1),
            Callback(topUp, providerId, merchantId: MerchantId(topUp.CreatedAtUtc))
        })
        {
            using var response = await Post(callbackPath, null, invalid);
            Check("Signed callback must match saved amount/user/time/merchant", await Code(response) == 2);
        }
        Check("Rejected callback metadata cannot cause a provider Query", gateway.QueryCount == callbackQueries);
        using (var success = await Post(callbackPath, null, validCallback))
            Check("Valid callback acknowledges only an independently queried committed receipt", await Code(success) == 1);
        var succeeded = await Read(topUp.Id);
        Check("Verified receipt records exact expected amount and provider reference", succeeded.Status == "Succeeded"
            && succeeded.ProviderAmountVnd == 35000 && succeeded.ProviderTransactionId == providerId && succeeded.VerifiedAtUtc is not null);
        var verifiedAt = succeeded.VerifiedAtUtc;
        for (var replay = 0; replay < 3; replay++)
        {
            using var response = await Post(callbackPath, null, validCallback);
            Check("Exact duplicate callback returns success for the same committed receipt", await Code(response) == 1);
            _ = await Sync(topUp.Id);
        }
        Check("Callback and Query replay keep the original confirmation timestamp", (await Read(topUp.Id)).VerifiedAtUtc == verifiedAt);
        await using (var staleScope = app.Services.CreateAsyncScope())
        {
            var stale = await staleScope.ServiceProvider.GetRequiredService<IZaloPayIntentRepository>()
                .RecordReceiptAsync(topUp.Id, Guid.NewGuid(), new(gateway.AppId, ProviderId(), 35000, null),
                    DateTimeOffset.UtcNow, CancellationToken.None);
            Check("A stale Query lease cannot flag or overwrite the winning receipt", stale.Intent.Status == "Succeeded"
                && !stale.Intent.RequiresReconciliation && stale.Intent.ProviderTransactionId == providerId
                && stale.Intent.VerifiedAtUtc == verifiedAt && !stale.Recorded);
            var saved = await staleScope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Set<ZaloPayIntent>()
                .AsNoTracking().SingleAsync(row => row.IntentId == topUp.Id);
            Check("Query confirmation never claims an independently unverified callback payment time", saved.ProviderOccurredAtUtc is null);
        }

        var mismatch = await CreateTopUp(22003);
        var mismatchProvider = ProviderId();
        gateway.Set(mismatch.MerchantTransactionId, new("Succeeded", new(gateway.AppId, mismatchProvider, 22004, null), null));
        var wrongAmount = await Sync(mismatch.Id);
        Check("Wrong verified provider amount preserves evidence without false Succeeded", wrongAmount.Status != "Succeeded"
            && wrongAmount.ExpectedAmountVnd == 22003 && wrongAmount.ProviderAmountVnd == 22004 && wrongAmount.RequiresReconciliation);
        var conflicting = await CreateTopUp(22005);
        gateway.Set(conflicting.MerchantTransactionId, new("Succeeded", new(gateway.AppId, providerId, 22005, null), null));
        var providerCollision = await Sync(conflicting.Id);
        Check("One provider receipt cannot be assigned to a second intent", providerCollision.Status != "Succeeded" && providerCollision.RequiresReconciliation);
        Check("Provider collision never rewrites the first confirmed intent", (await Read(topUp.Id)).ProviderTransactionId == providerId);

        var expired = await CreateTopUp(22006);
        gateway.Set(expired.MerchantTransactionId, new("Expired", null, null));
        Check("Explicit provider expiry is stored as Expired", (await Sync(expired.Id)).Status == "Expired");
        var lateProvider = ProviderId();
        gateway.Set(expired.MerchantTransactionId, Paid(expired, lateProvider));
        var late = await Sync(expired.Id);
        Check("Late verified money is retained and flagged for reconciliation", late.Status == "Succeeded" && late.RequiresReconciliation
            && late.ProviderTransactionId == lateProvider);
        var rejectedProvider = ProviderId();
        gateway.Set(rejected.MerchantTransactionId, Paid(rejected, rejectedProvider));
        var lateFailed = await Sync(rejected.Id);
        Check("Unexpected money after a failed Create is retained for review", lateFailed.Status == "Succeeded" && lateFailed.RequiresReconciliation);

        var racing = await CreateTopUp(22007);
        var racingProvider = ProviderId();
        gateway.Set(racing.MerchantTransactionId, Paid(racing, racingProvider));
        var raceResponses = await Task.WhenAll(Post(callbackPath, null, Callback(racing, racingProvider)),
            Post(own + "/" + racing.Id + "/sync", token, new { }), Post(callbackPath, null, Callback(racing, racingProvider)));
        foreach (var response in raceResponses) { Check("Concurrent callback/Query returns a handled result", response.StatusCode == HttpStatusCode.OK); response.Dispose(); }
        var raceSaved = await Read(racing.Id);
        Check("Concurrent callback/Query produces one durable verified intent", raceSaved.Status == "Succeeded" && raceSaved.ProviderTransactionId == racingProvider);
        await using (var raceScope = app.Services.CreateAsyncScope())
            Check("SQL independently has one row for the racing provider receipt", await raceScope.ServiceProvider.GetRequiredService<ApplicationDbContext>()
                .Set<ZaloPayIntent>().CountAsync(row => row.ProviderAppId == gateway.AppId && row.ProviderTransactionId == racingProvider) == 1);

        // Save failure occurs after the external receipt was observed, before its SQL commit.
        var rollback = await CreateTopUp(22008);
        var rollbackProvider = ProviderId();
        gateway.Set(rollback.MerchantTransactionId, Paid(rollback, rollbackProvider));
        gateway.OnQuery = merchant =>
        {
            if (merchant == rollback.MerchantTransactionId) { saveFailure.FailNextSave = 1; gateway.OnQuery = null; }
            return Task.CompletedTask;
        };
        using (var failedReceipt = await Post(callbackPath, null, Callback(rollback, rollbackProvider)))
            Check("Callback never acknowledges success when receipt commit fails", failedReceipt.StatusCode != HttpStatusCode.OK || await Code(failedReceipt) != 1);
        var rolledBack = await Read(rollback.Id);
        Check("Failed SQL receipt commit leaves no half-confirmed state", rolledBack.Status == "Pending" && rolledBack.VerifiedAtUtc is null
            && rolledBack.ProviderTransactionId is null);
        await ExpireLease(rollback.Id);
        var recovered = await Sync(rollback.Id);
        Check("A new request scope recovers the same saved intent after rollback", recovered.Status == "Succeeded"
            && recovered.MerchantTransactionId == rollback.MerchantTransactionId && recovered.ProviderTransactionId == rollbackProvider);

        // Simulate a paused Create request whose lease expired before its HTTP response was saved.
        gateway.OnCreate = async request =>
        {
            gateway.OnCreate = null;
            var pending = new ZaloPayIntentDto(request.IntentId, request.CustomerId, request.Purpose,
                request.OrderId ?? request.IntentId, request.AmountVnd, "Pending", null, request.MerchantTransactionId,
                request.CreatedAtUtc, request.ExpiresAtUtc, null, null, null, false, null);
            var receipt = ProviderId();
            gateway.Set(request.MerchantTransactionId, Paid(pending, receipt));
            await ExpireLease(request.IntentId);
            using var response = await Post(callbackPath, null, Callback(pending, receipt));
            if (await Code(response) != 1) throw new InvalidOperationException("Early callback fixture was not durably recorded.");
        };
        var earlyCallback = await CreateTopUp(22010);
        Check("A delayed Create response cannot overwrite an earlier committed callback", earlyCallback.Status == "Succeeded"
            && earlyCallback.VerifiedAtUtc is not null && earlyCallback.ProviderTransactionId is not null);

        var orderedProvider = ProviderId();
        gateway.Set(orderIntent.MerchantTransactionId, Paid(orderIntent, orderedProvider));
        var orderPaid = await Sync(orderIntent.Id);
        Check("AwaitingPayment Order accepts its verified receipt without OrderClosed reconciliation", orderPaid.Status == "Succeeded"
            && orderPaid.ProviderAmountVnd == 43210 && !orderPaid.RequiresReconciliation && orderPaid.FailureReason is null);
        var legacyPendingId = await CreateOrder(owner, 43211, "Pending");
        using (var legacyResponse = await Post(own + "/orders/" + legacyPendingId, token, new { }, Guid.NewGuid().ToString()))
        {
            var legacy = await Body(legacyResponse);
            gateway.Set(legacy.MerchantTransactionId, Paid(legacy, ProviderId()));
            var legacyPaid = await Sync(legacy.Id);
            Check("Legacy Pending Order remains compatible with the adapter", legacyPaid.Status == "Succeeded"
                && !legacyPaid.RequiresReconciliation && legacyPaid.FailureReason is null);
        }
        foreach (var state in new[] { "Cancelled", "Expired" })
        {
            var closedId = await CreateOrder(owner, 43212, state);
            var beforeClosedCreate = gateway.CreateCount;
            await Expect(state + " Order cannot open a new provider bill", HttpStatusCode.Conflict,
                await Post(own + "/orders/" + closedId, token, new { }, Guid.NewGuid().ToString()));
            Check("Rejected " + state + " source never calls provider Create", gateway.CreateCount == beforeClosedCreate);
        }
        var closedOrderId = await CreateOrder(owner, 22011);
        using var closedOrderResponse = await Post(own + "/orders/" + closedOrderId, token, new { }, Guid.NewGuid().ToString());
        var closedOrderIntent = await Body(closedOrderResponse);
        await using (var closeScope = app.Services.CreateAsyncScope())
        {
            var db = closeScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            (await db.Orders.SingleAsync(row => row.OrderId == closedOrderId)).Status = "Cancelled";
            await db.SaveChangesAsync();
        }
        gateway.Set(closedOrderIntent.MerchantTransactionId, Paid(closedOrderIntent, ProviderId()));
        var closedPaid = await Sync(closedOrderIntent.Id);
        Check("Money for a closed Order is retained for reconciliation", closedPaid.Status == "Succeeded" && closedPaid.RequiresReconciliation);
        await using (var unchanged = app.Services.CreateAsyncScope())
        {
            var db = unchanged.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            Check("S2-M-2 does not credit TopUp wallet or invent a ledger", await db.Customers.Where(row => row.CustomerId == owner)
                .Select(row => row.WalletBalance).SingleAsync() == 0 && !await db.WalletTransactions.AnyAsync(row => row.CustomerId == owner));
            Check("S2-M-2 does not confirm Order, change stock or create a Payment mirror", await db.Orders.Where(row => row.OrderId == orderId)
                .Select(row => row.Status).SingleAsync() == "AwaitingPayment" && !await db.Payments.AnyAsync(row => row.OrderId == orderId));
            Check("Late gateway money never resurrects a cancelled Order", await db.Orders.Where(row => row.OrderId == closedOrderId)
                .Select(row => row.Status).SingleAsync() == "Cancelled");
        }
        gateway.Enabled = false;
        try
        {
            var createsAtDisabled = gateway.CreateCount;
            await Expect("Disabled gateway returns 503 without a local mock success", HttpStatusCode.ServiceUnavailable,
                await Post(own + "/topups", token, new { amountVnd = 22009 }, Guid.NewGuid().ToString()));
            Check("Disabled gateway does not call Create", gateway.CreateCount == createsAtDisabled);
        }
        finally { gateway.Enabled = true; gateway.OnQuery = null; gateway.OnCreate = null; }
        return checks;

        async Task RunCreateRecoveryChecks()
        {
            var recoveryIds = new List<int>();
            var key = Guid.NewGuid();
            var unsent = await SeedUnsent("TopUp", null, 23001, key);
            gateway.Set(unsent.MerchantTransactionId, new("NotFound", null, null));
            var before = gateway.CreateCount;
            using (var response = await Post(own + "/topups", token, new { amountVnd = 23001 }, key.ToString()))
            {
                var resumed = await Body(response);
                Check("Retry recovers an intent committed before the first Create HTTP", resumed.PaymentUrl is not null
                    && resumed.Status == "Pending" && gateway.CreateCount == before + 1);
                SameSnapshot("Recovered Create reuses merchant, amount, app_time and expiry", unsent, resumed);
                var sent = gateway.Requests.Single(row => row.IntentId == unsent.Id);
                Check("Recovery sends the persisted Create parameters without extending the payment window",
                    sent.MerchantTransactionId == unsent.MerchantTransactionId && sent.AmountVnd == unsent.ExpectedAmountVnd
                    && sent.CreatedAtUtc == unsent.CreatedAtUtc && sent.ExpiresAtUtc == unsent.ExpiresAtUtc);
            }
            await ParkRecoveryRows();

            var raceKey = Guid.NewGuid();
            var racingUnsent = await SeedUnsent("TopUp", null, 23002, raceKey);
            gateway.Set(racingUnsent.MerchantTransactionId, new("NotFound", null, null));
            var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            gateway.OnCreate = async request =>
            {
                if (request.IntentId != racingUnsent.Id) return;
                entered.TrySetResult();
                await release.Task.WaitAsync(TimeSpan.FromSeconds(15));
            };
            var createsAtRace = gateway.CreateCount;
            var first = Post(own + "/topups", token, new { amountVnd = 23002 }, raceKey.ToString());
            try
            {
                await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
                using (var second = await Post(own + "/topups", token, new { amountVnd = 23002 }, raceKey.ToString()))
                {
                    var duringCreate = await Body(second);
                    SameSnapshot("Concurrent retry observes the same recoverable intent", racingUnsent, duringCreate);
                    Check("Concurrent retry does not send a second Create while HTTP is running", gateway.CreateCount == createsAtRace + 1);
                }
                await Reconcile();
                Check("Worker shares the Create lease with an in-flight request", gateway.CreateCount == createsAtRace + 1);
            }
            finally { release.TrySetResult(); gateway.OnCreate = null; }
            using (var response = await first)
                Check("Winning recovery saves one checkout URL after concurrent retries", (await Body(response)).PaymentUrl is not null);
            await using (var scope = app.Services.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                Check("Concurrent recovery leaves one durable intent and releases its lease", await db.Set<ZaloPayIntent>()
                    .CountAsync(row => row.MerchantTransactionId == racingUnsent.MerchantTransactionId) == 1
                    && await db.Set<ZaloPayIntent>().Where(row => row.IntentId == racingUnsent.Id)
                        .AllAsync(row => row.QueryLeaseToken == null && row.QueryLeaseUntilUtc == null));
            }
            await ParkRecoveryRows();

            var fenced = await SeedUnsent("TopUp", null, 23006, Guid.NewGuid());
            Guid staleCreateToken;
            await using (var scope = app.Services.CreateAsyncScope())
            {
                var lease = await scope.ServiceProvider.GetRequiredService<IZaloPayIntentRepository>()
                    .AcquireCreateAsync(fenced.Id, DateTimeOffset.UtcNow, null, CancellationToken.None);
                staleCreateToken = lease.Token ?? throw new InvalidOperationException("Create fence fixture could not claim its first lease.");
            }
            await ExpireLease(fenced.Id);
            await using (var scope = app.Services.CreateAsyncScope())
            {
                var repository = scope.ServiceProvider.GetRequiredService<IZaloPayIntentRepository>();
                var queryLease = await repository.AcquireQueryAsync(fenced.Id, DateTimeOffset.UtcNow, CancellationToken.None);
                var current = await repository.AcquireCreateAsync(fenced.Id, DateTimeOffset.UtcNow, queryLease.Token, CancellationToken.None);
                var currentToken = current.Token ?? throw new InvalidOperationException("Create fence fixture could not promote its recovery lease.");
                var stale = await repository.SaveCreateResultAsync(fenced.Id, staleCreateToken,
                    new("https://qcgateway.zalopay.vn/openinapp?order=fixture-stale", false, null), DateTimeOffset.UtcNow, CancellationToken.None);
                Check("A stale Create result cannot publish a URL or release the replacement lease", stale.PaymentUrl is null
                    && stale.QueryLeaseToken == currentToken && stale.QueryLeaseUntilUtc > DateTimeOffset.UtcNow);
                var winner = await repository.SaveCreateResultAsync(fenced.Id, currentToken,
                    new("https://qcgateway.zalopay.vn/openinapp?order=fixture-current", false, null), DateTimeOffset.UtcNow, CancellationToken.None);
                var lateRejection = await repository.SaveCreateResultAsync(fenced.Id, staleCreateToken,
                    new(null, true, "ProviderRejected"), DateTimeOffset.UtcNow, CancellationToken.None);
                Check("The current Create response commits its URL and clears its lease", winner.PaymentUrl is not null
                    && winner.QueryLeaseToken is null && winner.QueryLeaseUntilUtc is null);
                Check("A stale rejection cannot downgrade the winning checkout URL", lateRejection.Status == "Pending"
                    && lateRejection.PaymentUrl == "https://qcgateway.zalopay.vn/openinapp?order=fixture-current"
                    && lateRejection.FailureReason is null && !lateRejection.RequiresReconciliation);
            }
            await ParkRecoveryRows();

            var workerOrderId = await CreateOrder(owner, 23003);
            var workerOrderKey = Guid.NewGuid();
            var workerOrder = await SeedUnsent("Order", workerOrderId, null, workerOrderKey);
            gateway.Set(workerOrder.MerchantTransactionId, new("NotFound", null, null));
            before = gateway.CreateCount;
            await MakeDue(workerOrder.Id);
            await Reconcile();
            var orderResumed = await Read(workerOrder.Id);
            Check("Worker restores a checkout URL for an unsent AwaitingPayment Order", orderResumed.PaymentUrl is not null
                && gateway.CreateCount == before + 1);
            SameSnapshot("Worker recovery preserves the Order's source and original payment window", workerOrder, orderResumed);
            using (var response = await Post(own + "/orders/" + workerOrderId, token, new { }, workerOrderKey.ToString()))
                Check("Order retry reads the recovered bill rather than opening another", (await Body(response)).Id == workerOrder.Id
                    && gateway.CreateCount == before + 1);
            await ParkRecoveryRows();

            foreach (var invalidSource in new[] { "Cancelled", "ChangedAmount" })
            {
                var changedId = await CreateOrder(owner, 23007);
                var changedKey = Guid.NewGuid();
                var changed = await SeedUnsent("Order", changedId, null, changedKey);
                await using (var scope = app.Services.CreateAsyncScope())
                {
                    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                    var source = await db.Orders.SingleAsync(row => row.OrderId == changedId);
                    if (invalidSource == "Cancelled") source.Status = "Cancelled";
                    else source.TotalAmount++;
                    await db.SaveChangesAsync();
                }
                gateway.Set(changed.MerchantTransactionId, new("NotFound", null, null));
                before = gateway.CreateCount;
                using var response = await Post(own + "/orders/" + changedId, token, new { }, changedKey.ToString());
                var stopped = await Body(response);
                Check("Create recovery rechecks " + invalidSource + " Order before HTTP", gateway.CreateCount == before
                    && stopped.PaymentUrl is null && stopped.RequiresReconciliation && stopped.FailureReason == "CreateSourceChanged");
                SameSnapshot("Invalid source recovery preserves its original intent for reconciliation", changed, stopped);
                await ParkRecoveryRows();
            }

            var uncertainKey = Guid.NewGuid();
            gateway.UnknownNextCreate = 1;
            var uncertain = await CreateTopUp(23004, uncertainKey.ToString());
            recoveryIds.Add(uncertain.Id);
            var uncertainCreates = gateway.CreateCount;
            foreach (var observation in new[] { new GatewayQueryResult("Unavailable", null, "QueryUnavailable"), new GatewayQueryResult("Pending", null, null) })
            {
                gateway.Set(uncertain.MerchantTransactionId, observation);
                using var response = await Post(own + "/topups", token, new { amountVnd = 23004 }, uncertainKey.ToString());
                var held = await Body(response);
                SameSnapshot("An uncertain provider observation preserves the original bill", uncertain, held);
                Check("Pending or unavailable Query cannot authorize another Create", gateway.CreateCount == uncertainCreates
                    && held.PaymentUrl is null && held.VerifiedAtUtc is null && held.Status == "Pending");
            }
            gateway.Set(uncertain.MerchantTransactionId, new("NotFound", null, null));
            using (var response = await Post(own + "/topups", token, new { amountVnd = 23004 }, uncertainKey.ToString()))
            {
                var foundMissing = await Body(response);
                Check("Confirmed NotFound resumes an ambiguous Create with the same bill", foundMissing.PaymentUrl is not null
                    && gateway.CreateCount == uncertainCreates + 1);
                SameSnapshot("NotFound recovery never replaces merchant or extends expiry", uncertain, foundMissing);
            }
            await ParkRecoveryRows();

            ZaloPayIntentDto? callbackDuringCreate = null;
            var earlyProvider = ProviderId();
            gateway.OnCreate = async request =>
            {
                gateway.OnCreate = null;
                callbackDuringCreate = new(request.IntentId, request.CustomerId, request.Purpose,
                    request.OrderId ?? request.IntentId, request.AmountVnd, "Pending", null, request.MerchantTransactionId,
                    request.CreatedAtUtc, request.ExpiresAtUtc, null, null, null, false, null);
                gateway.Set(request.MerchantTransactionId, Paid(callbackDuringCreate, earlyProvider));
                using var response = await Post(callbackPath, null, Callback(callbackDuringCreate, earlyProvider));
                Check("An early callback defers safely while Create holds the shared lease", await Code(response) == 0);
            };
            var early = await CreateTopUp(23009);
            recoveryIds.Add(early.Id);
            Check("Create does not pretend that a deferred callback confirmed payment", early.Status == "Pending"
                && early.ProviderTransactionId is null && early.PaymentUrl is not null);
            using (var response = await Post(callbackPath, null, Callback(callbackDuringCreate!, earlyProvider)))
                Check("Provider retry records the deferred callback after Create releases its lease", await Code(response) == 1);
            Check("Deferred callback records one original provider receipt", (await Read(early.Id)).ProviderTransactionId == earlyProvider);
            await ParkRecoveryRows();

            var old = await SeedUnsent("TopUp", null, 23005, Guid.NewGuid(), DateTimeOffset.UtcNow.AddHours(-26));
            gateway.Set(old.MerchantTransactionId, new("NotFound", null, null));
            before = gateway.CreateCount;
            await MakeDue(old.Id);
            await using (var scope = app.Services.CreateAsyncScope())
                Check("A Pending intent after 26 hours downtime stays in the worker queue", (await scope.ServiceProvider
                    .GetRequiredService<IZaloPayIntentRepository>().GetDueIdsAsync(DateTimeOffset.UtcNow, 5, CancellationToken.None)).Contains(old.Id));
            var queriesBeforeDowntime = gateway.QueryCount;
            await Reconcile();
            var stillMissing = await Read(old.Id);
            Check("Worker queries an old unresolved bill without recreating an expired payment", gateway.QueryCount > queriesBeforeDowntime
                && gateway.CreateCount == before && stillMissing.Status == "Pending" && stillMissing.RequiresReconciliation
                && stillMissing.FailureReason == "CreateRecoveryExpired" && stillMissing.PaymentUrl is null);
            var oldReceipt = ProviderId();
            gateway.Set(old.MerchantTransactionId, Paid(old, oldReceipt));
            await MakeDue(old.Id);
            await Reconcile();
            var afterDowntime = await Read(old.Id);
            Check("Worker recovers a receipt lost during more than 24 hours downtime", afterDowntime.Status == "Succeeded"
                && afterDowntime.ProviderTransactionId == oldReceipt && afterDowntime.ProviderAmountVnd == old.ExpectedAmountVnd
                && afterDowntime.VerifiedAtUtc is not null && afterDowntime.RequiresReconciliation
                && afterDowntime.FailureReason == "LatePayment" && gateway.CreateCount == before);
            SameSnapshot("Late reconciliation preserves the original merchant and timestamps", old, afterDowntime);
            await ParkRecoveryRows();

            var callbackLost = await SeedUnsent("TopUp", null, 23008, Guid.NewGuid(), DateTimeOffset.UtcNow.AddHours(-26));
            var lostReceipt = ProviderId();
            gateway.Set(callbackLost.MerchantTransactionId, Paid(callbackLost, lostReceipt));
            before = gateway.CreateCount;
            await MakeDue(callbackLost.Id);
            await Reconcile();
            var paidAfterDowntime = await Read(callbackLost.Id);
            Check("An old Pending receipt is discovered on the first Query after 26 hours downtime", paidAfterDowntime.Status == "Succeeded"
                && paidAfterDowntime.ProviderTransactionId == lostReceipt && paidAfterDowntime.RequiresReconciliation
                && paidAfterDowntime.FailureReason == "LatePayment" && gateway.CreateCount == before);
            await ParkRecoveryRows();

            var rejectedRecoveryKey = Guid.NewGuid();
            gateway.UnknownNextCreate = 1;
            var rejectedRecovery = await CreateTopUp(23010, rejectedRecoveryKey.ToString());
            recoveryIds.Add(rejectedRecovery.Id);
            gateway.Set(rejectedRecovery.MerchantTransactionId, new("NotFound", null, null));
            gateway.RejectNextCreate = 1;
            before = gateway.CreateCount;
            using (var response = await Post(own + "/topups", token, new { amountVnd = 23010 }, rejectedRecoveryKey.ToString()))
            {
                var attempted = await Body(response);
                Check("A rejected recovery attempt cannot erase uncertainty from the original Create", attempted.Status == "Pending"
                    && attempted.RequiresReconciliation && attempted.FailureReason == "CreateRecoveryRejected"
                    && attempted.PaymentUrl is null && attempted.ProviderTransactionId is null && gateway.CreateCount == before + 1);
                SameSnapshot("Rejected recovery retains the original merchant for further Query", rejectedRecovery, attempted);
            }
            var recoveredReceipt = ProviderId();
            gateway.Set(rejectedRecovery.MerchantTransactionId, Paid(rejectedRecovery, recoveredReceipt));
            await MakeDue(rejectedRecovery.Id);
            await Reconcile();
            var finallyPaid = await Read(rejectedRecovery.Id);
            Check("Worker can still discover payment after a recovery Create was rejected", finallyPaid.Status == "Succeeded"
                && finallyPaid.ProviderTransactionId == recoveredReceipt && finallyPaid.ExpectedAmountVnd == 23010
                && gateway.CreateCount == before + 1);
            await ParkRecoveryRows();

            async Task<ZaloPayIntentDto> SeedUnsent(string purpose, int? orderId, decimal? amount, Guid requestKey, DateTimeOffset? now = null)
            {
                await using var scope = app.Services.CreateAsyncScope();
                var result = await scope.ServiceProvider.GetRequiredService<IZaloPayIntentRepository>()
                    .CreateAsync(owner, purpose, orderId, amount, requestKey, gateway.AppId, now ?? DateTimeOffset.UtcNow, CancellationToken.None);
                var row = result.Intent;
                Check("Crash fixture commits an intent before provider Create", result.Created && row.PaymentUrl is null
                    && row.ProviderTransactionId is null && row.Status == "Pending");
                recoveryIds.Add(row.IntentId);
                return new(row.IntentId, row.CustomerId, row.Purpose, row.OrderId ?? row.IntentId, row.ExpectedAmountVnd,
                    row.Status, row.PaymentUrl, row.MerchantTransactionId, row.CreatedAtUtc, row.ExpiresAtUtc,
                    row.ProviderTransactionId, row.ProviderAmountVnd, row.VerifiedAtUtc, row.RequiresReconciliation, row.FailureReason);
            }
            async Task ParkRecoveryRows()
            {
                await using var scope = app.Services.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                foreach (var row in await db.Set<ZaloPayIntent>().Where(row => recoveryIds.Contains(row.IntentId)).ToListAsync())
                    row.NextQueryAtUtc = DateTimeOffset.UtcNow.AddDays(1);
                await db.SaveChangesAsync();
            }
            void SameSnapshot(string name, ZaloPayIntentDto original, ZaloPayIntentDto observed) => Check(name,
                observed.Id == original.Id && observed.CustomerId == original.CustomerId && observed.Purpose == original.Purpose
                && observed.SourceId == original.SourceId && observed.MerchantTransactionId == original.MerchantTransactionId
                && observed.ExpectedAmountVnd == original.ExpectedAmountVnd && observed.CreatedAtUtc == original.CreatedAtUtc
                && observed.ExpiresAtUtc == original.ExpiresAtUtc);
        }

        async Task Reconcile()
        {
            await using var scope = app.Services.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<IZaloPayPaymentService>().ReconcileDueAsync(CancellationToken.None);
        }
        async Task MakeDue(int id)
        {
            await using var scope = app.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var row = await db.Set<ZaloPayIntent>().SingleAsync(row => row.IntentId == id);
            row.NextQueryAtUtc = DateTimeOffset.UtcNow.AddSeconds(-1);
            await db.SaveChangesAsync();
        }

        string ProviderId() => Interlocked.Increment(ref nextProviderId).ToString(CultureInfo.InvariantCulture);
        GatewayQueryResult Paid(ZaloPayIntentDto intent, string receipt) => new("Succeeded", new(gateway.AppId, receipt, intent.ExpectedAmountVnd, null), null);
        async Task<ZaloPayIntentDto> CreateTopUp(decimal amount, string? key = null)
        {
            using var response = await Post(own + "/topups", token, new { amountVnd = amount }, key ?? Guid.NewGuid().ToString());
            return await Body(response);
        }
        async Task<ZaloPayIntentDto> Sync(int id)
        {
            using var response = await Post(own + "/" + id + "/sync", token, new { });
            return await Body(response);
        }
        async Task<ZaloPayIntentDto> Read(int id)
        {
            using var response = await Get(own + "/" + id, token);
            return await Body(response);
        }
        static async Task<ZaloPayIntentDto> Body(HttpResponseMessage response)
        {
            if (response.StatusCode != HttpStatusCode.OK)
                throw new InvalidOperationException($"Gateway request returned {(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
            return await response.Content.ReadFromJsonAsync<ZaloPayIntentDto>() ?? throw new InvalidOperationException("Gateway response did not contain an intent.");
        }
        async Task<(int Id, string Token)> CreateCustomer(string phone, string name)
        {
            int id;
            await using (var scope = app.Services.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var row = new Customer { FullName = name, Phone = phone, Status = "Active", WalletBalance = 0 };
                row.PasswordHash = scope.ServiceProvider.GetRequiredService<ICustomerPasswordHasher>().HashPassword(row, password);
                db.Customers.Add(row); await db.SaveChangesAsync(); id = row.CustomerId;
            }
            using var response = await client.PostAsJsonAsync("/api/auth/customer/login", new { phone, password });
            var login = await response.Content.ReadFromJsonAsync<CustomerLoginResponse>() ?? throw new InvalidOperationException("Gateway fixture login failed.");
            return (id, login.AccessToken);
        }
        async Task SetCustomerStatus(int id, string status)
        {
            await using var scope = app.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            (await db.Customers.SingleAsync(row => row.CustomerId == id)).Status = status; await db.SaveChangesAsync();
        }
        async Task<int> CreateOrder(int customerId, decimal amount, string status = "AwaitingPayment")
        {
            await using var scope = app.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var employee = await db.Employees.Where(row => row.Username == "admin").Select(row => row.EmployeeId).SingleAsync();
            var row = new Order { CustomerId = customerId, EmployeeId = employee, TotalAmount = amount, Status = status };
            db.Orders.Add(row); await db.SaveChangesAsync(); return row.OrderId;
        }
        async Task ExpireLease(int id)
        {
            await using var scope = app.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var row = await db.Set<ZaloPayIntent>().SingleAsync(row => row.IntentId == id);
            row.QueryLeaseUntilUtc = DateTimeOffset.UtcNow.AddSeconds(-1); await db.SaveChangesAsync();
        }
        Task<HttpResponseMessage> Get(string path, string? bearer = null) => Request(HttpMethod.Get, path, bearer, null, null);
        Task<HttpResponseMessage> Post(string path, string? bearer, object? body, string? key = null) => Request(HttpMethod.Post, path, bearer, body, key);
        async Task<HttpResponseMessage> Request(HttpMethod method, string path, string? bearer, object? body, string? key)
        {
            using var request = new HttpRequestMessage(method, path);
            if (bearer is not null) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearer);
            if (key is not null) request.Headers.Add("Idempotency-Key", key);
            if (body is not null) request.Content = JsonContent.Create(body);
            return await client.SendAsync(request);
        }
        async Task Expect(string name, HttpStatusCode status, HttpResponseMessage response)
        {
            using (response)
            {
                var body = await response.Content.ReadAsStringAsync();
                if (response.StatusCode != status)
                    throw new InvalidOperationException($"{name}: expected {(int)status}, got {(int)response.StatusCode}: {body}");
                Check($"{name} ({(int)status})", true);
            }
        }
        void Check(string name, bool condition)
        {
            if (!condition) throw new InvalidOperationException(name);
            checks++; Console.WriteLine("PASS " + name);
        }
    }

    private sealed record CallbackBody(string data, string mac, int type = 1);
    private static CallbackBody Callback(ZaloPayIntentDto intent, string provider, decimal? amount = null,
        string? appUser = null, long? appTime = null, string? merchantId = null)
    {
        var data = JsonSerializer.Serialize(new
        {
            app_id = 2553, app_trans_id = merchantId ?? intent.MerchantTransactionId,
            app_user = appUser ?? "customer:" + intent.CustomerId.ToString(CultureInfo.InvariantCulture),
            app_time = appTime ?? intent.CreatedAtUtc.ToUnixTimeMilliseconds(), amount = amount ?? intent.ExpectedAmountVnd,
            zp_trans_id = long.Parse(provider, CultureInfo.InvariantCulture), server_time = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        });
        var mac = Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(GatewayChecksGateway.CallbackKey), Encoding.UTF8.GetBytes(data))).ToLowerInvariant();
        return new(data, mac);
    }
    private static string MerchantId(DateTimeOffset created) => created.ToOffset(TimeSpan.FromHours(7)).ToString("yyMMdd", CultureInfo.InvariantCulture) + "_" + Guid.NewGuid().ToString("N");
    private static async Task<int> Code(HttpResponseMessage response)
    {
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return body.RootElement.GetProperty("return_code").GetInt32();
    }
}
