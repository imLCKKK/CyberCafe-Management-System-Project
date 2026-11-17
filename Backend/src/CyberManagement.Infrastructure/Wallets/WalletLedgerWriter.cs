using System.ComponentModel.DataAnnotations;
using CyberManagement.Application.Exceptions;
using CyberManagement.Application.Wallets;
using CyberManagement.Domain.Entities;
using CyberManagement.Infrastructure.Persistance;
using Microsoft.EntityFrameworkCore;

namespace CyberManagement.Infrastructure.Wallets;

public sealed class WalletLedgerWriter(ApplicationDbContext database) : IWalletLedgerWriter
{
    private const decimal MaximumBalance = 999_999_999_999_999_999m;
    private readonly Dictionary<int, Customer> lockedCustomers = [];
    private readonly HashSet<WalletTransaction> createdInTransaction = [];
    private Guid? transactionId;

    public async Task<Customer> LockCustomerAsync(int customerId, CancellationToken cancellationToken)
    {
        if (customerId <= 0) throw new ValidationException("Mã khách hàng không hợp lệ.");
        RequireTransaction();
        if (lockedCustomers.TryGetValue(customerId, out var locked)
            && database.Entry(locked).State != EntityState.Detached) return locked;

        var snapshot = await database.Customers.FromSqlInterpolated(
                $"SELECT * FROM [Customers] WITH (UPDLOCK, HOLDLOCK) WHERE [CustomerId] = {customerId}")
            .AsNoTracking().SingleOrDefaultAsync(cancellationToken)
            ?? throw new EntityNotFoundException("Không tìm thấy khách hàng.");
        var customer = database.Customers.Local.SingleOrDefault(row => row.CustomerId == customerId);
        if (customer is null)
        {
            customer = snapshot;
            database.Customers.Attach(customer);
        }
        else
        {
            // A read earlier in this scope may predate the write lock. Refresh only the
            // fields owned by this writer; never discard a caller's unrelated edits.
            var entry = database.Entry(customer);
            if (entry.Property(row => row.WalletBalance).IsModified || entry.Property(row => row.Status).IsModified)
                throw new InvalidOperationException("Acquire the customer wallet lock before changing its balance or status.");
            customer.WalletBalance = snapshot.WalletBalance;
            customer.Status = snapshot.Status;
            entry.Property(row => row.WalletBalance).OriginalValue = snapshot.WalletBalance;
            entry.Property(row => row.Status).OriginalValue = snapshot.Status;
        }
        lockedCustomers[customerId] = customer;
        return customer;
    }

