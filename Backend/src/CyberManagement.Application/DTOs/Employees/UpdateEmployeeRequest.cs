namespace CyberManagement.Application.DTOs.Employees;

public sealed record UpdateEmployeeRequest(
    string? FullName,
    string? Phone,
    int? RoleId);
