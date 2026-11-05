using CyberCafe.Api.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace CyberCafe.Api.Features;

/// <summary>Ném ra khi một giao dịch ví làm số dư xuống dưới 0.</summary>
public sealed class InsufficientWalletBalanceException(decimal balance, decimal amount)
    : InvalidOperationException($"Số dư {balance:N0} không đủ cho giao dịch {amount:N0}.")
{
    public decimal Balance { get; } = balance;
    public decimal Amount { get; } = amount;
}

/// <summary>
/// Nơi DUY NHẤT được cộng/trừ ví. Mọi module (nạp tiền, phí phiên, đơn hàng, giải đấu)
/// phải gọi <see cref="Record"/> thay vì tự sửa <c>Customer.WalletBalance</c>.
///
/// Hợp đồng:
/// - Record chỉ thay đổi trạng thái trong DbContext: đổi WalletBalance, tăng WalletVersion
///   và thêm một WalletTransaction (amount có dấu, balanceAfter = số dư sau giao dịch).
/// - Record KHÔNG gọi SaveChanges và KHÔNG commit. Module gọi phải mở transaction,
///   lưu cả nghiệp vụ lẫn ví trong cùng một SaveChanges rồi mới Commit.
/// - Customer phải là entity đang được DbContext theo dõi (tải bằng query có tracking).
///   WalletVersion là concurrency token: hai request cùng sửa một ví thì một request nhận
///   DbUpdateConcurrencyException; dùng <see cref="IsConflict"/> để đổi thành HTTP 409.
/// - Quy ước dấu/nguồn khớp CHECK constraint trong CyberCafeDbContext.
/// </summary>
public static class WalletLedger
{
    public static WalletTransaction Record(
        CyberCafeDbContext db,
        Customer customer,
        WalletTransactionType type,
        decimal amount,
        DateTimeOffset recordedAt,
        long? recordedByAccountId = null,
        long? topUpId = null,
        long? gamingSessionId = null,
        long? orderId = null,
        long? orderItemId = null,
        long? tournamentRegistrationId = null)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(customer);

        if (db.Entry(customer).State is EntityState.Detached)
            throw new InvalidOperationException("Customer phải đang được DbContext theo dõi để ví được lưu.");
        if (amount == 0 || decimal.Truncate(amount) != amount)
            throw new ArgumentOutOfRangeException(nameof(amount), "Số tiền phải khác 0 và là số nguyên VND.");

        var wantsCredit = type is WalletTransactionType.TopUpCredit
            or WalletTransactionType.OrderItemRefund
            or WalletTransactionType.TournamentRefund;
        if (wantsCredit != (amount > 0))
            throw new ArgumentException($"{type} yêu cầu số tiền {(wantsCredit ? "dương" : "âm")}.", nameof(amount));

        var sources = new[] { topUpId, gamingSessionId, orderId, orderItemId, tournamentRegistrationId };
        if (sources.Count(s => s.HasValue) != 1)
            throw new ArgumentException("Phải có đúng một nguồn giao dịch (top-up, phiên, đơn, món hoặc đăng ký giải).");
        var expectedSource = type switch
        {
            WalletTransactionType.TopUpCredit => topUpId,
            WalletTransactionType.GamingCharge => gamingSessionId,
            WalletTransactionType.OrderDebit => orderId,
            WalletTransactionType.OrderItemRefund => orderItemId,
            WalletTransactionType.TournamentFee or WalletTransactionType.TournamentRefund => tournamentRegistrationId,
            _ => throw new ArgumentOutOfRangeException(nameof(type)),
        };
        if (expectedSource is null)
            throw new ArgumentException($"Nguồn giao dịch không khớp loại {type}.");

        var balanceAfter = customer.WalletBalance + amount;
        if (balanceAfter < 0)
            throw new InsufficientWalletBalanceException(customer.WalletBalance, amount);

        customer.WalletBalance = balanceAfter;
        customer.WalletVersion++;

        var transaction = new WalletTransaction
        {
            CustomerId = customer.Id,
            TransactionType = type,
            Amount = amount,
            BalanceAfter = balanceAfter,
            TopUpId = topUpId,
            GamingSessionId = gamingSessionId,
            OrderId = orderId,
            OrderItemId = orderItemId,
            TournamentRegistrationId = tournamentRegistrationId,
            RecordedByAccountId = recordedByAccountId,
            RecordedAt = recordedAt,
        };
        db.WalletTransactions.Add(transaction);
        return transaction;
    }

    /// <summary>
    /// True nếu lỗi là xung đột dữ liệu nên trả HTTP 409: ghi đè đồng thời (concurrency token),
    /// vi phạm unique index, hoặc deadlock do transaction Serializable.
    /// </summary>
    public static bool IsConflict(Exception ex) =>
        ex is DbUpdateException { InnerException: SqlException { Number: 2601 or 2627 or 1205 } }
        || ex is DbUpdateConcurrencyException
        || ex is SqlException { Number: 2601 or 2627 or 1205 };
}