using CyberManagement.Application.DTOs.Realtime;
using CyberManagement.Application.Exceptions;
using CyberManagement.Application.Interfaces.Repositories;
using CyberManagement.Application.Interfaces.Services;
using CyberManagement.Application.Wallets;
using CyberManagement.Domain.Entities;

namespace CyberManagement.Application.Services;

// Order side of the S2-M-3 boundary. Gateway receipts are durable before this independent transaction.
public sealed class OrderZaloPaySettlement(IOrderRepository orders, IInventoryRepository products,
    IInventoryService inventory, IVoucherService vouchers, IWalletLedgerWriter wallet,
    IInvoiceService invoices, IRealtimePublisher realtime, TimeProvider clock) : IOrderZaloPaySettlement
{
    public async Task ApplyAsync(int orderId, CancellationToken cancellationToken)
    {
        var ct = cancellationToken;
        var customerId = await orders.FindCustomerIdAsync(orderId, null, ct);
        var changed = await orders.ExecuteAsync(async () =>
        {
            await wallet.LockCustomerAsync(customerId, ct);
            await orders.LockRelatedSessionAsync(orderId, ct);
            var order = await orders.LockAsync(orderId, ct) ?? throw new EntityNotFoundException("Không tìm thấy đơn.");
            if (order.PaymentMethod != "ZaloPay" || order.TotalAmount == 0 || order.ReservedUntilUtc is null) return false;
            var payment = await orders.LockZaloPayPaymentAsync(orderId, ct);
            var receipt = await orders.GatewayIntentAsync(orderId, ct);
            // Replay precedes the deadline check: an already paid order never expires on callback retry.
            if (payment.Status == "Succeeded") return false;
            var now = clock.GetUtcNow();
            var expired = order.Status == "AwaitingPayment" && order.ReservedUntilUtc <= now;
            if (expired)
            {
                await products.GetProductsForUpdateAsync(order.Details.Select(x => x.ProductId).ToArray(), ct);
                foreach (var detail in order.Details.OrderBy(x => x.ProductId))
                {
                    if (detail.Status != "Pending") throw new BusinessRuleException("Đơn chờ gateway có dòng món không hợp lệ.");
                    detail.Status = "Cancelled";
                    await inventory.ApplyReturnAsync(detail.OrderDetailId, detail.ProductId, detail.Quantity,
                        null, $"OrderReturn:{orderId}:{detail.OrderDetailId}", ct);
                }
                order.Status = "Expired";
                order.StatusHistory.Add(new OrderStatusHistory { OldStatus = "AwaitingPayment", NewStatus = "Expired",
                    ActorType = "System", ChangedAt = now.UtcDateTime, Reason = "ReservationExpired" });
                await orders.SaveAsync(ct);
                await vouchers.ReleaseForCancelledOrderAsync(orderId, ct);
                if (payment.Status != "Failed") payment.Status = "Expired";
            }

            var verified = receipt is { Status: "Succeeded", Purpose: "Order", ProviderTransactionId: not null, VerifiedAtUtc: not null }
                && receipt.CustomerId == customerId && receipt.OrderId == orderId
                && receipt.ExpectedAmountVnd == payment.Amount && receipt.ProviderAmountVnd == payment.Amount
                && payment.Amount == order.TotalAmount && receipt.MerchantTransactionId == payment.MerchantTransactionId;
            if (!verified)
            {
                var failed = receipt?.Status == "Failed" && payment.Status == "Pending";
                if (failed) payment.Status = "Failed";
                if (!expired && !failed) return false;
            }
            else
            {
                var old = payment.Status;
                payment.Status = "Succeeded";
                payment.GatewayTransactionId = receipt!.ProviderTransactionId;
                payment.ProviderOccurredAt = receipt.ProviderOccurredAtUtc;
                payment.ConfirmedAt = now;
                payment.PaymentTime = now;
                payment.PaymentTimeBasis = "Confirmation";
                if (order.Status == "AwaitingPayment" && order.ReservedUntilUtc > now)
                {
                    order.Status = "Pending";
                    order.StatusHistory.Add(new OrderStatusHistory { OldStatus = "AwaitingPayment", NewStatus = "Pending",
                        ActorType = "System", ChangedAt = now.UtcDateTime, Reason = "VerifiedZaloPay" });
                }
                else if (order.Status is "Expired" or "Cancelled")
                {
                    // Record the real money and an unapproved obligation; never resurrect stock or credit the wallet.
                    orders.AddPayment(new Payment { OrderId = orderId, RefundOfPaymentId = payment.PaymentId,
                        Type = "Refund", Method = "ZaloPay", Status = "Pending", Amount = payment.Amount,
                        OperationKey = $"OrderLateRefund:{orderId}:{payment.PaymentId}",
                        Reason = "LateVerifiedZaloPay: closed order", CreatedAt = now });
                }
                else throw new BusinessRuleException("Trạng thái đơn không khớp khoản thu gateway.");
                if (old == "Failed" || order.Status is "Expired" or "Cancelled")
                    orders.AddAudit(new AuditLog { EntityType = "Payment", EntityId = payment.PaymentId,
                        Action = $"{old}->Succeeded;{(order.Status is "Expired" or "Cancelled" ? "LateVerifiedZaloPay;" : "")}Query:{payment.GatewayTransactionId}",
                        CreatedAt = now.UtcDateTime });
            }
            order.LastChangedAtUtc = now.UtcDateTime;
            await orders.SaveAsync(ct);
            await invoices.ApplyFinancialChangeAsync(orderId, null, ct);
            return true;
        }, ct);
        if (changed)
        {
            var message = new RealtimeEvent(Guid.NewGuid(), "OrderChanged", orderId, clock.GetUtcNow());
            await realtime.PublishToPermissionAsync("order.view", message, CancellationToken.None);
            await realtime.PublishToAccountAsync(RealtimeAccountType.Customer, customerId, message, CancellationToken.None);
        }
    }
}
