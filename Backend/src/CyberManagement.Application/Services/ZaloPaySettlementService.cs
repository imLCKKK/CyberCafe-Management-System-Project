using CyberManagement.Application.DTOs.Payments;
using CyberManagement.Application.DTOs.Realtime;
using CyberManagement.Application.Interfaces.Repositories;
using CyberManagement.Application.Interfaces.Services;

namespace CyberManagement.Application.Services;

public sealed class ZaloPaySettlementService(IZaloPaySettlementRepository repository,
    IRealtimePublisher realtime, TimeProvider clock) : IZaloPaySettlementService
{
    public async Task<ZaloPaySettlementResult> ApplyAsync(int intentId, CancellationToken cancellationToken)
    {
        var result = await repository.ApplyAsync(intentId, clock.GetUtcNow(), cancellationToken);
        if (!result.Applied) return result;
        // The repository committed balance/ledger or Order/Payment and its durable applied marker first.
        var intent = result.Intent;
        var message = new RealtimeEvent(Guid.NewGuid(), intent.Purpose == "TopUp" ? "WalletChanged" : "OrderChanged",
            intent.OrderId ?? intent.CustomerId, clock.GetUtcNow());
        await realtime.PublishToAccountAsync(RealtimeAccountType.Customer, intent.CustomerId, message, CancellationToken.None);
        await realtime.PublishToPermissionAsync(intent.Purpose == "TopUp" ? "wallet.view" : "order.view", message, CancellationToken.None);
        await realtime.PublishToAccountAsync(RealtimeAccountType.Customer, intent.CustomerId,
            new RealtimeEvent(Guid.NewGuid(), "ZaloPayPaymentChanged", intent.IntentId, clock.GetUtcNow()), CancellationToken.None);
        return result;
    }

    public async Task RecoverDueAsync(CancellationToken cancellationToken)
    {
        foreach (var id in await repository.GetDueIdsAsync(clock.GetUtcNow(), 5, cancellationToken))
            await ApplyAsync(id, cancellationToken);
    }
}
