using CyberManagement.Application.DTOs.Authentication;
using CyberManagement.Application.DTOs.Identity;

namespace CyberManagement.Application.Interfaces.Services;

public interface IIdentityService
{
    Task<LoginResult> LoginAsync(LoginCommand command, CancellationToken cancellationToken);

    Task<IReadOnlyList<RoleSummaryDto>> GetRolesAsync(CancellationToken cancellationToken);
}

public enum LoginStatus
{
    Authenticated,
    InvalidCredentials,
    AccountUnavailable
}

public sealed record LoginResult(
    LoginStatus Status,
    AuthenticatedEmployeeDto? Employee);
