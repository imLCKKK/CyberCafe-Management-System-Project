namespace CyberManagement.Application.DTOs.Authentication;

public sealed record AuthenticatedEmployeeDto(
    int EmployeeId,
    string Username,
    string FullName,
    string Role,
    IReadOnlyList<string> Permissions);
