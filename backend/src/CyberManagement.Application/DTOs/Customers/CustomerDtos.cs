namespace CyberManagement.Application.DTOs.Customers;

public sealed record CustomerDto(
    int CustomerId, string FullName, string Phone, string? Email,
    decimal WalletBalance, string Status, DateTime CreatedAt, bool HasActiveSession);

public sealed record CreateCustomerCommand(
    string? FullName, string? Phone, string? Email, string? InitialPassword, int ActorEmployeeId);

public sealed record UpdateCustomerCommand(int CustomerId, string? FullName, string? Phone, string? Email);

public sealed record ChangeCustomerStatusCommand(int CustomerId, string? NewStatus, string? Reason, int ActorEmployeeId);

// Nạp tại quầy: nhân viên xác nhận đã thu tiền (Cash/BankTransfer), ví được cộng ngay trong cùng transaction.
public sealed record StaffTopUpCommand(int CustomerId, decimal Amount, string? CollectionMethod, int ActorEmployeeId);

public sealed record WalletTransactionDto(
    int TransactionId, int CustomerId, decimal Amount, decimal BalanceAfter,
    string Type, string? Description, DateTime CreatedAt);

public sealed record ClientWalletDto(decimal WalletBalance, IReadOnlyList<WalletTransactionDto> Transactions);
