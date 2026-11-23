using System.ComponentModel.DataAnnotations;
using System.Globalization;
using CyberManagement.Application.DTOs.Payments;
using CyberManagement.Application.Exceptions;
using CyberManagement.Application.Interfaces.Repositories;
using CyberManagement.Application.Interfaces.Services;
using CyberManagement.Domain.Entities;

namespace CyberManagement.Application.Services;

// SCRUM-22 persists verified receipts; S2-K-2 consumes Order receipts. TopUp settlement belongs to S2-M-3.
public sealed class ZaloPayPaymentService(IZaloPayIntentRepository repository, IZaloPayGateway gateway,
    TimeProvider clock, IOrderZaloPaySettlement? orderSettlement = null) : IZaloPayPaymentService
{
    public async Task<ZaloPayIntentDto> CreateTopUpAsync(int customerId, decimal amountVnd, Guid requestKey, CancellationToken cancellationToken)
    {
        if (amountVnd is <= 0 or > 999_999_999 || decimal.Truncate(amountVnd) != amountVnd)
            throw new ValidationException("Số tiền nạp phải là số nguyên từ 1 đến 999.999.999 đồng.");
        return await CreateAsync(customerId, "TopUp", null, amountVnd, requestKey, cancellationToken);
    }

    public Task<ZaloPayIntentDto> CreateOrderAsync(int customerId, int orderId, Guid requestKey, CancellationToken cancellationToken)
    {
        if (orderId <= 0) throw new ValidationException("Mã đơn hàng phải là số nguyên dương.");
        // No amount argument: the repository reads the order's persisted server snapshot.
        return CreateAsync(customerId, "Order", orderId, null, requestKey, cancellationToken);
    }

    private async Task<ZaloPayIntentDto> CreateAsync(int customerId, string purpose, int? orderId,
        decimal? amountVnd, Guid key, CancellationToken cancellationToken)
    {
        RequireEnabled();
        if (customerId <= 0 || key == Guid.Empty) throw new ValidationException("Khách hàng và khóa yêu cầu không hợp lệ.");
        var creation = await repository.CreateAsync(customerId, purpose, orderId, amountVnd, key,
            gateway.AppId, clock.GetUtcNow(), cancellationToken);
        if (!creation.Created)
            return ToDto(NeedsCreateRecovery(creation.Intent)
                ? await SyncAsync(creation.Intent.IntentId, cancellationToken) : creation.Intent);
        var lease = await repository.AcquireCreateAsync(creation.Intent.IntentId, clock.GetUtcNow(), null, cancellationToken);
        if (!lease.Acquired || lease.Token is null) return ToDto(lease.Intent);
        return ToDto(await SendCreateAsync(lease, cancellationToken));
    }

    private async Task<ZaloPayIntent> SendCreateAsync(GatewayCreateLease lease, CancellationToken cancellationToken)
    {
        var intent = lease.Intent;
        // The intent, source and merchant ID are durable before HTTP; no SQL transaction crosses this call.
        var response = await gateway.CreateAsync(new GatewayCreateRequest(intent.IntentId, intent.Purpose,
            intent.CustomerId, intent.OrderId, intent.ProviderAppId, intent.MerchantTransactionId,
            intent.ExpectedAmountVnd, intent.CreatedAtUtc, intent.ExpiresAtUtc), cancellationToken);
        return await repository.SaveCreateResultAsync(intent.IntentId, lease.Token!.Value, response,
            clock.GetUtcNow(), cancellationToken);
    }

    public async Task<ZaloPayIntentDto> GetOwnAsync(int customerId, int intentId, CancellationToken cancellationToken)
    {
        ValidateIds(customerId, intentId);
        return ToDto(await repository.GetOwnAsync(customerId, intentId, cancellationToken));
    }

    public async Task<ZaloPayIntentDto> SyncOwnAsync(int customerId, int intentId, CancellationToken cancellationToken)
    {
        RequireEnabled();
        ValidateIds(customerId, intentId);
        _ = await repository.GetOwnAsync(customerId, intentId, cancellationToken);
        return ToDto(await SyncAsync(intentId, cancellationToken));
    }

    public async Task<int> HandleCallbackAsync(string data, string mac, int type, CancellationToken cancellationToken)
    {
        if (!gateway.Enabled) return 0;
        // Verify the exact original data before parsing or accessing source state.
        var callback = gateway.ReadCallback(data, mac, type);
        if (callback is null) return 2;
        var saved = await repository.FindByMerchantAsync(callback.MerchantTransactionId, cancellationToken);
        if (saved is null || saved.ProviderAppId != callback.AppId
            || callback.AppUser != "customer:" + saved.CustomerId.ToString(CultureInfo.InvariantCulture)
            || callback.AppTime != saved.CreatedAtUtc.ToUnixTimeMilliseconds()
            || callback.AmountVnd != saved.ExpectedAmountVnd) return 2;
        if (saved.Status == "Succeeded")
        {
            await SettleOrderAsync(saved, cancellationToken);
            return saved.ProviderTransactionId == callback.TransactionId && saved.ProviderAmountVnd == callback.AmountVnd ? 1 : 2;
        }

        var lease = await repository.AcquireQueryAsync(saved.IntentId, clock.GetUtcNow(), cancellationToken);
        if (!lease.Acquired || lease.Token is null) return 0;
        // Trial merchant keys are public: independently query the saved merchant ID even after a valid callback MAC.
        var query = await gateway.QueryAsync(saved.ProviderAppId, saved.MerchantTransactionId, cancellationToken);
        if (query.Receipt is not { } receipt)
        {
            await repository.SaveQueryResultAsync(saved.IntentId, lease.Token.Value, query, clock.GetUtcNow(), cancellationToken);
            return 0;
        }
        var matches = receipt.TransactionId == callback.TransactionId && receipt.AmountVnd == callback.AmountVnd;
        var confirmation = await repository.RecordReceiptAsync(saved.IntentId, lease.Token.Value,
            receipt,
            clock.GetUtcNow(), cancellationToken);
        await SettleOrderAsync(confirmation.Intent, cancellationToken);
        if (!matches) return 2;
        return confirmation.Intent.Status == "Succeeded"
            && confirmation.Intent.ProviderTransactionId == callback.TransactionId ? 1 : 0;
    }

    public async Task ReconcileDueAsync(CancellationToken cancellationToken)
    {
        if (!gateway.Enabled) return;
        foreach (var id in await repository.GetDueIdsAsync(clock.GetUtcNow(), 5, cancellationToken))
            await SyncAsync(id, cancellationToken);
    }

    private async Task<ZaloPayIntent> SyncAsync(int intentId, CancellationToken cancellationToken)
    {
        var lease = await repository.AcquireQueryAsync(intentId, clock.GetUtcNow(), cancellationToken);
        if (!lease.Acquired || lease.Token is null) return lease.Intent;
        var response = await gateway.QueryAsync(lease.Intent.ProviderAppId, lease.Intent.MerchantTransactionId, cancellationToken);
        if (response.Receipt is null)
        {
            // NotFound is the only recovery result that permits resending the original Create.
            // Promote, rather than release, the shared lease so concurrent replays cannot also Create.
            if (response.State == "NotFound" && NeedsCreateRecovery(lease.Intent))
            {
                var create = await repository.AcquireCreateAsync(intentId, clock.GetUtcNow(), lease.Token, cancellationToken);
                return create.Acquired && create.Token is not null
                    ? await SendCreateAsync(create, cancellationToken) : create.Intent;
            }
            return await repository.SaveQueryResultAsync(intentId, lease.Token.Value, response, clock.GetUtcNow(), cancellationToken);
        }
        var result = (await repository.RecordReceiptAsync(intentId, lease.Token.Value, response.Receipt,
            clock.GetUtcNow(), cancellationToken)).Intent;
        await SettleOrderAsync(result, cancellationToken);
        return result;
    }

    private Task SettleOrderAsync(ZaloPayIntent intent, CancellationToken ct) =>
        intent is { Purpose: "Order", OrderId: int id, Status: "Succeeded" } && orderSettlement is not null
            ? orderSettlement.ApplyAsync(id, ct) : Task.CompletedTask;

    private void RequireEnabled()
    {
        if (!gateway.Enabled) throw new PaymentUnavailableException("ZaloPay Sandbox chưa được bật trên máy chủ.");
    }

    private static bool NeedsCreateRecovery(ZaloPayIntent intent) => intent.Status == "Pending"
        && intent.PaymentUrl is null && intent.ProviderTransactionId is null;

    private static void ValidateIds(int customerId, int intentId)
    {
        if (customerId <= 0 || intentId <= 0) throw new ValidationException("Mã khách hàng/giao dịch phải là số nguyên dương.");
    }

    private static ZaloPayIntentDto ToDto(ZaloPayIntent row) => new(row.IntentId, row.CustomerId, row.Purpose,
        row.OrderId ?? row.IntentId, row.ExpectedAmountVnd, row.Status, row.PaymentUrl, row.MerchantTransactionId,
        row.CreatedAtUtc, row.ExpiresAtUtc, row.ProviderTransactionId, row.ProviderAmountVnd, row.VerifiedAtUtc,
        row.RequiresReconciliation, row.FailureReason);
}
