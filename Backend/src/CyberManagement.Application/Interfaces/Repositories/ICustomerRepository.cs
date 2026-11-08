using CyberManagement.Application.DTOs.Customers;
using CyberManagement.Domain.Entities;

namespace CyberManagement.Application.Interfaces.Repositories;

public interface ICustomerRepository
{
    Task<CustomerPageDto> GetCustomersAsync(
        int page, int pageSize, string? search, CancellationToken cancellationToken);
    Task<CustomerProfileDto> GetProfileAsync(
        int customerId, bool requireActive, CancellationToken cancellationToken);
    Task<CustomerProfileDto> CreateAsync(Customer customer, CancellationToken cancellationToken);
    Task<CustomerWalletDto> GetWalletAsync(
        int customerId, int page, int pageSize, bool requireActive, CancellationToken cancellationToken);
}
