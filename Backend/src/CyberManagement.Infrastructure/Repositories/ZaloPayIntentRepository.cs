using System.ComponentModel.DataAnnotations;
using System.Data;
using System.Globalization;
using CyberManagement.Application.DTOs.Payments;
using CyberManagement.Application.Exceptions;
using CyberManagement.Application.Interfaces.Repositories;
using CyberManagement.Domain.Entities;
using CyberManagement.Infrastructure.Persistance;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace CyberManagement.Infrastructure.Repositories;

public sealed class ZaloPayIntentRepository(ApplicationDbContext database) : IZaloPayIntentRepository
{
    private static readonly string[] ManualReasons = ["AmountMismatch", "GatewayTransactionConflict", "ConflictingReceipt"];
    private DbSet<ZaloPayIntent> Intents => database.Set<ZaloPayIntent>();

    public async Task<GatewayIntentCreation> CreateAsync(int customerId, string purpose, int? orderId,
        decimal? topUpAmount, Guid requestKey, int appId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        if (customerId <= 0 || appId <= 0 || requestKey == Guid.Empty
            || (purpose == "TopUp" ? orderId is not null || !ValidAmount(topUpAmount)
                : purpose != "Order" || orderId is not > 0 || topUpAmount is not null))
            throw new ValidationException("Thông tin ý định thanh toán không hợp lệ.");
        var operationKey = $"ZaloPay:{purpose}:{customerId.ToString(CultureInfo.InvariantCulture)}:{requestKey:N}";
        await using var transaction = await BeginWriteAsync(cancellationToken);
        var customer = await LockCustomerAsync(customerId, cancellationToken);
        RequireActive(customer);
        var prior = await Intents.SingleOrDefaultAsync(row => row.OperationKey == operationKey, cancellationToken);
        if (prior is not null)
        {
            if (prior.CustomerId != customerId || prior.Purpose != purpose || prior.OrderId != orderId
                || (purpose == "TopUp" && prior.ExpectedAmountVnd != topUpAmount))
                throw new BusinessRuleException("Khóa yêu cầu đã được dùng cho nội dung thanh toán khác.");
            await transaction.CommitAsync(cancellationToken);
            return new(prior, false);
        }
        var amount = topUpAmount ?? 0;
        var expiresAt = now.AddMinutes(15);
        if (purpose == "Order")
        {
            var order = await LockOrderAsync(orderId!.Value, cancellationToken);
            if (order.CustomerId != customerId) throw new EntityNotFoundException("Không tìm thấy đơn hàng của tài khoản.");
            if (await Intents.AnyAsync(row => row.OrderId == orderId, cancellationToken))
                throw new BusinessRuleException("Đơn hàng đã có ý định thanh toán. Hãy kiểm tra giao dịch đã lưu.");
            if (order.Status is not ("Pending" or "AwaitingPayment")) throw new BusinessRuleException("Đơn hàng không còn chờ thanh toán.");
            var orderCreatedAt = new DateTimeOffset(DateTime.SpecifyKind(order.OrderTime, DateTimeKind.Utc));
            if (orderCreatedAt > now.AddMinutes(5))
                throw new BusinessRuleException("Mốc thời gian đơn hàng không hợp lệ.");
            var orderDeadline = orderCreatedAt.AddMinutes(15);
            expiresAt = orderDeadline < expiresAt ? orderDeadline : expiresAt;
            if (expiresAt - now < TimeSpan.FromSeconds(300))
                throw new BusinessRuleException("Đơn hàng không còn đủ thời hạn để mở thanh toán gateway.");
            amount = order.TotalAmount;
            if (!ValidAmount(amount)) throw new BusinessRuleException("Số tiền đơn hàng không phù hợp thanh toán gateway.");
            if (await database.Payments.AnyAsync(row => row.OrderId == orderId && row.Type == "Payment"
                    && row.Status == "Succeeded", cancellationToken))
                throw new BusinessRuleException("Đơn hàng đã có khoản thanh toán thành công.");
        }
        var intent = new ZaloPayIntent
        {
            CustomerId = customerId, Customer = customer, OrderId = orderId, Purpose = purpose,
            OperationKey = operationKey, ExpectedAmountVnd = amount, ProviderAppId = appId,
            MerchantTransactionId = now.ToOffset(TimeSpan.FromHours(7)).ToString("yyMMdd", CultureInfo.InvariantCulture) + "_" + Guid.NewGuid().ToString("N"),
            CreatedAtUtc = now, ExpiresAtUtc = expiresAt, NextQueryAtUtc = now.AddSeconds(20)
        };
        Intents.Add(intent);
        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new(intent, true);
    }

