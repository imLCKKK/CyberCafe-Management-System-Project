using CyberManagement.Application.Interfaces.Repositories;
using CyberManagement.Domain.Entities;
using CyberManagement.Infrastructure.Persistance;
using Microsoft.EntityFrameworkCore;

namespace CyberManagement.Infrastructure.Repositories;

public sealed class CustomerIdentityRepository(ApplicationDbContext database)
    : ICustomerIdentityRepository
{
    public Task<Customer?> FindCustomerForAuthenticationAsync(
        string normalizedPhone,
        CancellationToken cancellationToken) =>
        database.Customers.SingleOrDefaultAsync(
            customer => customer.Phone == normalizedPhone,
            cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken) =>
        database.SaveChangesAsync(cancellationToken);
}
