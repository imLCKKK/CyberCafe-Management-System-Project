using CyberManagement.Application.DTOs.Authentication;
using CyberManagement.Application.Interfaces.Repositories;
using CyberManagement.Application.Interfaces.Services;
using CyberManagement.Application.Interfaces.Validation;

namespace CyberManagement.Application.Services;

public sealed class CustomerIdentityService(
    ICustomerIdentityRepository repository,
    ICustomerPasswordHasher passwordHasher,
    IRequestValidator<CustomerLoginCommand> loginCommandValidator) : ICustomerIdentityService
{
    public async Task<CustomerLoginResult> LoginAsync(
        CustomerLoginCommand command,
        CancellationToken cancellationToken)
    {
        loginCommandValidator.Validate(command);
        var normalizedPhone = command.Phone.Trim();
        var customer = await repository.FindCustomerForAuthenticationAsync(
            normalizedPhone, cancellationToken);
        if (customer?.PasswordHash is null)
            return new CustomerLoginResult(CustomerLoginStatus.InvalidCredentials, null);

        if (!string.Equals(customer.Status, "Active", StringComparison.OrdinalIgnoreCase))
        {
            return new CustomerLoginResult(CustomerLoginStatus.AccountUnavailable, null);
        }

        var verification = passwordHasher.VerifyHashedPassword(
            customer, customer.PasswordHash, command.Password);
        if (verification == PasswordVerificationResult.Failed)
        {
            return new CustomerLoginResult(CustomerLoginStatus.InvalidCredentials, null);
        }

        if (verification == PasswordVerificationResult.SuccessRehashNeeded)
        {
            customer.PasswordHash = passwordHasher.HashPassword(customer, command.Password);
        }

        await repository.SaveChangesAsync(cancellationToken);

        return new CustomerLoginResult(
            CustomerLoginStatus.Authenticated,
            new AuthenticatedCustomerDto(
                customer.CustomerId,
                customer.FullName,
                customer.Phone,
                customer.Email,
                customer.WalletBalance,
                customer.Status));
    }
}
