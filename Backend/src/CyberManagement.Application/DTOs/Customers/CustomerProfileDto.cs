namespace CyberManagement.Application.DTOs.Customers;

public sealed record CustomerProfileDto(
    int CustomerId,
    string FullName,
    string Phone,
    string? Email,
    string Status,
    DateTime CreatedAtUtc);

public sealed record CustomerPageDto(
    IReadOnlyList<CustomerProfileDto> Items,
    int Page,
    int PageSize,
    int TotalCount);