    public async Task<ZaloPayIntent> GetOwnAsync(int customerId, int intentId, CancellationToken cancellationToken)
    {
        var customer = await database.Customers.AsNoTracking().SingleOrDefaultAsync(row => row.CustomerId == customerId, cancellationToken)
            ?? throw new EntityNotFoundException("Không tìm thấy khách hàng.");
        RequireActive(customer);
        return await Intents.AsNoTracking().SingleOrDefaultAsync(row => row.IntentId == intentId && row.CustomerId == customerId, cancellationToken)
            ?? throw new EntityNotFoundException("Không tìm thấy ý định thanh toán.");
    }

    public Task<ZaloPayIntent?> FindByMerchantAsync(string merchantId, CancellationToken cancellationToken) =>
        Intents.AsNoTracking().SingleOrDefaultAsync(row => row.MerchantTransactionId == merchantId, cancellationToken);

    public async Task<GatewayCreateLease> AcquireCreateAsync(int intentId, DateTimeOffset now, Guid? queryToken,
        CancellationToken cancellationToken)
    {
        await using var transaction = await BeginWriteAsync(cancellationToken);
        var (customer, row) = await LockIntentAsync(intentId, cancellationToken);
        // The same durable lease serializes every Create/Query exchange for this merchant ID.
        // Only a brand-new intent may skip Query. A retry promotes a held NotFound Query lease.
        var available = queryToken is Guid token
            ? row.QueryLeaseToken == token && row.QueryLeaseUntilUtc > now
            : row.QueryLeaseToken is null && row.LastQueryAtUtc is null
                && row.FailureReason is null && !row.RequiresReconciliation;
        var acquired = false;
        if (available && row.Status == "Pending" && row.PaymentUrl is null
            && row.ProviderTransactionId is null && !NeedsManualReview(row))
        {
            if (queryToken is not null) row.LastQueryAtUtc = now;
            string? reason = customer.Status != "Active" ? "CreateCustomerLocked"
                : row.ExpiresAtUtc - now < TimeSpan.FromSeconds(300) ? "CreateRecoveryExpired"
                : row.MerchantTransactionId[..6] != now.ToOffset(TimeSpan.FromHours(7)).ToString("yyMMdd", CultureInfo.InvariantCulture)
                    ? "CreateMerchantDayChanged" : null;
            if (reason is null && row.OrderId is int orderId)
            {
                var order = await LockOrderAsync(orderId, cancellationToken);
                if (order.CustomerId != row.CustomerId || order.Status is not ("Pending" or "AwaitingPayment")
                    || order.TotalAmount != row.ExpectedAmountVnd
                    || new DateTimeOffset(DateTime.SpecifyKind(order.OrderTime, DateTimeKind.Utc)).AddMinutes(15) < row.ExpiresAtUtc
                    || await database.Payments.AnyAsync(payment => payment.OrderId == orderId
                        && payment.Type == "Payment" && payment.Status == "Succeeded", cancellationToken))
                    reason = "CreateSourceChanged";
            }
            row.RequiresReconciliation = true;
            row.FailureReason = reason ?? (queryToken is null ? "CreateUnavailable" : "CreateRecoveryUnavailable");
            if (reason is null)
            {
                row.QueryLeaseToken = queryToken ?? Guid.NewGuid();
                row.QueryLeaseUntilUtc = now.AddSeconds(30);
                acquired = true;
            }
            else
            {
                row.QueryLeaseToken = null;
                row.QueryLeaseUntilUtc = null;
                row.NextQueryAtUtc = NextQuery(now, row.ExpiresAtUtc);
            }
            await database.SaveChangesAsync(cancellationToken);
        }
        await transaction.CommitAsync(cancellationToken);
        return new(row, acquired ? row.QueryLeaseToken : null, acquired);
    }

