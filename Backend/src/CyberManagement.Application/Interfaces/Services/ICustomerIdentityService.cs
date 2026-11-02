using CyberManagement.Application.DTOs.Authentication;

namespace CyberManagement.Application.Interfaces.Services;

public interface ICustomerIdentityService
{
    Task<CustomerLoginResult> LoginAsync(
        CustomerLoginCommand command,
        CancellationToken cancellationToken);
}

public enum CustomerLoginStatus
{
    Authenticated,
    InvalidCredentials,
    AccountUnavailable
}

public sealed record CustomerLoginResult(
    CustomerLoginStatus Status,
    AuthenticatedCustomerDto? Customer);
