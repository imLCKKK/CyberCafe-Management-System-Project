using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace CyberManagement.API.Security;

public sealed class PermissionPolicyProvider(IOptions<AuthorizationOptions> options)
    : IAuthorizationPolicyProvider
{
    private const string PolicyPrefix = "permission:";
    private readonly DefaultAuthorizationPolicyProvider _fallback = new(options);

    public Task<AuthorizationPolicy> GetDefaultPolicyAsync() =>
        _fallback.GetDefaultPolicyAsync();

    public Task<AuthorizationPolicy?> GetFallbackPolicyAsync() =>
        _fallback.GetFallbackPolicyAsync();

    public Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
    {
        if (!policyName.StartsWith(PolicyPrefix, StringComparison.Ordinal)
            || string.IsNullOrWhiteSpace(policyName[PolicyPrefix.Length..]))
        {
            return _fallback.GetPolicyAsync(policyName);
        }

        var permission = policyName[PolicyPrefix.Length..];
        var policy = new AuthorizationPolicyBuilder()
            .RequireAuthenticatedUser()
            .AddRequirements(new PermissionRequirement(permission))
            .Build();

        return Task.FromResult<AuthorizationPolicy?>(policy);
    }
}