    public async Task<ZaloPayIntent> SaveCreateResultAsync(int intentId, Guid token, GatewayCreateResult result,
        DateTimeOffset now, CancellationToken cancellationToken)
    {
        await using var transaction = await BeginWriteAsync(cancellationToken);
        var (_, row) = await LockIntentAsync(intentId, cancellationToken);
        if (row.QueryLeaseToken == token)
        {
            row.QueryLeaseToken = null;
            row.QueryLeaseUntilUtc = null;
            row.NextQueryAtUtc = NextQuery(now, row.ExpiresAtUtc);
            if (row.Status != "Pending" || row.PaymentUrl is not null
                || row.ProviderTransactionId is not null || NeedsManualReview(row))
            {
                await database.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return row;
            }
            if (result.Rejected)
            {
                // NotFound does not prove an earlier uncertain Create can never arrive.
                // A rejection of its retry must leave that merchant ID under reconciliation.
                if (row.FailureReason == "CreateRecoveryUnavailable")
                {
                    row.RequiresReconciliation = true;
                    row.FailureReason = "CreateRecoveryRejected";
                }
                else
                {
                    row.Status = "Failed";
                    row.FailureReason = "CreateRejected";
                    row.RequiresReconciliation = false;
                    row.NextQueryAtUtc = null;
                }
            }
            else if (result.PaymentUrl is { } url)
            {
                if (url.Length > 2048 || !Uri.TryCreate(url, UriKind.Absolute, out var address)
                    || address.Scheme != "https" || address.Port != 443 || !string.IsNullOrEmpty(address.UserInfo)
                    || address.Host is not ("sbgateway.zalopay.vn" or "sbcheckout.zalopay.vn" or "qcgateway.zalopay.vn"))
                    throw new ValidationException("Địa chỉ thanh toán Sandbox không hợp lệ.");
                row.PaymentUrl ??= url;
                row.RequiresReconciliation = false;
                row.FailureReason = null;
                row.NextQueryAtUtc = NextQuery(now, row.ExpiresAtUtc);
            }
            else
            {
                row.RequiresReconciliation = true;
                row.FailureReason = result.FailureReason == "AppChanged" ? "AppChanged" : "CreateUnavailable";
            }
            await database.SaveChangesAsync(cancellationToken);
        }
        await transaction.CommitAsync(cancellationToken);
        return row;
    }

    public async Task<GatewayQueryLease> AcquireQueryAsync(int intentId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        await using var transaction = await BeginWriteAsync(cancellationToken);
        var (_, row) = await LockIntentAsync(intentId, cancellationToken);
        var acquired = row.Status != "Succeeded" && (row.QueryLeaseUntilUtc is null || row.QueryLeaseUntilUtc <= now);
        if (acquired)
        {
            row.QueryLeaseToken = Guid.NewGuid();
            row.QueryLeaseUntilUtc = now.AddSeconds(30);
            await database.SaveChangesAsync(cancellationToken);
        }
        await transaction.CommitAsync(cancellationToken);
        return new(row, acquired ? row.QueryLeaseToken : null, acquired);
    }

    public async Task<ZaloPayIntent> SaveQueryResultAsync(int intentId, Guid token, GatewayQueryResult result,
        DateTimeOffset now, CancellationToken cancellationToken)
    {
        await using var transaction = await BeginWriteAsync(cancellationToken);
        var (_, row) = await LockIntentAsync(intentId, cancellationToken);
        if (row.QueryLeaseToken == token && row.Status != "Succeeded")
        {
            CompleteQuery(row, now);
            if (row.ProviderTransactionId is null && !NeedsManualReview(row))
            {
                switch (result.State)
                {
                    case "Expired":
                        row.Status = "Expired";
                        row.RequiresReconciliation = false;
                        row.FailureReason = "ProviderExpired";
                        row.NextQueryAtUtc = null;
                        break;
                    case "Failed" when !result.RetryableFailure:
                        row.Status = "Failed";
                        row.RequiresReconciliation = false;
                        row.FailureReason = "ProviderRejected";
                        row.NextQueryAtUtc = null;
                        break;
                    case "Pending" when row.Status == "Pending":
                        row.RequiresReconciliation = row.PaymentUrl is null;
                        row.FailureReason = row.PaymentUrl is null ? "CheckoutUrlMissing"
                            : result.RetryableFailure ? "PaymentAttemptFailed" : null;
                        break;
                    case "NotFound":
                        row.RequiresReconciliation = true;
                        row.FailureReason = "ProviderNotFound";
                        break;
                    default:
                        row.RequiresReconciliation = true;
                        row.FailureReason = result.FailureReason == "AppChanged" ? "AppChanged" : "QueryUnavailable";
                        break;
                }
            }
            await database.SaveChangesAsync(cancellationToken);
        }
        await transaction.CommitAsync(cancellationToken);
        return row;
    }

