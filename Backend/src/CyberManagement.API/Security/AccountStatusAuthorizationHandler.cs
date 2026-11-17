using System.IdentityModel.Tokens.Jwt;
using CyberManagement.Application.Interfaces.Repositories;
using Microsoft.AspNetCore.Authorization;

namespace CyberManagement.API.Security;

public sealed class AccountStatusAuthorizationHandler
    : AuthorizationHandler<AccountStatusRequirement>
{
    private readonly IIdentityRepository _repository;

    public AccountStatusAuthorizationHandler(IIdentityRepository repository)
    {
        _repository = repository;
    }

    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        AccountStatusRequirement requirement)
    {
        var employeeIdClaim = context.User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        if (string.IsNullOrEmpty(employeeIdClaim) || !int.TryParse(employeeIdClaim, out var employeeId))
        {
            context.Fail(new AuthorizationFailureReason(this, "Could not determine current employee."));
            return;
        }

        var employee = await _repository.GetEmployeeByIdAsync(employeeId, CancellationToken.None);
        if (employee is null || !employee.IsActive)
        {
            context.Fail(new AuthorizationFailureReason(this, "Account is not active."));
            return;
        }

        context.Succeed(requirement);
    }
}
