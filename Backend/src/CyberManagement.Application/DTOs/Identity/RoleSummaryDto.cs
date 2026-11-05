namespace CyberManagement.Application.DTOs.Identity;

public sealed record RoleSummaryDto(
    int RoleId,
    string RoleName,
    string? Description,
    IReadOnlyList<string> Permissions);