    public async Task<GatewayReceiptResult> RecordReceiptAsync(int intentId, Guid token, GatewayReceipt receipt,
        DateTimeOffset now, CancellationToken cancellationToken)
    {
        if (receipt.AppId <= 0 || !ValidAmount(receipt.AmountVnd) || !ValidReference(receipt.TransactionId))
            throw new ValidationException("Chứng từ thanh toán không hợp lệ.");
        for (var attempt = 0; ; attempt++)
        {
            try { return await RecordReceiptCoreAsync(intentId, token, receipt, now, cancellationToken); }
            catch (DbUpdateException error) when (attempt == 0 && error.InnerException is SqlException { Number: 2601 or 2627 })
            {
                // The first transaction has rolled back; reload the winning receipt under the same lock order.
                database.ChangeTracker.Clear();
            }
        }
    }

    private async Task<GatewayReceiptResult> RecordReceiptCoreAsync(int intentId, Guid token, GatewayReceipt receipt,
        DateTimeOffset now, CancellationToken cancellationToken)
    {
        await using var transaction = await BeginWriteAsync(cancellationToken);
        var (customer, row) = await LockIntentAsync(intentId, cancellationToken);
        if (receipt.AppId != row.ProviderAppId) throw new BusinessRuleException("Ứng dụng thanh toán không khớp ý định đã lưu.");
        if (row.ProviderTransactionId is not null)
        {
            var changed = false;
            if (row.QueryLeaseToken == token
                && (row.ProviderTransactionId != receipt.TransactionId || row.ProviderAmountVnd != receipt.AmountVnd))
            {
                row.RequiresReconciliation = true;
                row.FailureReason = "ConflictingReceipt";
                changed = true;
            }
            if (row.QueryLeaseToken == token)
            {
                CompleteQuery(row, now);
                row.NextQueryAtUtc = null;
                changed = true;
            }
            if (changed) await database.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new(row, false);
        }
        if (row.QueryLeaseToken != token)
        {
            await transaction.CommitAsync(cancellationToken);
            return new(row, false);
        }
        if (await Intents.AnyAsync(other => other.IntentId != intentId && other.ProviderAppId == receipt.AppId
                && other.ProviderTransactionId == receipt.TransactionId, cancellationToken))
        {
            row.RequiresReconciliation = true;
            row.FailureReason = "GatewayTransactionConflict";
            CompleteQuery(row, now);
            await database.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new(row, false);
        }
        var oldStatus = row.Status;
        var priorManualReason = NeedsManualReview(row) ? row.FailureReason : null;
        row.ProviderTransactionId = receipt.TransactionId;
        row.ProviderAmountVnd = receipt.AmountVnd;
        row.ProviderOccurredAtUtc = receipt.ProviderOccurredAtUtc;
        row.VerifiedAtUtc = now;
        CompleteQuery(row, now);
        row.NextQueryAtUtc = null;
        if (receipt.AmountVnd != row.ExpectedAmountVnd)
        {
            row.Status = "Pending";
            row.RequiresReconciliation = true;
            row.FailureReason = "AmountMismatch";
        }
        else
        {
            row.Status = "Succeeded";
            row.FailureReason = priorManualReason ?? (customer.Status != "Active" ? "CustomerLocked"
                : oldStatus is "Expired" or "Failed" || now >= row.ExpiresAtUtc ? "LatePayment" : null);
            if (row.OrderId is int orderId)
            {
                var order = await database.Orders.AsNoTracking().SingleAsync(item => item.OrderId == orderId, cancellationToken);
                if (order.Status is not ("Pending" or "AwaitingPayment")) row.FailureReason ??= "OrderClosed";
                else if (order.TotalAmount != row.ExpectedAmountVnd) row.FailureReason ??= "OrderAmountChanged";
                if (await database.Payments.AnyAsync(payment => payment.OrderId == orderId && payment.Type == "Payment"
                        && payment.Status == "Succeeded" && (payment.Method != "ZaloPay"
                            || payment.MerchantTransactionId != row.MerchantTransactionId), cancellationToken))
                    row.FailureReason ??= "OrderAlreadyPaid";
            }
            row.RequiresReconciliation = row.FailureReason is not null;
        }
        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new(row, true);
    }

