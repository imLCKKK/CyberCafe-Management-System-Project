using System.ComponentModel.DataAnnotations;
using System.Globalization;
using CyberManagement.Application.DTOs.Payments;
using CyberManagement.Application.Exceptions;
using CyberManagement.Application.Interfaces.Services;
using CyberManagement.Application.Wallets;
using CyberManagement.Domain.Entities;
using CyberManagement.Infrastructure.Persistance;
using Microsoft.EntityFrameworkCore;

namespace CyberManagement.Infrastructure.Payments;

public sealed class ZaloPayOrderPaymentBridge(ApplicationDbContext database, IWalletLedgerWriter wallet,
    TimeProvider clock) : IZaloPayOrderPaymentBridge
{
    public async Task<Payment> RegisterAsync(int customerId, int orderId, int intentId,
        DateTimeOffset reservationExpiresAtUtc, CancellationToken cancellationToken)
    {
        ZaloPaySettlementLocks.RequireTransaction(database);
        if (customerId <= 0 || orderId <= 0 || intentId <= 0)
            throw new ValidationException("Mã khách, đơn và giao dịch phải là số nguyên dương.");
        var (customer, order, intent) = await ZaloPaySettlementLocks.LoadAsync(database, wallet, intentId, cancellationToken);
        if (intent.Purpose != "Order" || intent.CustomerId != customerId || intent.OrderId != orderId
            || order is null || order.CustomerId != customerId)
            throw new BusinessRuleException("Ý định thanh toán không khớp nguồn checkout.");
        var existing = await FindPaymentAsync(intentId, cancellationToken);
        if (existing is not null)
        {
            if (!BindingMatches(existing, intent) || existing.GatewayReservationExpiresAtUtc != reservationExpiresAtUtc)
                throw new BusinessRuleException("Ý định đã được đăng ký với nội dung giữ chỗ khác.");
            if (intent.SettlementFailureReason == "CheckoutRegistrationMissing") intent.SettlementFailureReason = null;
            return existing;
        }
        if (intent.AppliedAtUtc is not null || customer.Status != "Active" || order.Status != "AwaitingPayment")
            throw new BusinessRuleException("Chỉ đăng ký checkout đang chờ thanh toán của khách hoạt động.");
        var now = clock.GetUtcNow();
        var orderDeadline = new DateTimeOffset(DateTime.SpecifyKind(order.OrderTime, DateTimeKind.Utc)).AddMinutes(15);
        if (order.TotalAmount != intent.ExpectedAmountVnd || reservationExpiresAtUtc <= now
            || reservationExpiresAtUtc > intent.ExpiresAtUtc || reservationExpiresAtUtc > orderDeadline)
            throw new BusinessRuleException("Số tiền hoặc hạn giữ chỗ không khớp snapshot checkout.");
        if (await database.Payments.AnyAsync(row => row.OrderId == orderId && row.Type == "Payment"
                && row.Status == "Succeeded", cancellationToken)
            || await ZaloPaySettlementLocks.HasGatewayPaymentConflictAsync(database, intent, null, cancellationToken))
            throw new BusinessRuleException("Nguồn checkout đã có khoản thu hoặc giao dịch gateway khác.");
        var payment = new Payment
        {
            OrderId = orderId, Type = "Payment", Method = "ZaloPay", Status = "Pending",
            Amount = intent.ExpectedAmountVnd, OperationKey = Key(intent), MerchantTransactionId = intent.MerchantTransactionId,
            ZaloPayIntentId = intentId, GatewayReservationExpiresAtUtc = reservationExpiresAtUtc, CreatedAt = now
        };
        database.Payments.Add(payment);
        if (intent.SettlementFailureReason == "CheckoutRegistrationMissing") intent.SettlementFailureReason = null;
        return payment;
    }

    public async Task<ZaloPaySettlementResult> ConfirmAsync(int intentId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        ZaloPaySettlementLocks.RequireTransaction(database);
        var (customer, order, intent) = await ZaloPaySettlementLocks.LoadAsync(database, wallet, intentId, cancellationToken);
        if (intent.AppliedAtUtc is not null) return new(intent, false);
        if (intent.Purpose != "Order" || order is null || order.CustomerId != intent.CustomerId)
            return ZaloPaySettlementLocks.Review(intent, "OrderSourceMismatch");
        if (!ZaloPaySettlementLocks.VerifiedReceipt(intent)) return new(intent, false);
        if (now < intent.VerifiedAtUtc) return ZaloPaySettlementLocks.Review(intent, "InvalidConfirmationTime");
        if (intent.RequiresReconciliation)
        {
            intent.SettlementFailureReason ??= intent.FailureReason ?? "ManualReconciliationRequired";
            return new(intent, false);
        }
        if (customer.Status != "Active") return ZaloPaySettlementLocks.Review(intent, "CustomerLocked");
        if (order.Status != "AwaitingPayment") return ZaloPaySettlementLocks.Review(intent, "OrderClosed");
        if (order.TotalAmount != intent.ExpectedAmountVnd) return ZaloPaySettlementLocks.Review(intent, "OrderAmountChanged");
        if (now >= intent.ExpiresAtUtc) return ZaloPaySettlementLocks.Review(intent, "LatePayment");
        var payment = await FindPaymentAsync(intentId, cancellationToken);
        if (payment is null)
        {
            // A valid receipt must not make an unregistered legacy or cash order look paid.
            intent.SettlementFailureReason = "CheckoutRegistrationMissing";
            return new(intent, false);
        }
        if (!BindingMatches(payment, intent)) return ZaloPaySettlementLocks.Review(intent, "CheckoutBindingMismatch");
        var orderDeadline = new DateTimeOffset(DateTime.SpecifyKind(order.OrderTime, DateTimeKind.Utc)).AddMinutes(15);
        if (now >= intent.ExpiresAtUtc || now >= payment.GatewayReservationExpiresAtUtc
            || payment.GatewayReservationExpiresAtUtc > intent.ExpiresAtUtc || payment.GatewayReservationExpiresAtUtc > orderDeadline)
            return ZaloPaySettlementLocks.Review(intent, "LatePayment");
        if (payment.Status != "Pending" || await database.Payments.AnyAsync(row => row.OrderId == order.OrderId
                && row.Type == "Payment" && row.Status == "Succeeded" && row.PaymentId != payment.PaymentId, cancellationToken))
            return ZaloPaySettlementLocks.Review(intent, "OrderAlreadyPaid");
        if (await ZaloPaySettlementLocks.HasGatewayPaymentConflictAsync(database, intent, payment, cancellationToken))
            return ZaloPaySettlementLocks.Review(intent, "GatewayTransactionConflict");
        payment.Status = "Succeeded";
        payment.GatewayTransactionId = intent.ProviderTransactionId;
        payment.ConfirmedAt = intent.VerifiedAtUtc;
        payment.PaymentTime = intent.VerifiedAtUtc;
        payment.PaymentTimeBasis = "Confirmation";
        payment.ProviderOccurredAt = null;
        order.Status = "Pending"; // Staff confirmation of food remains a separate operation.
        intent.Payment = payment;
        intent.AppliedAtUtc = now;
        intent.SettlementFailureReason = null;
        return new(intent, true);
    }

    private async Task<Payment?> FindPaymentAsync(int intentId, CancellationToken cancellationToken) =>
        database.Payments.Local.SingleOrDefault(row => row.ZaloPayIntentId == intentId)
        ?? await database.Payments.SingleOrDefaultAsync(row => row.ZaloPayIntentId == intentId, cancellationToken);

    private static string Key(ZaloPayIntent intent) => "ZaloPayOrder:" + intent.MerchantTransactionId;
    private static bool BindingMatches(Payment payment, ZaloPayIntent intent) => payment.ZaloPayIntentId == intent.IntentId
        && payment.OrderId == intent.OrderId && payment.SessionChargeId is null && payment.Type == "Payment"
        && payment.Method == "ZaloPay" && payment.Amount == intent.ExpectedAmountVnd
        && payment.WalletTransactionId is null
        && payment.MerchantTransactionId == intent.MerchantTransactionId && payment.OperationKey == Key(intent)
        && payment.GatewayReservationExpiresAtUtc is not null;
}

