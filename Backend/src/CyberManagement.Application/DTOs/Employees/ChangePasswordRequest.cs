using System.ComponentModel.DataAnnotations;

namespace CyberManagement.Application.DTOs.Employees;

/// <summary>Changes an employee password after checking the current password and matching confirmation.</summary>
public sealed record ChangePasswordRequest(
    [Required] string CurrentPassword,
    [Required] string NewPassword,
    [Required] string ConfirmNewPassword);
