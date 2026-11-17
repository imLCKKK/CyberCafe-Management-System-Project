namespace CyberManagement.Application.DTOs.Employees;

public sealed record CreateEmployeeRequest(
    string Username,
    string FullName,
    string? Phone,
    string Password,
    int RoleId);