internal static class ZaloPaySettlementLocks
{
    internal static void RequireTransaction(ApplicationDbContext database)
    {
        if (!database.Database.IsSqlServer() || database.Database.CurrentTransaction is null)
            throw new InvalidOperationException("Gateway settlement requires a caller-owned SQL Server transaction.");
    }

    internal static async Task<(Customer Customer, Order? Order, ZaloPayIntent Intent)> LoadAsync(
        ApplicationDbContext database, IWalletLedgerWriter wallet, int intentId, CancellationToken cancellationToken)
    {
        RequireTransaction(database);
        var source = await database.Set<ZaloPayIntent>().AsNoTracking().Where(row => row.IntentId == intentId)
            .Select(row => new { row.CustomerId, row.OrderId }).SingleOrDefaultAsync(cancellationToken)
            ?? throw new EntityNotFoundException("Không tìm thấy ý định thanh toán.");
        var customer = await wallet.LockCustomerAsync(source.CustomerId, cancellationToken);
        Order? order = null;
        if (source.OrderId is int orderId)
        {
            var snapshot = await database.Orders.FromSqlInterpolated(
                $"SELECT * FROM [Orders] WITH (UPDLOCK, HOLDLOCK) WHERE [OrderId] = {orderId}")
                .AsNoTracking().SingleAsync(cancellationToken);
            order = TrackFresh(database, snapshot, row => row.OrderId == orderId);
        }
        var intentSnapshot = await database.Set<ZaloPayIntent>().FromSqlInterpolated(
                $"SELECT * FROM [ZaloPayIntents] WITH (UPDLOCK, HOLDLOCK) WHERE [IntentId] = {intentId}")
            .AsNoTracking().SingleAsync(cancellationToken);
        var intent = TrackFresh(database, intentSnapshot, row => row.IntentId == intentId);
        return (customer, order, intent);
    }

