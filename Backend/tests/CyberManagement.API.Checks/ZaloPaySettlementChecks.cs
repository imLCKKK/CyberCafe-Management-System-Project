using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CyberManagement.API.Controller;
using CyberManagement.Application.DTOs.Payments;
using CyberManagement.Application.DTOs.Realtime;
using CyberManagement.Application.Exceptions;
using CyberManagement.Application.Interfaces.Repositories;
using CyberManagement.Application.Interfaces.Services;
using CyberManagement.Domain.Entities;
using CyberManagement.Infrastructure.Persistance;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;

// SQL settlement checks use the real wallet writer, repositories, Order bridge and realtime publisher.
// Only provider HTTP is replaced by GatewayChecksGateway. These receipts are internal fixtures.
internal static class ZaloPaySettlementChecks
{
    public static async Task<int> RunAsync(WebApplication app, HttpClient client,
        IReadOnlyDictionary<string, string> staffTokens, string customerToken, RealtimeSaveFailure saveFailure)
    {
        await using (var scope = app.Services.CreateAsyncScope())
            if (!scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Database.IsSqlServer())
            {
                Console.WriteLine("SKIP ZaloPay settlement checks require SQL Server transactions and unique constraints.");
                return 0;
            }

        const string own = "/api/customers/me/payments/zalopay";
        const string callbackPath = "/api/payments/zalopay/callback";
        const string password = "Settlement-check-customer-2026!";
        var gateway = app.Services.GetRequiredService<GatewayChecksGateway>();
        var checks = 0;
        var nextProviderId = 261117230000000L;
        var (ownerId, ownerToken) = await CreateCustomer("0782300001", "Settlement fixture owner");
        var (otherId, otherToken) = await CreateCustomer("0782300002", "Settlement other owner");
        var (lockedId, lockedToken) = await CreateCustomer("0782300003", "Settlement locked owner");

        await RunTopUpAndRealtime();
        await RunReceiptRecovery();
        await RunRejectedTopUps();
        await RunOrderBridge();
        await RunOrderReviewCases();
        await using (var scope = app.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var migrations = (await db.Database.GetAppliedMigrationsAsync()).ToArray();
            var refused = false;
            try { await db.GetService<IMigrator>().MigrateAsync(migrations[^2]); }
            catch (SqlException error) when (error.Number == 51023) { refused = true; }
            Check("Downgrade refuses to discard applied receipts and checkout bindings", refused
                && (await db.Database.GetAppliedMigrationsAsync()).SequenceEqual(migrations)
                && await db.Set<ZaloPayIntent>().AnyAsync(row => row.AppliedAtUtc != null));
        }
        return checks;

        async Task RunTopUpAndRealtime()
        {
            var intent = await CreateTopUp(ownerToken, 50000);
            var provider = ProviderId();
            var callback = Callback(intent, provider);
            await using var customerPeer = await RealtimeTestPeer.ConnectAsync(app.GetTestServer(), ownerToken);
            await using var otherPeer = await RealtimeTestPeer.ConnectAsync(app.GetTestServer(), otherToken);
            await using var adminPeer = await RealtimeTestPeer.ConnectAsync(app.GetTestServer(), staffTokens["Admin"]);
            await using var technicianPeer = await RealtimeTestPeer.ConnectAsync(app.GetTestServer(), staffTokens["Technician"]);
            using (var response = await Post(callbackPath, null, callback with { mac = new string('0', 64) }))
                Check("Invalid MAC never authorizes a wallet credit", await CallbackCode(response) == 2);
            await AssertNoEffect(intent, "A checkout URL and rejected MAC leave the wallet unchanged");
            await Barrier(customerPeer, RealtimeAccountType.Customer, ownerId, "Rejected MAC emits no owning-customer event");

            gateway.Set(intent.MerchantTransactionId, Paid(intent, provider));
            var payment = Post(callbackPath, null, callback);
            var walletEvent = await customerPeer.NextAsync();
            Check("TopUp invalidates its owning customer's wallet", Event(walletEvent, "WalletChanged").GetProperty("entityId").GetInt32() == ownerId);
            await AssertTopUp(intent, 50000, "The wallet event exposes an already committed SQL balance and ledger");
            Check("TopUp notifies staff with wallet.view", Event(await adminPeer.NextAsync(), "WalletChanged").GetProperty("entityId").GetInt32() == ownerId);
            Check("Applied TopUp also invalidates its owning payment state", Event(await customerPeer.NextAsync(), "ZaloPayPaymentChanged")
                .GetProperty("entityId").GetInt32() == intent.Id);
            using (var response = await payment)
                Check("The valid callback acknowledges its committed wallet settlement", await CallbackCode(response) == 1);
            await Barrier(otherPeer, RealtimeAccountType.Customer, otherId, "TopUp sends no wallet event to another customer");
            await Barrier(technicianPeer, RealtimeAccountType.Staff, TokenId(staffTokens["Technician"]), "Staff without wallet.view receives no TopUp event");

            var original = await Read(intent.Id, ownerToken);
            Check("Owned API returns durable applied timestamp and wallet ledger ID", original.AppliedAtUtc is not null
                && original.WalletTransactionId is not null && original.PaymentId is null && original.SettlementFailureReason is null);
            var queries = gateway.QueryCount;
            var replays = await Task.WhenAll(Post(callbackPath, null, callback),
                Post(own + "/" + intent.Id + "/sync", ownerToken, new { }), Post(callbackPath, null, callback));
            try
            {
                Check("Concurrent callback and Query replays are handled", replays.All(response => response.StatusCode == HttpStatusCode.OK));
                Check("Repeated callbacks acknowledge the same durable credit", await CallbackCode(replays[0]) == 1 && await CallbackCode(replays[2]) == 1);
            }
            finally { foreach (var response in replays) response.Dispose(); }
            var replayed = await Read(intent.Id, ownerToken);
            Check("Applied replay keeps confirmation, applied time and ledger identity", replayed.VerifiedAtUtc == original.VerifiedAtUtc
                && replayed.AppliedAtUtc == original.AppliedAtUtc && replayed.WalletTransactionId == original.WalletTransactionId
                && gateway.QueryCount == queries);
            await AssertTopUp(intent, 50000, "Callback/Query races credit the wallet exactly once");
            await Barrier(customerPeer, RealtimeAccountType.Customer, ownerId, "Applied replay sends no duplicate customer event");
            await Barrier(adminPeer, RealtimeAccountType.Staff, TokenId(staffTokens["Admin"]), "Applied replay sends no duplicate staff event");
        }

        async Task RunReceiptRecovery()
        {
            var intent = await CreateTopUp(ownerToken, 20000);
            await StageReceipt(intent);
            await using var peer = await RealtimeTestPeer.ConnectAsync(app.GetTestServer(), ownerToken);
            await using var staff = await RealtimeTestPeer.ConnectAsync(app.GetTestServer(), staffTokens["Admin"]);
            Interlocked.Exchange(ref saveFailure.FailNextSave, 1);
            var failed = false;
            try { _ = await Apply(intent.Id); }
            catch (InvalidOperationException error) when (error.Message.Contains("Injected save failure", StringComparison.Ordinal)) { failed = true; }
            Check("Injected failure reaches the settlement SaveChanges after the receipt committed", failed && saveFailure.FailNextSave == 0);
            var saved = await Read(intent.Id, ownerToken);
            Check("Settlement rollback preserves independently committed provider proof", saved.Status == "Succeeded" && saved.VerifiedAtUtc is not null
                && saved.ProviderTransactionId is not null && saved.AppliedAtUtc is null && saved.WalletTransactionId is null);
            await AssertNoEffect(intent, "Settlement rollback writes neither the wallet ledger nor applied marker");
            await Barrier(peer, RealtimeAccountType.Customer, ownerId, "Rolled-back settlement emits no customer event");
            await Barrier(staff, RealtimeAccountType.Staff, TokenId(staffTokens["Admin"]), "Rolled-back settlement emits no staff event");

            var queries = gateway.QueryCount;
            gateway.Enabled = false;
            try
            {
                var recovered = await Sync(intent.Id, ownerToken);
                Check("Verified receipt recovers while new provider calls are disabled", recovered.AppliedAtUtc is not null
                    && gateway.QueryCount == queries && recovered.ProviderTransactionId == saved.ProviderTransactionId);
            }
            finally { gateway.Enabled = true; }
            await AssertTopUp(intent, 70000, "A retry after failed settlement credits only its original amount once");
            _ = Event(await peer.NextAsync(), "WalletChanged");
            _ = Event(await peer.NextAsync(), "ZaloPayPaymentChanged");
            _ = Event(await staff.NextAsync(), "WalletChanged");

            var recovery = await CreateTopUp(ownerToken, 30000);
            await StageReceipt(recovery);
            gateway.Enabled = false;
            queries = gateway.QueryCount;
            try
            {
                await using var scope = app.Services.CreateAsyncScope();
                await scope.ServiceProvider.GetRequiredService<IZaloPaySettlementService>().RecoverDueAsync(CancellationToken.None);
            }
            finally { gateway.Enabled = true; }
            Check("Recovery worker applies durable succeeded receipts without provider HTTP", (await Read(recovery.Id, ownerToken)).AppliedAtUtc is not null
                && gateway.QueryCount == queries);
            await AssertTopUp(recovery, 100000, "Worker recovery commits one matching wallet ledger");

            var callbackRecovery = await CreateTopUp(ownerToken, 10000);
            var callbackProvider = await StageReceipt(callbackRecovery);
            using (var response = await Post(callbackPath, null, Callback(callbackRecovery, callbackProvider)))
                Check("Callback replay resumes a succeeded receipt that has not yet been applied", await CallbackCode(response) == 1);
            await AssertTopUp(callbackRecovery, 110000, "Callback recovery uses the same exactly-once SQL consumer");

            var expiredQr = await CreateTopUp(otherToken, 12345);
            await StageReceipt(expiredQr);
            await using (var scope = app.Services.CreateAsyncScope())
            {
                var recovered = await scope.ServiceProvider.GetRequiredService<IZaloPaySettlementRepository>()
                    .ApplyAsync(expiredQr.Id, expiredQr.ExpiresAtUtc.AddMinutes(1), CancellationToken.None);
                Check("Recovery after QR expiry honors an on-time durable confirmation", recovered.Applied
                    && recovered.Intent.AppliedAtUtc > expiredQr.ExpiresAtUtc && !recovered.Intent.RequiresReconciliation);
            }
            await AssertTopUp(expiredQr, 12345, "Delayed recovery credits the on-time receipt exactly once");
        }

        async Task RunRejectedTopUps()
        {
            var mismatch = await CreateTopUp(ownerToken, 10001);
            await StageReceipt(mismatch, amount: 10002);
            _ = await Apply(mismatch.Id);
            await AssertNoEffect(mismatch, "A provider amount mismatch cannot credit a TopUp");
            Check("Amount mismatch remains available for manual reconciliation", (await Read(mismatch.Id, ownerToken)).RequiresReconciliation);

            var locked = await CreateTopUp(lockedToken, 10003);
            await StageReceipt(locked);
            await SetCustomerStatus(lockedId, "Locked");
            var lockedResult = await Apply(locked.Id);
            Check("Settlement rechecks the customer after receipt verification", !lockedResult.Applied && lockedResult.Intent.RequiresReconciliation
                && lockedResult.Intent.SettlementFailureReason == "CustomerLocked");
            await AssertNoEffect(locked, "A locked customer keeps the receipt without a wallet credit");

            var late = await CreateTopUp(ownerToken, 10004);
            await StageReceipt(late);
            await using (var scope = app.Services.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var stored = await db.Set<ZaloPayIntent>().SingleAsync(row => row.IntentId == late.Id);
                stored.ExpiresAtUtc = stored.VerifiedAtUtc!.Value.AddTicks(-1);
                stored.CreatedAtUtc = stored.ExpiresAtUtc.AddMinutes(-15);
                await db.SaveChangesAsync();
            }
            var lateResult = await Apply(late.Id);
            Check("Late verified funds retain proof for review", !lateResult.Applied && lateResult.Intent.RequiresReconciliation
                && lateResult.Intent.SettlementFailureReason == "LatePayment");
            await AssertNoEffect(late, "Expired eligibility cannot be restored by a late receipt");

            foreach (var conflict in new[] { "merchant", "provider" })
            {
                var intent = await CreateTopUp(ownerToken, 10005);
                var provider = await StageReceipt(intent);
                await using (var scope = app.Services.CreateAsyncScope())
                {
                    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                    var order = await NewOrder(db, ownerId, 10005, "Pending");
                    var now = DateTimeOffset.UtcNow;
                    db.Payments.Add(new Payment
                    {
                        OrderId = order.OrderId, Amount = 10005, Method = "ZaloPay", Status = "Succeeded", Type = "Payment",
                        OperationKey = "SettlementLegacy:" + Guid.NewGuid().ToString("N"),
                        MerchantTransactionId = conflict == "merchant" ? intent.MerchantTransactionId : "legacy-" + Guid.NewGuid().ToString("N"),
                        GatewayTransactionId = conflict == "provider" ? provider : ProviderId(),
                        PaymentTime = now, ConfirmedAt = now, PaymentTimeBasis = "Confirmation"
                    });
                    await db.SaveChangesAsync();
                }
                var result = await Apply(intent.Id);
                Check("A legacy Payment " + conflict + " reference cannot be claimed by a TopUp", !result.Applied
                    && result.Intent.RequiresReconciliation && result.Intent.SettlementFailureReason == "GatewayTransactionConflict");
                await AssertNoEffect(intent, "Conflicting legacy " + conflict + " reference creates no wallet credit");
            }
        }

        async Task RunOrderBridge()
        {
            var fixture = await CreateOrderFixture(45000);
            var intent = await CreateOrderIntent(fixture.OrderId);
            await using (var scope = app.Services.CreateAsyncScope())
            {
                var bridge = scope.ServiceProvider.GetRequiredService<IZaloPayOrderPaymentBridge>();
                await ExpectException<InvalidOperationException>("Checkout registration requires its caller's SQL transaction",
                    () => bridge.RegisterAsync(ownerId, fixture.OrderId, intent.Id, intent.ExpiresAtUtc.AddSeconds(-1), CancellationToken.None));
                await ExpectException<InvalidOperationException>("Order confirmation requires its caller's SQL transaction",
                    () => bridge.ConfirmAsync(intent.Id, DateTimeOffset.UtcNow, CancellationToken.None));
            }
            var deadline = intent.ExpiresAtUtc.AddSeconds(-1);
            await using (var scope = app.Services.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var bridge = scope.ServiceProvider.GetRequiredService<IZaloPayOrderPaymentBridge>();
                await using var transaction = await db.Database.BeginTransactionAsync();
                Interlocked.Exchange(ref saveFailure.FailNextSave, 1);
                try
                {
                    var payment = await bridge.RegisterAsync(ownerId, fixture.OrderId, intent.Id, deadline, CancellationToken.None);
                    Check("Registration stages a canonical Payment without saving or committing the checkout transaction", saveFailure.FailNextSave == 1
                        && db.Database.CurrentTransaction == transaction && db.Entry(payment).State == EntityState.Added
                        && payment.Status == "Pending" && payment.Method == "ZaloPay" && payment.Amount == 45000
                        && payment.ZaloPayIntentId == intent.Id && payment.GatewayReservationExpiresAtUtc == deadline);
                }
                finally { Interlocked.Exchange(ref saveFailure.FailNextSave, 0); }
                await db.SaveChangesAsync();
                await transaction.RollbackAsync();
            }
            await using (var scope = app.Services.CreateAsyncScope())
                Check("Checkout owner can roll back registration without a Payment leak", !await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>()
                    .Payments.AnyAsync(row => row.ZaloPayIntentId == intent.Id));
            var paymentId = await Register(intent, fixture.OrderId, deadline);
            var replayedPaymentId = await Register(intent, fixture.OrderId, deadline);
            Check("Registration replay reuses the original Payment binding", replayedPaymentId == paymentId);
            await using (var scope = app.Services.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var bridge = scope.ServiceProvider.GetRequiredService<IZaloPayOrderPaymentBridge>();
                await using var transaction = await db.Database.BeginTransactionAsync();
                await ExpectException<BusinessRuleException>("Registration cannot change the saved reservation deadline", () =>
                    bridge.RegisterAsync(ownerId, fixture.OrderId, intent.Id, deadline.AddSeconds(-1), CancellationToken.None));
                await ExpectException<BusinessRuleException>("Registration cannot bind another customer's checkout", () =>
                    bridge.RegisterAsync(otherId, fixture.OrderId, intent.Id, deadline, CancellationToken.None));
            }

            var provider = await StageReceipt(intent);
            await using (var scope = app.Services.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var bridge = scope.ServiceProvider.GetRequiredService<IZaloPayOrderPaymentBridge>();
                await using var transaction = await db.Database.BeginTransactionAsync();
                Interlocked.Exchange(ref saveFailure.FailNextSave, 1);
                try
                {
                    var staged = await bridge.ConfirmAsync(intent.Id, DateTimeOffset.UtcNow, CancellationToken.None);
                    Check("Order confirmation stages all changes without an ambient save or commit", staged.Applied
                        && saveFailure.FailNextSave == 1 && db.Database.CurrentTransaction == transaction
                        && staged.Intent.AppliedAtUtc is not null && db.Payments.Local.Single(row => row.PaymentId == paymentId).Status == "Succeeded");
                }
                finally { Interlocked.Exchange(ref saveFailure.FailNextSave, 0); }
                await db.SaveChangesAsync();
                await transaction.RollbackAsync();
            }
            await using (var scope = app.Services.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                Check("Caller rollback restores Order, Payment and applied marker together", await db.Orders.Where(row => row.OrderId == fixture.OrderId)
                    .Select(row => row.Status).SingleAsync() == "AwaitingPayment"
                    && await db.Payments.Where(row => row.PaymentId == paymentId).Select(row => row.Status).SingleAsync() == "Pending"
                    && await db.Set<ZaloPayIntent>().Where(row => row.IntentId == intent.Id).Select(row => row.AppliedAtUtc).SingleAsync() is null);
            }

            await using var customerPeer = await RealtimeTestPeer.ConnectAsync(app.GetTestServer(), ownerToken);
            await using var adminPeer = await RealtimeTestPeer.ConnectAsync(app.GetTestServer(), staffTokens["Admin"]);
            await using var otherPeer = await RealtimeTestPeer.ConnectAsync(app.GetTestServer(), otherToken);
            await using var technicianPeer = await RealtimeTestPeer.ConnectAsync(app.GetTestServer(), staffTokens["Technician"]);
            var settling = Post(own + "/" + intent.Id + "/sync", ownerToken, new { });
            Check("Order settlement notifies its owning customer", Event(await customerPeer.NextAsync(), "OrderChanged")
                .GetProperty("entityId").GetInt32() == fixture.OrderId);
            await AssertOrder(fixture, intent, paymentId, "The Order event exposes an already committed canonical Payment");
            _ = Event(await customerPeer.NextAsync(), "ZaloPayPaymentChanged");
            Check("Order settlement notifies staff with order.view", Event(await adminPeer.NextAsync(), "OrderChanged")
                .GetProperty("entityId").GetInt32() == fixture.OrderId);
            using (var response = await settling)
            {
                var saved = await Body(response);
                Check("API returns the canonical Payment marker after Order settlement", saved.PaymentId == paymentId
                    && saved.AppliedAtUtc is not null && saved.WalletTransactionId is null);
            }
            await Barrier(otherPeer, RealtimeAccountType.Customer, otherId, "Another customer receives no Order payment event");
            await Barrier(technicianPeer, RealtimeAccountType.Staff, TokenId(staffTokens["Technician"]), "Staff without order.view receives no Order payment event");
            var responses = await Task.WhenAll(Post(callbackPath, null, Callback(intent, provider)),
                Post(own + "/" + intent.Id + "/sync", ownerToken, new { }), Post(callbackPath, null, Callback(intent, provider)));
            try { Check("Order callback/Query races remain handled", responses.All(row => row.StatusCode == HttpStatusCode.OK)); }
            finally { foreach (var response in responses) response.Dispose(); }
            await AssertOrder(fixture, intent, paymentId, "Repeated Order callbacks never add a Payment or change reserved stock/voucher usage");
            await Barrier(customerPeer, RealtimeAccountType.Customer, ownerId, "Order replay emits no duplicate owner event");
            await Barrier(adminPeer, RealtimeAccountType.Staff, TokenId(staffTokens["Admin"]), "Order replay emits no duplicate staff event");
        }

        async Task RunOrderReviewCases()
        {
            var missing = await CreateOrderFixture(25000);
            var intent = await CreateOrderIntent(missing.OrderId);
            await StageReceipt(intent);
            var held = await Apply(intent.Id);
            Check("Verified Order money waits for explicit checkout registration", !held.Applied && held.Intent.AppliedAtUtc is null
                && held.Intent.SettlementFailureReason == "CheckoutRegistrationMissing" && !held.Intent.RequiresReconciliation);
            await using (var scope = app.Services.CreateAsyncScope())
                Check("Unregistered receipt does not invent a Payment", !await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>()
                    .Payments.AnyAsync(row => row.OrderId == missing.OrderId));
            var paymentId = await Register(intent, missing.OrderId, intent.ExpiresAtUtc.AddSeconds(-1));
            var recovered = await Apply(intent.Id);
            Check("Late checkout registration can recover the same verified receipt", recovered.Applied && recovered.Intent.PaymentId == paymentId);
            await AssertOrder(missing, intent, paymentId, "Recovered registration retains its original amount and receipt");

            foreach (var state in new[] { "Cancelled", "Expired", "ChangedAmount", "OtherCashPayment", "ReservationExpired" })
            {
                var fixture = await CreateOrderFixture(26000);
                var pending = await CreateOrderIntent(fixture.OrderId);
                _ = await Register(pending, fixture.OrderId, pending.ExpiresAtUtc.AddSeconds(-1));
                await StageReceipt(pending);
                await using (var scope = app.Services.CreateAsyncScope())
                {
                    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                    var order = await db.Orders.SingleAsync(row => row.OrderId == fixture.OrderId);
                    if (state is "Cancelled" or "Expired") order.Status = state;
                    if (state == "ChangedAmount") order.TotalAmount++;
                    if (state == "ReservationExpired")
                        (await db.Payments.SingleAsync(row => row.ZaloPayIntentId == pending.Id)).GatewayReservationExpiresAtUtc = DateTimeOffset.UtcNow.AddSeconds(-1);
                    if (state == "OtherCashPayment")
                    {
                        var now = DateTimeOffset.UtcNow;
                        db.Payments.Add(new Payment { OrderId = fixture.OrderId, Amount = 26000, Method = "Cash", Status = "Succeeded",
                            OperationKey = "SettlementCash:" + Guid.NewGuid().ToString("N"), PaymentTime = now, ConfirmedAt = now, PaymentTimeBasis = "Confirmation" });
                    }
                    await db.SaveChangesAsync();
                }
                var result = await Apply(pending.Id);
                Check(state + " prevents automatic Order payment confirmation", !result.Applied && result.Intent.AppliedAtUtc is null
                    && result.Intent.RequiresReconciliation && result.Intent.SettlementFailureReason is not null);
                await using var verify = app.Services.CreateAsyncScope();
                var stored = verify.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                Check(state + " leaves the gateway Payment Pending and food reservation untouched", await stored.Payments.Where(row => row.ZaloPayIntentId == pending.Id)
                    .Select(row => row.Status).SingleAsync() == "Pending"
                    && await stored.Products.Where(row => row.ProductId == fixture.ProductId).Select(row => row.Stock).SingleAsync() == 17
                    && await stored.PromotionUsages.CountAsync(row => row.OrderId == fixture.OrderId) == 1
                    && await stored.Orders.Where(row => row.OrderId == fixture.OrderId).Select(row => row.Status).SingleAsync()
                        == (state is "Cancelled" or "Expired" ? state : "AwaitingPayment"));
            }
        }

        async Task AssertTopUp(ZaloPayIntentDto intent, decimal expectedBalance, string name)
        {
            await using var scope = app.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var saved = await db.Set<ZaloPayIntent>().AsNoTracking().SingleAsync(row => row.IntentId == intent.Id);
            var entries = await db.WalletTransactions.AsNoTracking().Where(row => row.CustomerId == intent.CustomerId
                && row.SourceType == "ZaloPayTopUp" && row.SourceId == intent.MerchantTransactionId).ToListAsync();
            Check(name, saved.Status == "Succeeded" && saved.AppliedAtUtc is not null && saved.PaymentId is null && entries.Count == 1
                && entries[0].TransactionId == saved.WalletTransactionId && entries[0].Amount == intent.ExpectedAmountVnd
                && entries[0].Kind == "Credit" && entries[0].Type == "TopUp" && entries[0].ActorType == "System"
                && entries[0].OperationKey == "ZaloPayTopUp:" + intent.MerchantTransactionId
                && await db.Customers.Where(row => row.CustomerId == intent.CustomerId).Select(row => row.WalletBalance).SingleAsync() == expectedBalance
                && await db.WalletTransactions.Where(row => row.CustomerId == intent.CustomerId).SumAsync(row => row.Amount) == expectedBalance
                && !await db.Payments.AnyAsync(row => row.ZaloPayIntentId == intent.Id || row.MerchantTransactionId == intent.MerchantTransactionId));
        }

        async Task AssertNoEffect(ZaloPayIntentDto intent, string name)
        {
            await using var scope = app.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var saved = await db.Set<ZaloPayIntent>().AsNoTracking().SingleAsync(row => row.IntentId == intent.Id);
            Check(name, saved.AppliedAtUtc is null && saved.WalletTransactionId is null && saved.PaymentId is null
                && !await db.WalletTransactions.AnyAsync(row => row.SourceType == "ZaloPayTopUp" && row.SourceId == intent.MerchantTransactionId));
        }

        async Task AssertOrder(OrderFixture fixture, ZaloPayIntentDto intent, int paymentId, string name)
        {
            await using var scope = app.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var receipt = await db.Set<ZaloPayIntent>().AsNoTracking().SingleAsync(row => row.IntentId == intent.Id);
            var payment = await db.Payments.AsNoTracking().SingleAsync(row => row.PaymentId == paymentId);
            Check(name, receipt.AppliedAtUtc is not null && receipt.PaymentId == paymentId && receipt.WalletTransactionId is null
                && payment.ZaloPayIntentId == intent.Id && payment.Amount == intent.ExpectedAmountVnd && payment.Status == "Succeeded"
                && payment.Method == "ZaloPay" && payment.GatewayTransactionId == receipt.ProviderTransactionId
                && payment.PaymentTime == receipt.VerifiedAtUtc && payment.ConfirmedAt == receipt.VerifiedAtUtc
                && payment.PaymentTimeBasis == "Confirmation" && payment.ProviderOccurredAt is null
                && await db.Payments.CountAsync(row => row.OrderId == fixture.OrderId) == 1
                && await db.Orders.Where(row => row.OrderId == fixture.OrderId).Select(row => row.Status).SingleAsync() == "Pending"
                && await db.OrderDetails.Where(row => row.OrderId == fixture.OrderId).Select(row => row.Status).SingleAsync() == "Pending"
                && await db.Products.Where(row => row.ProductId == fixture.ProductId).Select(row => row.Stock).SingleAsync() == 17
                && await db.PromotionUsages.CountAsync(row => row.OrderId == fixture.OrderId && row.ReleasedAt == null) == 1);
        }

        async Task<string> StageReceipt(ZaloPayIntentDto intent, decimal? amount = null)
        {
            var provider = ProviderId();
            await using var scope = app.Services.CreateAsyncScope();
            var repo = scope.ServiceProvider.GetRequiredService<IZaloPayIntentRepository>();
            var now = DateTimeOffset.UtcNow;
            var lease = await repo.AcquireQueryAsync(intent.Id, now, CancellationToken.None);
            if (!lease.Acquired || lease.Token is null) throw new InvalidOperationException("Receipt fixture could not obtain its real SQL Query lease.");
            _ = await repo.RecordReceiptAsync(intent.Id, lease.Token.Value, new(gateway.AppId, provider, amount ?? intent.ExpectedAmountVnd, null), now, CancellationToken.None);
            return provider;
        }

        async Task<ZaloPaySettlementResult> Apply(int id)
        {
            await using var scope = app.Services.CreateAsyncScope();
            return await scope.ServiceProvider.GetRequiredService<IZaloPaySettlementService>().ApplyAsync(id, CancellationToken.None);
        }

        async Task<int> Register(ZaloPayIntentDto intent, int orderId, DateTimeOffset expiry)
        {
            await using var scope = app.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            await using var transaction = await db.Database.BeginTransactionAsync();
            var payment = await scope.ServiceProvider.GetRequiredService<IZaloPayOrderPaymentBridge>()
                .RegisterAsync(ownerId, orderId, intent.Id, expiry, CancellationToken.None);
            await db.SaveChangesAsync();
            await transaction.CommitAsync();
            return payment.PaymentId;
        }

        async Task<OrderFixture> CreateOrderFixture(decimal amount)
        {
            await using var scope = app.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var order = await NewOrder(db, ownerId, amount, "AwaitingPayment");
            var product = new Product { ProductName = "Reserved settlement dish " + order.OrderId, Price = amount + 5000, Stock = 17,
                Category = new Category { CategoryName = "Settlement checks " + order.OrderId } };
            order.VoucherCodeSnapshot = "SETTLEMENT";
            order.VoucherDiscountTypeSnapshot = "Fixed";
            order.VoucherDiscountValueSnapshot = 5000;
            order.DiscountAmount = 5000;
            db.OrderDetails.Add(new OrderDetail { OrderId = order.OrderId, Product = product, ProductNameSnapshot = product.ProductName,
                Quantity = 1, UnitPrice = amount + 5000, DiscountAmount = 5000, Status = "Pending" });
            db.PromotionUsages.Add(new PromotionUsage { OrderId = order.OrderId, CustomerId = ownerId,
                Promotion = new Promotion { Code = "SETTLEMENT" + order.OrderId, PromoName = "Reserved settlement voucher", DiscountType = "Fixed",
                    DiscountValue = 5000, StartDate = DateTimeOffset.UtcNow.AddDays(-1), EndDate = DateTimeOffset.UtcNow.AddDays(1) } });
            await db.SaveChangesAsync();
            return new(order.OrderId, product.ProductId);
        }

        async Task<Order> NewOrder(ApplicationDbContext db, int customerId, decimal amount, string status)
        {
            var employee = await db.Employees.Where(row => row.Username == "admin").Select(row => row.EmployeeId).SingleAsync();
            var order = new Order { CustomerId = customerId, EmployeeId = employee, TotalAmount = amount, Status = status };
            db.Orders.Add(order);
            await db.SaveChangesAsync();
            return order;
        }

        async Task<ZaloPayIntentDto> CreateTopUp(string token, decimal amount)
        {
            using var response = await Post(own + "/topups", token, new { amountVnd = amount }, Guid.NewGuid().ToString());
            return await Body(response);
        }
        async Task<ZaloPayIntentDto> CreateOrderIntent(int id)
        {
            using var response = await Post(own + "/orders/" + id, ownerToken, new { }, Guid.NewGuid().ToString());
            return await Body(response);
        }
        async Task<ZaloPayIntentDto> Sync(int id, string token)
        {
            using var response = await Post(own + "/" + id + "/sync", token, new { });
            return await Body(response);
        }
        async Task<ZaloPayIntentDto> Read(int id, string token)
        {
            using var response = await Request(HttpMethod.Get, own + "/" + id, token, null, null);
            return await Body(response);
        }
        async Task<(int Id, string Token)> CreateCustomer(string phone, string name)
        {
            int id;
            await using (var scope = app.Services.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var row = new Customer { FullName = name, Phone = phone, Status = "Active", WalletBalance = 0 };
                row.PasswordHash = scope.ServiceProvider.GetRequiredService<ICustomerPasswordHasher>().HashPassword(row, password);
                db.Customers.Add(row);
                await db.SaveChangesAsync();
                id = row.CustomerId;
            }
            using var response = await client.PostAsJsonAsync("/api/auth/customer/login", new { phone, password });
            if (response.StatusCode != HttpStatusCode.OK) throw new InvalidOperationException("Settlement fixture login failed.");
            var login = await response.Content.ReadFromJsonAsync<CustomerLoginResponse>() ?? throw new InvalidOperationException("Settlement fixture JWT missing.");
            return (id, login.AccessToken);
        }
        async Task SetCustomerStatus(int id, string status)
        {
            await using var scope = app.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            (await db.Customers.SingleAsync(row => row.CustomerId == id)).Status = status;
            await db.SaveChangesAsync();
        }
        async Task Barrier(RealtimeTestPeer peer, RealtimeAccountType type, int accountId, string name)
        {
            await using var scope = app.Services.CreateAsyncScope();
            var marker = new RealtimeEvent(Guid.NewGuid(), "SettlementCheckBarrier", 1, DateTimeOffset.UtcNow);
            await scope.ServiceProvider.GetRequiredService<IRealtimePublisher>().PublishToAccountAsync(type, accountId, marker, CancellationToken.None);
            Check(name, Event(await peer.NextAsync(), marker.Type).GetProperty("eventId").GetGuid() == marker.EventId);
        }
        async Task ExpectException<T>(string name, Func<Task> action) where T : Exception
        {
            var thrown = false;
            try { await action(); }
            catch (T) { thrown = true; }
            Check(name, thrown);
        }
        Task<HttpResponseMessage> Post(string path, string? token, object body, string? key = null) => Request(HttpMethod.Post, path, token, body, key);
        async Task<HttpResponseMessage> Request(HttpMethod method, string path, string? token, object? body, string? key)
        {
            using var request = new HttpRequestMessage(method, path);
            if (token is not null) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            if (body is not null) request.Content = JsonContent.Create(body);
            if (key is not null) request.Headers.Add("Idempotency-Key", key);
            return await client.SendAsync(request);
        }
        string ProviderId() => Interlocked.Increment(ref nextProviderId).ToString(CultureInfo.InvariantCulture);
        GatewayQueryResult Paid(ZaloPayIntentDto intent, string provider) => new("Succeeded", new(gateway.AppId, provider, intent.ExpectedAmountVnd, null), null);
        void Check(string name, bool condition)
        {
            if (!condition) throw new InvalidOperationException(name);
            checks++;
            Console.WriteLine("PASS " + name);
        }
    }

