using CyberManagement.Domain.Entities;

namespace CyberManagement.Application.Interfaces.Repositories;

public interface ICustomerIdentityRepository
{
    Task<Customer?> FindCustomerForAuthenticationAsync(
        string normalizedPhone,
        CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