    private static T TrackFresh<T>(ApplicationDbContext database, T snapshot, Func<T, bool> matches) where T : class
    {
        var tracked = database.Set<T>().Local.SingleOrDefault(matches);
        if (tracked is null) { database.Attach(snapshot); return snapshot; }
        var values = database.Entry(snapshot).CurrentValues;
        foreach (var property in database.Entry(tracked).Properties)
        {
            if (property.IsModified) continue; // Keep changes made by the owner of this transaction.
            property.CurrentValue = values[property.Metadata.Name];
            property.OriginalValue = values[property.Metadata.Name];
        }
        return tracked;
    }

    internal static bool VerifiedReceipt(ZaloPayIntent intent) => intent.Status == "Succeeded"
        && intent.VerifiedAtUtc is not null && intent.ProviderAmountVnd == intent.ExpectedAmountVnd
        && intent.ExpectedAmountVnd is > 0 and <= 999_999_999
        && !string.IsNullOrEmpty(intent.ProviderTransactionId);

    internal static ZaloPaySettlementResult Review(ZaloPayIntent intent, string reason)
    {
        intent.RequiresReconciliation = true;
        intent.SettlementFailureReason = reason;
        return new(intent, false);
    }

    internal static async Task<bool> HasGatewayPaymentConflictAsync(ApplicationDbContext database, ZaloPayIntent intent,
        Payment? allowed, CancellationToken cancellationToken)
    {
        // The held SQL key ranges also protect a TopUp (which intentionally has no Payment row)
        // against a concurrent legacy writer using the same merchant/provider reference.
        var stored = await database.Payments.FromSqlInterpolated(
            $"SELECT * FROM [Payments] WITH (UPDLOCK, HOLDLOCK) WHERE [Method] = N'ZaloPay' AND [Type] = N'Payment' AND ([MerchantTransactionId] = {intent.MerchantTransactionId} OR ([GatewayTransactionId] = {intent.ProviderTransactionId} AND [GatewayTransactionId] IS NOT NULL))")
            .AsNoTracking().ToListAsync(cancellationToken);
        var rows = stored.Concat(database.Payments.Local.Where(row => database.Entry(row).State == EntityState.Added
            && row.Method == "ZaloPay" && row.Type == "Payment" && (row.MerchantTransactionId == intent.MerchantTransactionId
                || (intent.ProviderTransactionId is not null && row.GatewayTransactionId == intent.ProviderTransactionId))));
        return rows.Any(row => !ReferenceEquals(row, allowed) && !(allowed?.PaymentId > 0 && row.PaymentId == allowed.PaymentId));
    }
}
