namespace CyberManagement.Application.DTOs.Identity;

public sealed record IdentitySeedOptions(
    bool IsDevelopment,
    IReadOnlyDictionary<string, string?> AccountPasswords);
