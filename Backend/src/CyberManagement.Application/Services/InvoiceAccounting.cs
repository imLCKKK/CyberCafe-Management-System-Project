using System.Globalization;
using CyberManagement.Application.DTOs.Invoices;
using CyberManagement.Application.Exceptions;
using CyberManagement.Domain.Entities;

namespace CyberManagement.Application.Services;

// Chỉ đối soát nguồn đã ghi; không ghi hoặc tạo giao dịch tiền.
internal static class InvoiceAccounting
{
    public static InvoiceDto ToDto(Invoice invoice, IReadOnlyList<Payment> allPayments, IReadOnlyList<WalletTransaction> ledgers, IReadOnlyList<int> cancelledDetailIds)
    {
        var orders = invoice.InvoiceOrders.Select(x => x.OrderId).Order().ToArray();
        var charges = invoice.Details.Where(x => x.SessionChargeId != null).Select(x => x.SessionChargeId!.Value).ToArray();
        var payments = allPayments.Where(x => x.InvoiceId == invoice.InvoiceId
            || (x.OrderId != null && orders.Contains(x.OrderId.Value))
            || (x.SessionChargeId != null && charges.Contains(x.SessionChargeId.Value))).ToArray();
        if (payments.Any(x => x.InvoiceId != null && x.InvoiceId != invoice.InvoiceId))
            throw new BusinessRuleException("Khoản thu của nguồn đang gắn với hóa đơn khác, cần đối soát.");
        if (payments.Any(x => (x.OrderId is int orderId && !orders.Contains(orderId))
            || (x.SessionChargeId is int chargeId && !charges.Contains(chargeId))
            || (x.OrderDetailId is int detailId && !invoice.Details.Any(line => line.OrderDetailId == detailId))))
            throw new BusinessRuleException("Khoản tiền không thuộc đúng nguồn/dòng của hóa đơn, cần đối soát.");
        var totals = Totals(invoice.CustomerId, orders, charges, payments, ledgers);
        var cancelled = invoice.Details.Sum(x => x.CancelledAmount);
        var payable = invoice.TotalAmount - cancelled;
        if (payable < 0) throw new BusinessRuleException("Giá trị hủy vượt snapshot hóa đơn.");
        var allFreeLinesCancelled = invoice.TotalAmount == 0 && invoice.Details.Count > 0 && invoice.Details.All(x => x.OrderDetailId is int id && cancelledDetailIds.Contains(id));
        var status = invoice.Status == "Cancelled" || allFreeLinesCancelled || (invoice.TotalAmount > 0 && cancelled == invoice.TotalAmount)
            ? "Cancelled" : totals.Net >= payable ? "Paid" : "Pending";
        return new(invoice.InvoiceId, invoice.CustomerId, invoice.Customer?.FullName ?? $"KH{invoice.CustomerId}",
            orders, invoice.GamingSessionId, invoice.ActorType, invoice.EmployeeId,
            invoice.ActorType == "System" ? "Hệ thống" : invoice.Employee?.FullName ?? $"NV{invoice.EmployeeId}",
            new DateTimeOffset(DateTime.SpecifyKind(invoice.InvoiceDate, DateTimeKind.Utc)),
            invoice.Subtotal, invoice.Discount, invoice.TotalAmount, cancelled, totals.Refunded, totals.Net,
            Math.Max(0, payable - totals.Net), Math.Max(0, totals.Net - payable), status,
            invoice.Details.OrderBy(x => x.InvoiceDetailId).Select(x => new InvoiceLineDto(
                x.InvoiceDetailId, x.OrderDetailId, x.SessionChargeId, x.ItemType, x.Description, x.Quantity,
                x.UnitPrice, x.UnitPrice * x.Quantity, x.UnitPrice * x.Quantity - x.Amount, x.Amount,
                x.CancelledAmount, x.VoucherCodeSnapshot, x.VoucherDiscountTypeSnapshot, x.VoucherDiscountValueSnapshot)).ToArray(),
            payments.OrderBy(x => x.PaymentId).Select(x => new InvoicePaymentDto(x.PaymentId, x.Type,
                x.Method, x.Status, x.Amount, x.WalletTransactionId, x.PaymentTime,
                x.ConfirmedAt, x.ProviderOccurredAt, x.PaymentTimeBasis)).ToArray());
    }

