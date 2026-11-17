using CyberManagement.Application.DTOs.Payments;
using CyberManagement.Domain.Entities;

namespace CyberManagement.Application.Interfaces.Services;

// Internal checkout contract: the caller must own the SQL transaction and reserved source.
// Neither method saves, commits, calls the provider or changes stock/voucher usage.
public interface IZaloPayOrderPaymentBridge
{
    Task<Payment> RegisterAsync(int customerId, int orderId, int intentId,
        DateTimeOffset reservationExpiresAtUtc, CancellationToken cancellationToken);
    Task<ZaloPaySettlementResult> ConfirmAsync(int intentId, DateTimeOffset now, CancellationToken cancellationToken);
}