    public async Task<WalletMutationResult> RecordAsync(WalletMutation mutation, CancellationToken cancellationToken)
    {
        Validate(mutation);
        var customer = await LockCustomerAsync(mutation.CustomerId, cancellationToken);
        var existing = createdInTransaction.SingleOrDefault(row => row.OperationKey == mutation.OperationKey)
            ?? await database.WalletTransactions.AsNoTracking().SingleOrDefaultAsync(
                row => row.OperationKey == mutation.OperationKey, cancellationToken);
        if (existing is not null)
        {
            if (!Matches(existing, mutation))
                throw new BusinessRuleException("Khóa thao tác ví đã được dùng cho nội dung khác.");
            var tracked = database.WalletTransactions.Local.SingleOrDefault(row => row.TransactionId == existing.TransactionId
                && existing.TransactionId > 0);
            if (tracked is not null && database.Entry(tracked).State is EntityState.Modified or EntityState.Deleted)
                throw new InvalidOperationException("Recorded wallet entries are immutable.");
            if (tracked is null && !createdInTransaction.Contains(existing)) database.WalletTransactions.Attach(existing);
            return new WalletMutationResult(tracked ?? existing, true);
        }
        if (createdInTransaction.Any(row => SameSource(row, mutation))
            || await database.WalletTransactions.AnyAsync(row => row.SourceType == mutation.SourceType
                && row.SourceId == mutation.SourceId && row.Type == mutation.Type, cancellationToken))
            throw new BusinessRuleException("Nguồn giao dịch này đã được ghi vào ví bằng khóa khác.");

        if (mutation.Kind != WalletMutationKind.Refund && customer.Status != "Active")
            throw new BusinessRuleException("Khách hàng phải đang hoạt động để nạp hoặc sử dụng ví.");
        if (mutation.Kind == WalletMutationKind.Refund)
            await ValidateRefundAsync(mutation, cancellationToken);
        var signedAmount = mutation.Kind == WalletMutationKind.Debit ? -mutation.Amount : mutation.Amount;
        var balanceAfter = customer.WalletBalance + signedAmount;
        if (balanceAfter < 0) throw new BusinessRuleException("Số dư ví không đủ để thanh toán.");
        if (balanceAfter > MaximumBalance || decimal.Truncate(balanceAfter) != balanceAfter)
            throw new BusinessRuleException("Số dư ví vượt giới hạn lưu trữ hoặc không phải số nguyên đồng.");

        var entry = new WalletTransaction
        {
            CustomerId = mutation.CustomerId, Customer = customer, Amount = signedAmount,
            BalanceAfter = balanceAfter, Type = mutation.Type, Description = mutation.Description,
            OperationKey = mutation.OperationKey, Kind = mutation.Kind.ToString(),
            SourceType = mutation.SourceType, SourceId = mutation.SourceId,
            OriginalDebitId = mutation.OriginalDebitId, ActorType = mutation.Actor.Type,
            ActorEmployeeId = mutation.Actor.EmployeeId, CreatedAt = mutation.OccurredAtUtc.UtcDateTime
        };
        customer.WalletBalance = balanceAfter;
        database.WalletTransactions.Add(entry);
        createdInTransaction.Add(entry);
        return new WalletMutationResult(entry, false);
    }

    private async Task ValidateRefundAsync(WalletMutation mutation, CancellationToken cancellationToken)
    {
        // Query the database, rather than Local, so an unflushed debit cannot authorize a refund.
        var original = await database.WalletTransactions.AsNoTracking().SingleOrDefaultAsync(
            row => row.TransactionId == mutation.OriginalDebitId, cancellationToken);
        if (original is null || original.CustomerId != mutation.CustomerId || original.Kind != "Debit"
            || original.Amount >= 0 || original.Type != "OrderPayment" || original.SourceType != "Order"
            || createdInTransaction.Any(row => row.TransactionId == original.TransactionId))
            throw new BusinessRuleException("Chỉ được hoàn khoản thanh toán đơn bằng ví đã ghi nhận của chính khách hàng.");
        var refunded = await database.WalletTransactions.Where(row => row.OriginalDebitId == original.TransactionId)
            .SumAsync(row => (decimal?)row.Amount, cancellationToken) ?? 0m;
        refunded += database.ChangeTracker.Entries<WalletTransaction>()
            .Where(entry => entry.State == EntityState.Added && entry.Entity.OriginalDebitId == original.TransactionId)
            .Sum(entry => entry.Entity.Amount);

        var paymentIds = await database.Payments.Where(row => row.Type == "Payment" && row.Method == "Wallet"
                && row.Status == "Succeeded" && row.WalletTransactionId == original.TransactionId)
            .Select(row => row.PaymentId).ToListAsync(cancellationToken);
        if (paymentIds.Count > 0)
        {
            var reservations = await database.Payments.AsNoTracking().Where(row => row.Type == "Refund"
                    && row.Method == "Wallet" && row.Status == "Pending" && row.RefundOfPaymentId != null
                    && paymentIds.Contains(row.RefundOfPaymentId.Value))
                .ToListAsync(cancellationToken);
            foreach (var tracked in database.ChangeTracker.Entries<Payment>())
            {
                if (tracked.State is EntityState.Unchanged or EntityState.Detached) continue;
                reservations.RemoveAll(row => row.PaymentId == tracked.Entity.PaymentId && row.PaymentId > 0);
                if (tracked.State != EntityState.Deleted && tracked.Entity.Type == "Refund"
                    && tracked.Entity.Method == "Wallet" && tracked.Entity.Status == "Pending"
                    && tracked.Entity.RefundOfPaymentId is int paymentId && paymentIds.Contains(paymentId))
                    reservations.Add(tracked.Entity);
            }
            var countedLedgerIds = await database.WalletTransactions.Where(row => row.OriginalDebitId == original.TransactionId)
                .Select(row => row.TransactionId).ToListAsync(cancellationToken);
            foreach (var reservation in reservations)
            {
                if (reservation.OperationKey == mutation.OperationKey)
                {
                    if (reservation.Amount != mutation.Amount)
                        throw new BusinessRuleException("Số tiền hoàn không khớp khoản đang giữ cùng khóa.");
                    continue; // This call converts its own reservation into a ledger entry.
                }
                if (reservation.WalletTransactionId is null || !countedLedgerIds.Contains(reservation.WalletTransactionId.Value))
                    refunded += reservation.Amount;
            }
        }
        if (refunded + mutation.Amount > -original.Amount)
            throw new BusinessRuleException("Tổng tiền hoàn và đang giữ hoàn vượt khoản đã trừ từ ví.");
    }

