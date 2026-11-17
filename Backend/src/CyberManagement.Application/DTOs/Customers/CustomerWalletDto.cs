namespace CyberManagement.Application.DTOs.Customers;

public sealed record WalletTransactionDto(
    int TransactionId,
    decimal Amount,
    decimal BalanceAfter,
    string Type,
    string? Description,
    DateTime CreatedAtUtc);

public sealed record CustomerWalletDto(
    int CustomerId,
    decimal Balance,
    IReadOnlyList<WalletTransactionDto> Transactions,
    int Page,
    int PageSize,
    int TotalCount);
