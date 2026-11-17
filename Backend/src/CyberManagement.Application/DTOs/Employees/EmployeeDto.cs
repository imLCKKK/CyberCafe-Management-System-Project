namespace CyberManagement.Application.DTOs.Employees;

/// <summary>Employee profile with a Base64 row version for optimistic concurrency.</summary>
public sealed record EmployeeDto(
    int EmployeeId,
    string Username,
    string FullName,
    string? Phone,
    string RoleName,
    bool IsActive,
    DateTime CreatedAt,
    string Version);