    private void RequireTransaction()
    {
        if (!database.Database.IsSqlServer() || database.Database.CurrentTransaction is null)
            throw new InvalidOperationException("Wallet writes require a caller-owned SQL Server transaction.");
        var current = database.Database.CurrentTransaction.TransactionId;
        if (transactionId == current) return;
        if (createdInTransaction.Any(row => database.Entry(row).State == EntityState.Added))
            throw new InvalidOperationException("Discard rolled-back wallet changes before starting another transaction.");
        transactionId = current;
        lockedCustomers.Clear();
        createdInTransaction.Clear();
    }

    private static bool SameSource(WalletTransaction row, WalletMutation mutation) =>
        row.SourceType == mutation.SourceType && row.SourceId == mutation.SourceId && row.Type == mutation.Type;

    private static bool Matches(WalletTransaction row, WalletMutation mutation) =>
        row.CustomerId == mutation.CustomerId && row.Kind == mutation.Kind.ToString()
        && row.Amount == (mutation.Kind == WalletMutationKind.Debit ? -mutation.Amount : mutation.Amount)
        && SameSource(row, mutation) && row.OriginalDebitId == mutation.OriginalDebitId
        && row.ActorType == mutation.Actor.Type && row.ActorEmployeeId == mutation.Actor.EmployeeId
        && row.Description == mutation.Description;

    private static void Validate(WalletMutation mutation)
    {
        if (mutation.CustomerId <= 0 || mutation.Amount is <= 0 or > 999_999_999
            || decimal.Truncate(mutation.Amount) != mutation.Amount || !Enum.IsDefined(mutation.Kind)
            || !ValidText(mutation.Type, 50) || !ValidText(mutation.SourceType, 50)
            || !ValidText(mutation.SourceId, 100) || !ValidText(mutation.OperationKey, 100)
            || mutation.Description?.Length > 255 || mutation.Actor is null
            || !((mutation.Actor.Type == "Staff" && mutation.Actor.EmployeeId > 0)
                || (mutation.Actor.Type == "System" && mutation.Actor.EmployeeId is null))
            || (mutation.Kind == WalletMutationKind.Refund ? mutation.OriginalDebitId is not > 0 : mutation.OriginalDebitId is not null))
            throw new ValidationException("Thông tin thao tác ví không hợp lệ.");
        var validSource = (mutation.Kind, mutation.Type, mutation.SourceType) switch
        {
            (WalletMutationKind.Credit, "TopUp", "CounterTopUp" or "ZaloPayTopUp") => true,
            (WalletMutationKind.Debit, "GamingCharge", "SessionCharge") => true,
            (WalletMutationKind.Debit, "TournamentFee", "TournamentRegistration") => true,
            (WalletMutationKind.Debit, "OrderPayment", "Order") => true,
            (WalletMutationKind.Refund, "OrderRefund", "OrderDetail") => true,
            _ => false
        };
        if (!validSource) throw new ValidationException("Loại và nguồn thao tác ví không phù hợp.");
    }

    private static bool ValidText(string? value, int maximum) =>
        !string.IsNullOrWhiteSpace(value) && value.Length <= maximum && value == value.Trim();
}
