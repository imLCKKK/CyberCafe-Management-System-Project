using CyberManagement.Application.DTOs.Customers;

namespace CyberManagement.Application.Interfaces.Services;

public interface ICustomerService
{
    Task<CustomerPageDto> GetCustomersAsync(
        int page, int pageSize, string? search, CancellationToken cancellationToken);
    Task<CustomerProfileDto> GetProfileAsync(int customerId, CancellationToken cancellationToken);
    Task<CustomerProfileDto> GetOwnProfileAsync(int customerId, CancellationToken cancellationToken);
    Task<CustomerProfileDto> CreateAsync(CreateCustomerCommand command, CancellationToken cancellationToken);
    Task<CustomerWalletDto> GetWalletAsync(
        int customerId, int page, int pageSize, CancellationToken cancellationToken);
    Task<CustomerWalletDto> GetOwnWalletAsync(
        int customerId, int page, int pageSize, CancellationToken cancellationToken);
}