    public async Task<IReadOnlyList<int>> GetDueIdsAsync(DateTimeOffset now, int take, CancellationToken cancellationToken)
    {
        if (take <= 0) return [];
        return await Intents.AsNoTracking().Where(row => row.Status == "Pending" && row.ProviderTransactionId == null
                && (row.NextQueryAtUtc == null || row.NextQueryAtUtc <= now)
                && (row.QueryLeaseUntilUtc == null || row.QueryLeaseUntilUtc <= now)
                && (row.FailureReason == null || !ManualReasons.Contains(row.FailureReason)))
            .OrderBy(row => row.NextQueryAtUtc).ThenBy(row => row.IntentId).Take(Math.Min(take, 5))
            .Select(row => row.IntentId).ToListAsync(cancellationToken);
    }

    private async Task<IDbContextTransaction> BeginWriteAsync(CancellationToken cancellationToken)
    {
        if (!database.Database.IsSqlServer() || database.Database.CurrentTransaction is not null)
            throw new InvalidOperationException("Gateway intent writes require an independent SQL Server unit of work.");
        if (database.ChangeTracker.HasChanges())
            throw new InvalidOperationException("Save or discard the caller's changes before starting a gateway intent operation.");
        database.ChangeTracker.Clear();
        return await database.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
    }

    private async Task<Customer> LockCustomerAsync(int customerId, CancellationToken cancellationToken) =>
        await database.Customers.FromSqlInterpolated($"SELECT * FROM [Customers] WITH (UPDLOCK, HOLDLOCK) WHERE [CustomerId] = {customerId}")
            .SingleOrDefaultAsync(cancellationToken) ?? throw new EntityNotFoundException("Không tìm thấy khách hàng.");

    private async Task<Order> LockOrderAsync(int orderId, CancellationToken cancellationToken) =>
        await database.Orders.FromSqlInterpolated($"SELECT * FROM [Orders] WITH (UPDLOCK, HOLDLOCK) WHERE [OrderId] = {orderId}")
            .SingleOrDefaultAsync(cancellationToken) ?? throw new EntityNotFoundException("Không tìm thấy đơn hàng.");

    private async Task<(Customer Customer, ZaloPayIntent Intent)> LockIntentAsync(int intentId, CancellationToken cancellationToken)
    {
        var customerId = await Intents.AsNoTracking().Where(row => row.IntentId == intentId).Select(row => (int?)row.CustomerId)
            .SingleOrDefaultAsync(cancellationToken) ?? throw new EntityNotFoundException("Không tìm thấy ý định thanh toán.");
        var customer = await LockCustomerAsync(customerId, cancellationToken);
        var intent = await Intents.FromSqlInterpolated($"SELECT * FROM [ZaloPayIntents] WITH (UPDLOCK, HOLDLOCK) WHERE [IntentId] = {intentId}")
            .SingleAsync(cancellationToken);
        return (customer, intent);
    }

    private static void RequireActive(Customer customer)
    {
        if (customer.Status != "Active") throw new UnauthorizedAccessException("Tài khoản khách hàng hiện không hoạt động.");
    }

    private static bool ValidAmount(decimal? amount) => amount is > 0 and <= 999_999_999 && decimal.Truncate(amount.Value) == amount;
    private static bool ValidReference(string? value) => value is { Length: > 0 and <= 19 }
        && long.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var numeric) && numeric > 0
        && numeric.ToString(CultureInfo.InvariantCulture) == value;
    private static bool NeedsManualReview(ZaloPayIntent row) => row.FailureReason is not null && ManualReasons.Contains(row.FailureReason);
    private static void CompleteQuery(ZaloPayIntent row, DateTimeOffset now)
    {
        row.QueryLeaseToken = null;
        row.QueryLeaseUntilUtc = null;
        row.LastQueryAtUtc = now;
        row.NextQueryAtUtc = NextQuery(now, row.ExpiresAtUtc);
    }
    private static DateTimeOffset NextQuery(DateTimeOffset now, DateTimeOffset expiry) =>
        now - expiry >= TimeSpan.FromHours(24) ? now.AddHours(1) : now.AddSeconds(15);
}
