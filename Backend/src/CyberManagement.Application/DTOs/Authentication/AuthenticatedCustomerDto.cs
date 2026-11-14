namespace CyberManagement.Application.DTOs.Authentication;

public sealed record AuthenticatedCustomerDto(
    int CustomerId,
    string FullName,
    string Phone,
    string? Email,
    decimal WalletBalance,
    string Status);
