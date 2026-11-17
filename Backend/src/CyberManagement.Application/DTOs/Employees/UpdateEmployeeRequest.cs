using System.ComponentModel.DataAnnotations;

namespace CyberManagement.Application.DTOs.Employees;

/// <summary>Updates an employee only if the supplied row version is still current.</summary>
public sealed record UpdateEmployeeRequest(
    string? FullName,
    string? Phone,
    int? RoleId,
    [Required] string ExpectedVersion);
