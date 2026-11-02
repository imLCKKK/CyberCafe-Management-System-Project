using Microsoft.AspNetCore.Authorization;

namespace CyberManagement.API.Security;

public sealed record PermissionRequirement(string Permission) : IAuthorizationRequirement;
