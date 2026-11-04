using CyberManagement.Application.Exceptions;
using CyberManagement.Domain.Entities;

namespace CyberManagement.Application.Wallet;

/// <summary>
/// Nơi DUY NHẤT được cộng/trừ ví. Mọi module (nạp tiền, phí phiên, đơn hàng, giải đấu) phải gọi
/// <see cref="Record"/> thay vì tự sửa <c>Customer.WalletBalance</c>. Xem Backend/docs/002-wallet-ledger.md.
///
/// Record chỉ đổi trạng thái entity (số dư + thêm một WalletTransaction vào customer.WalletTransactions);
/// KHÔNG lưu DB. Nơi gọi phải: mở transaction, tải khách bằng ICustomerRepository.FindTrackedForWalletUpdateAsync
/// (khóa dòng), gọi Record, lưu nghiệp vụ và ví trong cùng một SaveChanges rồi mới commit.
/// </summary>
public static class WalletLedger
{
    public static WalletTransaction Record(
        Customer customer, string type, decimal amount, DateTime recordedAtUtc, string? description = null)
    {
        ArgumentNullException.ThrowIfNull(customer);

        if (!WalletTransactionTypes.IsKnown(type))
            throw new ArgumentException("Loại giao dịch ví không hợp lệ.", nameof(type));
        if (amount == 0 || decimal.Truncate(amount) != amount)
            throw new ArgumentException("Số tiền phải khác 0 và là số nguyên VND.", nameof(amount));
        if (type != WalletTransactionTypes.Adjustment && WalletTransactionTypes.IsCredit(type) != (amount > 0))
            throw new ArgumentException($"{type} yêu cầu số tiền {(WalletTransactionTypes.IsCredit(type) ? "dương" : "âm")}.", nameof(amount));
        // Khách bị khóa: chặn nạp/trừ/điều chỉnh, nhưng vẫn cho HOÀN TIỀN (đơn/giải) để các luồng hủy
        // đơn, hủy giải không bị kẹt chỉ vì tài khoản khách đang khóa.
        var isRefund = type is WalletTransactionTypes.OrderRefund or WalletTransactionTypes.TournamentRefund;
        if (!isRefund && !string.Equals(customer.Status, "Active", StringComparison.OrdinalIgnoreCase))
            throw new BusinessRuleException("Khách hàng đang bị khóa, không thể thay đổi ví.");

        var balanceAfter = customer.WalletBalance + amount;
        if (balanceAfter < 0)
            throw new InsufficientWalletBalanceException(customer.WalletBalance, amount);

        customer.WalletBalance = balanceAfter;
        var transaction = new WalletTransaction
        {
            CustomerId = customer.CustomerId,
            Amount = amount,
            BalanceAfter = balanceAfter,
            Type = type,
            Description = description,
            CreatedAt = recordedAtUtc,
        };
        customer.WalletTransactions.Add(transaction);
        return transaction;
    }
}
