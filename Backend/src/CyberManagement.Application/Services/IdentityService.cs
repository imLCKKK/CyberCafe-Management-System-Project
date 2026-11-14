using CyberManagement.Application.DTOs.Authentication;
using CyberManagement.Application.DTOs.Identity;
using CyberManagement.Application.Interfaces.Repositories;
using CyberManagement.Application.Interfaces.Services;
using CyberManagement.Application.Interfaces.Validation;

namespace CyberManagement.Application.Services;

public sealed class IdentityService(
    IIdentityRepository repository,
    IEmployeePasswordHasher passwordHasher,
    IRequestValidator<LoginCommand> loginCommandValidator) : IIdentityService
{
    public async Task<LoginResult> LoginAsync(
        LoginCommand command,
        CancellationToken cancellationToken)
    {
        loginCommandValidator.Validate(command);
        var username = command.Username.Trim().ToUpperInvariant();
        var employee = await repository.FindEmployeeForAuthenticationAsync(username, cancellationToken);
        if (employee is null)
            return new LoginResult(LoginStatus.InvalidCredentials, null);

        if (!employee.IsActive)
            return new LoginResult(LoginStatus.AccountUnavailable, null);

        var verification = passwordHasher.VerifyHashedPassword(
            employee, employee.PasswordHash, command.Password);
        if (verification == PasswordVerificationResult.Failed)
        {
            return new LoginResult(LoginStatus.InvalidCredentials, null);
        }

        if (verification == PasswordVerificationResult.SuccessRehashNeeded)
        {
            employee.PasswordHash = passwordHasher.HashPassword(employee, command.Password);
        }

        await repository.SaveChangesAsync(cancellationToken);

        var permissions = employee.Role.RolePermissions
            .Select(item => item.Permission.PermissionName)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();

        return new LoginResult(
            LoginStatus.Authenticated,
            new AuthenticatedEmployeeDto(
                employee.EmployeeId,
                employee.Username,
                employee.FullName,
                employee.Role.RoleName,
                permissions));
    }

    public async Task<IReadOnlyList<RoleSummaryDto>> GetRolesAsync(CancellationToken cancellationToken)
    {
        var roles = await repository.GetRolesWithPermissionsAsync(cancellationToken);
        return roles
            .OrderBy(role => role.RoleName, StringComparer.Ordinal)
            .Select(role => new RoleSummaryDto(
                role.RoleId,
                role.RoleName,
                role.Description,
                role.RolePermissions
                    .Select(item => item.Permission.PermissionName)
                    .Order(StringComparer.Ordinal)
                    .ToArray()))
            .ToArray();
    }
}
