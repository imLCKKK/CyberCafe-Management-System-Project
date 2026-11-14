using CyberManagement.Application.DTOs.Customers;

namespace CyberManagement.Application.Interfaces.Repositories;

public interface ICounterTopUpRepository
{
    Task RequireActiveCustomerAsync(int customerId, CancellationToken cancellationToken);
    Task<CounterTopUpPageDto> GetAsync(int? customerId, string status, int page, int pageSize, CancellationToken cancellationToken);
    Task<CounterTopUpResult> CreateAsync(int customerId, Guid requestKey, CounterTopUpOptionDto quote, CancellationToken cancellationToken);
    Task<CounterTopUpResult> ApproveAsync(int requestId, int employeeId, decimal cashAmount, CancellationToken cancellationToken);
}