    private sealed record OrderFixture(int OrderId, int ProductId);
    private sealed record CallbackBody(string data, string mac, int type = 1);
    private static CallbackBody Callback(ZaloPayIntentDto intent, string provider)
    {
        var data = JsonSerializer.Serialize(new
        {
            app_id = 2553, app_trans_id = intent.MerchantTransactionId,
            app_user = "customer:" + intent.CustomerId.ToString(CultureInfo.InvariantCulture),
            app_time = intent.CreatedAtUtc.ToUnixTimeMilliseconds(), amount = intent.ExpectedAmountVnd,
            zp_trans_id = long.Parse(provider, CultureInfo.InvariantCulture), server_time = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        });
        var mac = Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(GatewayChecksGateway.CallbackKey), Encoding.UTF8.GetBytes(data))).ToLowerInvariant();
        return new(data, mac);
    }
    private static int TokenId(string token) => int.Parse(new JwtSecurityTokenHandler().ReadJwtToken(token).Subject, CultureInfo.InvariantCulture);
    private static JsonElement Event(JsonElement frame, string target)
    {
        if (frame.GetProperty("type").GetInt32() != 1 || frame.GetProperty("target").GetString() != target)
            throw new InvalidOperationException($"Expected {target}; received {frame.GetRawText()}");
        return frame.GetProperty("arguments")[0];
    }
    private static async Task<int> CallbackCode(HttpResponseMessage response)
    {
        if (response.StatusCode != HttpStatusCode.OK) return -1;
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return body.RootElement.GetProperty("return_code").GetInt32();
    }
    private static async Task<ZaloPayIntentDto> Body(HttpResponseMessage response)
    {
        if (response.StatusCode != HttpStatusCode.OK)
            throw new InvalidOperationException($"Settlement API returned {(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        return await response.Content.ReadFromJsonAsync<ZaloPayIntentDto>() ?? throw new InvalidOperationException("Settlement API intent missing.");
    }
}
