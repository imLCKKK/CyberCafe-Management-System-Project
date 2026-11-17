using Microsoft.AspNetCore.Authorization;

namespace CyberManagement.API.Security;

public sealed record AccountStatusRequirement : IAuthorizationRequirement;
