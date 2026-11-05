namespace CyberManagement.Application.Exceptions;

// Số dư ví không đủ: API ánh xạ sang 409 như mọi BusinessRuleException.
public sealed class InsufficientWalletBalanceException(decimal balance, decimal amount)
    : BusinessRuleException($"Số dư {balance:N0} không đủ cho giao dịch {amount:N0}.")
{
    public decimal Balance { get; } = balance;
    public decimal Amount { get; } = amount;
}
