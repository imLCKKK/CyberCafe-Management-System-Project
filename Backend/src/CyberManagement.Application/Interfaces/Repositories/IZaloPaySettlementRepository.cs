using CyberManagement.Application.DTOs.Payments;

namespace CyberManagement.Application.Interfaces.Repositories;

public interface IZaloPaySettlementRepository
{
    Task<ZaloPaySettlementResult> ApplyAsync(int intentId, DateTimeOffset now, CancellationToken cancellationToken);
    Task<IReadOnlyList<int>> GetDueIdsAsync(DateTimeOffset now, int take, CancellationToken cancellationToken);
}
