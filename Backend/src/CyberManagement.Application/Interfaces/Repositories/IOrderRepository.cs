using CyberManagement.Application.DTOs.Orders;
using CyberManagement.Domain.Entities;

namespace CyberManagement.Application.Interfaces.Repositories;

public interface IOrderRepository
{
    Task<T> ExecuteAsync<T>(Func<Task<T>> action, CancellationToken ct);
    Task AuthorizeAsync(OrderActor actor, string? permission, CancellationToken ct);
    Task<int> FindCustomerIdAsync(int orderId, int? ownerId, CancellationToken ct);
    Task ValidateSessionAsync(int customerId, int? sessionId, CancellationToken ct);
    Task LockWalletSourcesAsync(int customerId, CancellationToken ct);
    Task LockRelatedSessionAsync(int orderId, CancellationToken ct);
    Task<bool> CategoryActiveAsync(int categoryId, CancellationToken ct);
    Task<Order?> LockAsync(int orderId, CancellationToken ct);
    Task<OrderOperation?> FindOperationAsync(OrderActor actor, string route, string key, CancellationToken ct);
    Task<OrderDto> ReadAsync(int orderId, int? customerId, CancellationToken ct);
    Task<OrderPageDto> ReadPageAsync(int? customerId, string? status, int page, int pageSize, CancellationToken ct);
    Task<IReadOnlyList<Payment>> PaymentsAsync(int orderId, CancellationToken ct);
    Task<IReadOnlyList<WalletTransaction>> LedgersAsync(int orderId, CancellationToken ct);
    void Add(Order order);
    void AddPayment(Payment payment);
    void AddOperation(OrderOperation operation);
    Task SaveAsync(CancellationToken ct);
}
