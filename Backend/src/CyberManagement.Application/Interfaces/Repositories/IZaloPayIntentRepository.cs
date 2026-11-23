using CyberManagement.Application.DTOs.Payments;
using CyberManagement.Domain.Entities;

namespace CyberManagement.Application.Interfaces.Repositories;

public interface IZaloPayIntentRepository
{
    Task<GatewayIntentCreation> CreateAsync(int customerId, string purpose, int? orderId,
        decimal? topUpAmount, Guid requestKey, int appId, DateTimeOffset now, CancellationToken cancellationToken);
    Task<ZaloPayIntent> GetOwnAsync(int customerId, int intentId, CancellationToken cancellationToken);
    Task<(IReadOnlyList<ZaloPayIntent> Items, int TotalCount)> GetOwnPageAsync(int customerId, int page,
        int pageSize, string? purpose, CancellationToken cancellationToken);
    Task<ZaloPayIntent?> FindByMerchantAsync(string merchantId, CancellationToken cancellationToken);
    Task<GatewayCreateLease> AcquireCreateAsync(int intentId, DateTimeOffset now, Guid? queryToken, CancellationToken cancellationToken);
    Task<ZaloPayIntent> SaveCreateResultAsync(int intentId, Guid token, GatewayCreateResult result,
        DateTimeOffset now, CancellationToken cancellationToken);
    Task<ZaloPayIntent> RecordCallbackMismatchAsync(int intentId, Guid? queryToken,
        DateTimeOffset now, CancellationToken cancellationToken);
    Task<GatewayQueryLease> AcquireQueryAsync(int intentId, DateTimeOffset now, CancellationToken cancellationToken);
    Task<ZaloPayIntent> SaveQueryResultAsync(int intentId, Guid token, GatewayQueryResult result,
        DateTimeOffset now, CancellationToken cancellationToken);
    Task<GatewayReceiptResult> RecordReceiptAsync(int intentId, Guid token, GatewayReceipt receipt,
        DateTimeOffset now, CancellationToken cancellationToken);
    Task<IReadOnlyList<int>> GetDueIdsAsync(DateTimeOffset now, int take, CancellationToken cancellationToken);
}
