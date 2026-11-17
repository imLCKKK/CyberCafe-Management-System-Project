using CyberManagement.Application.DTOs.Payments;

namespace CyberManagement.Application.Interfaces.Services;

public interface IZaloPaySettlementService
{
    Task<ZaloPaySettlementResult> ApplyAsync(int intentId, CancellationToken cancellationToken);
    Task RecoverDueAsync(CancellationToken cancellationToken);
}
