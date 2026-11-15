namespace CyberManagement.Application.DTOs.Employees;

public sealed record EmployeeDto(
    int EmployeeId,
    string Username,
    string FullName,
    string? Phone,
    string RoleName,
    bool IsActive,
    DateTime CreatedAt);
