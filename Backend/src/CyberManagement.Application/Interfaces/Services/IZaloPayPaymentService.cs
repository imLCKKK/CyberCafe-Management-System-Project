using CyberManagement.Application.DTOs.Payments;

namespace CyberManagement.Application.Interfaces.Services;

public interface IZaloPayPaymentService
{
    Task<ZaloPayIntentDto> CreateTopUpAsync(int customerId, decimal amountVnd, Guid requestKey, CancellationToken cancellationToken);
    Task<ZaloPayIntentDto> CreateOrderAsync(int customerId, int orderId, Guid requestKey, CancellationToken cancellationToken);
    Task<ZaloPayIntentDto> GetOwnAsync(int customerId, int intentId, CancellationToken cancellationToken);
    Task<ZaloPayIntentDto> SyncOwnAsync(int customerId, int intentId, CancellationToken cancellationToken);
    Task<int> HandleCallbackAsync(string data, string mac, int type, CancellationToken cancellationToken);
    Task ReconcileDueAsync(CancellationToken cancellationToken);
}
