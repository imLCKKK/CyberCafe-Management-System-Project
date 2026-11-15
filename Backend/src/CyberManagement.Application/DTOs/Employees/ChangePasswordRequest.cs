namespace CyberManagement.Application.DTOs.Employees;

public sealed record ChangePasswordRequest(
    string CurrentPassword,
    string NewPassword);