    public static (decimal Net, decimal Refunded) Totals(int customerId, IReadOnlyList<int> orderIds,
        IReadOnlyList<int> chargeIds, IReadOnlyList<Payment> payments, IReadOnlyList<WalletTransaction> ledgers)
    {
        var ledgerById = ledgers.ToDictionary(x => x.TransactionId);
        var paymentById = payments.ToDictionary(x => x.PaymentId);
        decimal collected = 0, refunded = 0;
        foreach (var payment in payments)
        {
            if (payment.Status != "Succeeded") continue;
            if (payment.PaymentTime is null || payment.ConfirmedAt is null)
                throw new BusinessRuleException("Khoản thu/hoàn thiếu bằng chứng xác nhận.");
            if (payment.Type == "Refund")
            {
                if (payment.RefundOfPaymentId is not int originalId || !paymentById.TryGetValue(originalId, out var original)
                    || original.Type != "Payment" || original.Status != "Succeeded" || original.Method != payment.Method
                    || original.OrderId != payment.OrderId || original.SessionChargeId != payment.SessionChargeId)
                    throw new BusinessRuleException("Khoản hoàn không khớp khoản thu gốc.");
            }
            decimal amount;
            if (payment.Method == "Wallet")
            {
                if (payment.WalletTransactionId is not int ledgerId || !ledgerById.TryGetValue(ledgerId, out var ledger)
                    || ledger.CustomerId != customerId || Math.Abs(ledger.Amount) != payment.Amount)
                    throw new BusinessRuleException("Khoản ví chưa khớp ledger của khách hàng.");
                var isRefund = payment.Type == "Refund";
                var expectedSource = isRefund ? "OrderDetail" : payment.OrderId.HasValue ? "Order" : "SessionCharge";
                var sourceId = isRefund ? payment.OrderDetailId : payment.OrderId ?? payment.SessionChargeId;
                var expectedType = isRefund ? "OrderRefund" : payment.OrderId.HasValue ? "OrderPayment" : "GamingCharge";
                if (ledger.Kind != (isRefund ? "Refund" : "Debit") || ledger.Type != expectedType
                    || ledger.SourceType != expectedSource || sourceId is null
                    || ledger.SourceId != sourceId.Value.ToString(CultureInfo.InvariantCulture)
                    || (isRefund ? ledger.Amount <= 0 : ledger.Amount >= 0))
                    throw new BusinessRuleException("Loại, dấu hoặc nguồn ledger không khớp khoản ví dịch vụ.");
                if (isRefund && (ledger.OriginalDebitId != paymentById[payment.RefundOfPaymentId!.Value].WalletTransactionId
                    || ledger.OriginalDebitId is null))
                    throw new BusinessRuleException("Ledger hoàn không tham chiếu debit gốc.");
                // Chỉ lấy tiền từ ledger, không cộng thêm Amount của Payment đối soát.
                amount = Math.Abs(ledger.Amount);
            }
            else if (payment.Method is "Cash" or "ZaloPay") amount = payment.Amount;
            else throw new BusinessRuleException("Phương thức thanh toán chưa được hỗ trợ.");
            if (payment.Type == "Payment") collected += amount;
            else if (payment.Type == "Refund") refunded += amount;
            else throw new BusinessRuleException("Loại giao dịch tiền không hợp lệ.");
        }
        foreach (var original in payments.Where(x => x.Type == "Payment"))
        {
            var reserved = payments.Where(x => x.RefundOfPaymentId == original.PaymentId
                && x.Status is "Succeeded" or "Pending").Sum(x => x.Amount);
            if (reserved > original.Amount) throw new BusinessRuleException("Tổng hoàn/giữ hoàn vượt khoản thu gốc.");
        }
        var orderStrings = orderIds.Select(x => x.ToString(CultureInfo.InvariantCulture)).ToArray();
        var chargeStrings = chargeIds.Select(x => x.ToString(CultureInfo.InvariantCulture)).ToArray();
        var originalLedgers = ledgers.Where(x => (x.SourceType == "Order" && orderStrings.Contains(x.SourceId))
            || (x.SourceType == "SessionCharge" && chargeStrings.Contains(x.SourceId))).Select(x => x.TransactionId).ToArray();
        foreach (var ledger in ledgers.Where(x => originalLedgers.Contains(x.TransactionId)
            || (x.OriginalDebitId != null && originalLedgers.Contains(x.OriginalDebitId.Value))))
        {
            if (!payments.Any(x => x.Method == "Wallet" && x.Status == "Succeeded" && x.WalletTransactionId == ledger.TransactionId))
                throw new BusinessRuleException("Có ledger dịch vụ chưa có Payment đối soát, cần xử lý nguồn trước khi phát hành.");
        }
        if (refunded > collected) throw new BusinessRuleException("Tiền hoàn vượt khoản thực thu của hóa đơn.");
        return (collected - refunded, refunded);
    }
}
