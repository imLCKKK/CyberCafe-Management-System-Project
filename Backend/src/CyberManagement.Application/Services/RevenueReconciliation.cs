using CyberManagement.Application.DTOs.Revenue;
using CyberManagement.Application.Interfaces.Repositories;
using CyberManagement.Domain.Entities;

namespace CyberManagement.Application.Services;

// Đối chiếu snapshot và liên kết đã lưu; không ghi hoặc sửa dữ liệu nghiệp vụ.
internal static class RevenueReconciliation
{
    public static IReadOnlyList<ReconciliationIssueDto> Check(RevenueReadSet data, RevenueMoney money)
    {
        var issues = money.Issues.ToList();
        var orderMap = data.Orders.ToDictionary(x => x.OrderId);
        var detailMap = data.Orders.SelectMany(x => x.Details).ToDictionary(x => x.OrderDetailId);
        var chargeMap = data.SessionCharges.ToDictionary(x => x.ChargeId);
        var entries = money.Entries.Select(x => x.Transaction).ToArray();
        void Issue(string code, string message, string entity, int id, decimal? expected = null, decimal? actual = null, string? unit = null) =>
            issues.Add(RevenueAccounting.Issue(code, message, entity, id, expected, actual, unit));
        static decimal Net(IEnumerable<RevenueTransactionDto> rows) => rows.Sum(x => x.Type == "Payment" ? x.Amount : -x.Amount);

        // Một Payment ví là bản đối soát của đúng một ledger, không phải nguồn tiền thứ hai.
        foreach (var row in entries.Where(x => x.EntityType == "WalletTransaction"))
            if (data.Payments.Count(x => x.WalletTransactionId == row.EntityId && x.Method == "Wallet" && x.Status == "Succeeded") != 1)
                Issue("WalletMirrorMissing", "Ledger dịch vụ thiếu Payment đối soát duy nhất.", "WalletTransaction", row.EntityId);
        foreach (var original in data.Payments.Where(x => x.Type == "Payment" && x.Status == "Succeeded"))
        {
            var reserved = data.Payments.Where(x => x.RefundOfPaymentId == original.PaymentId
                && x.Type == "Refund" && x.Status is "Pending" or "Succeeded").Sum(x => x.Amount);
            if (reserved > original.Amount)
                Issue("RefundLimitExceeded", "Tổng hoàn thành công và đang giữ chỗ vượt khoản thu gốc.", "Payment", original.PaymentId, original.Amount, reserved, "VND");
        }
        foreach (var original in data.WalletTransactions.Where(x => x.Amount < 0))
        {
            var refunded = data.WalletTransactions.Where(x => x.OriginalDebitId == original.TransactionId).Sum(x => x.Amount);
            if (refunded > -original.Amount)
                Issue("WalletRefundLimitExceeded", "Tổng hoàn ledger vượt debit gốc.", "WalletTransaction", original.TransactionId, -original.Amount, refunded, "VND");
        }

        foreach (var order in data.Orders)
        {
            var closed = order.Status is "Cancelled" or "Expired";
            if ((closed && order.Details.Any(x => x.Status != "Cancelled"))
                || (!closed && order.Details.Count > 0 && order.Details.All(x => x.Status == "Cancelled")))
                Issue("OrderStatusMismatch", "Trạng thái đơn không khớp các dòng đã hủy.", "Order", order.OrderId);
            var gross = order.Details.Sum(x => x.UnitPrice * x.Quantity);
            var discount = order.Details.Sum(x => x.DiscountAmount);
            if (order.Details.Count == 0 || order.Details.Any(x => x.Quantity <= 0 || x.UnitPrice < 0 || x.DiscountAmount < 0 || x.DiscountAmount > x.UnitPrice * x.Quantity)
                || order.TotalAmount != gross - discount || order.DiscountAmount != discount)
                Issue("OrderSnapshotMismatch", "Tổng đơn hoặc giảm giá không khớp snapshot dòng món.", "Order", order.OrderId, gross - discount, order.TotalAmount, "VND");
            var sourceEntries = entries.Where(x => x.OrderId == order.OrderId).ToArray();
            var collected = sourceEntries.Where(x => x.Type == "Payment").Sum(x => x.Amount);
            if (collected > order.TotalAmount)
                Issue("OrderOverCollected", "Khoản đã thu vượt giá trị gốc của đơn; cần xử lý tiền dư.", "Order", order.OrderId, order.TotalAmount, collected, "VND");
            if (closed && Net(sourceEntries) != 0)
                Issue("ClosedOrderMoneyUnresolved", "Đơn đã hủy/hết hạn còn tiền thu chưa hoàn hoặc hoàn vượt thu.", "Order", order.OrderId, 0, Net(sourceEntries), "VND");
            var usages = data.PromotionUsages.Where(x => x.OrderId == order.OrderId).ToArray();
            if (order.VoucherCodeSnapshot is not null)
            {
                if (usages.Length != 1 || usages.Any(x => x.CustomerId != order.CustomerId))
                    Issue("VoucherUsageMismatch", "Snapshot voucher không có lượt dùng duy nhất đúng khách.", "Order", order.OrderId, 1, usages.Length, "Count");
                foreach (var usage in usages)
                    if ((closed && usage.ReleasedAt is null) || (!closed && usage.ReleasedAt is not null)
                        || usage.ReleasedAt < usage.UsedAt)
                        Issue("VoucherReleaseMismatch", "Trạng thái hoàn lượt không khớp trạng thái đơn hoặc thời gian dùng.", "PromotionUsage", usage.PromotionUsageId);
            }
            else if (usages.Length != 0)
                Issue("VoucherUsageMismatch", "Đơn không có snapshot voucher nhưng có lịch sử sử dụng.", "Order", order.OrderId);
            foreach (var detail in order.Details)
            {
                var cancelled = closed || detail.Status == "Cancelled";
                if (detail.Status is not ("Pending" or "Confirmed" or "Cancelled"))
                    Issue("OrderDetailStatusInvalid", "Trạng thái dòng món không hợp lệ.", "OrderDetail", detail.OrderDetailId);
                var movements = data.InventoryTransactions.Where(x => x.OrderDetailId == detail.OrderDetailId).ToArray();
                var sales = movements.Where(x => x.Type == "Sale").ToArray();
                var returns = movements.Where(x => x.Type == "Return").ToArray();
                if (sales.Length != 1 || sales.Sum(x => x.Quantity) != -detail.Quantity)
                    Issue("StockSaleMismatch", "Dòng món thiếu hoặc sai biến động xuất kho.", "OrderDetail", detail.OrderDetailId, -detail.Quantity, sales.Sum(x => x.Quantity), "Quantity");
                if (returns.Length > 1 || (returns.Length == 1 && (returns[0].Quantity != detail.Quantity || sales.Length != 1))
                    || (cancelled && returns.Length != 1))
                    Issue("StockReturnMismatch", "Hoàn tồn thiếu, lặp hoặc không khớp số lượng đã xuất.", "OrderDetail", detail.OrderDetailId, cancelled ? detail.Quantity : null, returns.Sum(x => x.Quantity), "Quantity");
                if (!cancelled && returns.Length != 0)
                    Issue("StockReturnUnexpected", "Dòng món chưa hủy nhưng đã hoàn tồn.", "OrderDetail", detail.OrderDetailId, 0, returns.Sum(x => x.Quantity), "Quantity");
                foreach (var movement in movements)
                    if (movement.OrderId != order.OrderId || movement.ProductId != detail.ProductId
                        || movement.Before < 0 || movement.After < 0 || (long)movement.Before + movement.Quantity != movement.After
                        || movement.Type is not ("Sale" or "Return"))
                        Issue("StockValuesInvalid", "Nguồn hoặc tồn trước/sau của biến động không hợp lệ.", "InventoryTransaction", movement.InvTransId);
            }
        }
        foreach (var movement in data.InventoryTransactions)
            if (movement.OrderDetailId is not int id || !detailMap.ContainsKey(id))
                Issue("StockSourceMissing", "Biến động theo đơn không truy được dòng món.", "InventoryTransaction", movement.InvTransId);
        foreach (var charge in data.SessionCharges)
        {
            var collected = entries.Where(x => x.SessionChargeId == charge.ChargeId && x.Type == "Payment").Sum(x => x.Amount);
            if (charge.Amount < 0 || collected != charge.Amount)
                Issue("SessionChargeMoneyMismatch", "Phí phiên không khớp khoản thực thu theo nguồn.", "SessionCharge", charge.ChargeId, charge.Amount, collected, "VND");
        }

        // Đọc snapshot đã lưu, không lấy tên/giá/voucher hiện tại để tính lại hóa đơn cũ.
        foreach (var invoice in data.Invoices)
        {
            var gross = invoice.Details.Sum(x => x.UnitPrice * x.Quantity);
            var total = invoice.Details.Sum(x => x.Amount);
            if (invoice.Details.Count == 0 || invoice.Subtotal != gross || invoice.Discount != gross - total || invoice.TotalAmount != total)
                Issue("InvoiceSnapshotMismatch", "Tổng hóa đơn không khớp các dòng snapshot.", "Invoice", invoice.InvoiceId, total, invoice.TotalAmount, "VND");
            foreach (var line in invoice.Details)
            {
                if ((line.OrderDetailId != null) == (line.SessionChargeId != null))
                { Issue("InvoiceSourceInvalid", "Dòng hóa đơn phải có đúng một nguồn món hoặc phí phiên.", "InvoiceDetail", line.InvoiceDetailId); continue; }
                if (line.OrderDetailId is int detailId)
                {
                    if (!detailMap.TryGetValue(detailId, out var detail) || !orderMap.TryGetValue(detail.OrderId, out var order)
                        || order.CustomerId != invoice.CustomerId || !invoice.InvoiceOrders.Any(x => x.OrderId == order.OrderId))
                        Issue("InvoiceSourceInvalid", "Dòng hóa đơn món không khớp đơn/khách/liên kết nguồn.", "InvoiceDetail", line.InvoiceDetailId);
                    else
                    {
                        if (line.Quantity != detail.Quantity || line.UnitPrice != detail.UnitPrice || line.Amount != detail.Quantity * detail.UnitPrice - detail.DiscountAmount)
                            Issue("InvoiceSnapshotMismatch", "Snapshot dòng hóa đơn khác snapshot dòng món.", "InvoiceDetail", line.InvoiceDetailId);
                        var cancelled = detail.Status == "Cancelled" || order.Status is "Cancelled" or "Expired";
                        var expectedCancelled = cancelled ? line.Amount : 0;
                        if (line.CancelledAmount != expectedCancelled)
                            Issue("InvoiceCancellationMismatch", "Snapshot tiền hủy không khớp trạng thái dòng món.", "InvoiceDetail", line.InvoiceDetailId, expectedCancelled, line.CancelledAmount, "VND");
                        if (line.VoucherCodeSnapshot != order.VoucherCodeSnapshot
                            || line.VoucherDiscountTypeSnapshot != order.VoucherDiscountTypeSnapshot
                            || line.VoucherDiscountValueSnapshot != order.VoucherDiscountValueSnapshot)
                            Issue("InvoiceVoucherSnapshotMismatch", "Snapshot voucher trên hóa đơn khác snapshot đơn.", "InvoiceDetail", line.InvoiceDetailId);
                    }
                }
                else if (line.SessionChargeId is int chargeId)
                {
                    if (!chargeMap.TryGetValue(chargeId, out var charge) || charge.GamingSession?.CustomerId != invoice.CustomerId
                        || charge.GamingSessionId != invoice.GamingSessionId)
                        Issue("InvoiceSourceInvalid", "Dòng phí phiên không khớp phiên hoặc khách.", "InvoiceDetail", line.InvoiceDetailId);
                    else if (line.Quantity != 1 || line.UnitPrice != charge.Amount || line.Amount != charge.Amount)
                        Issue("InvoiceSnapshotMismatch", "Snapshot phí phiên không khớp khoản phí gốc.", "InvoiceDetail", line.InvoiceDetailId);
                    if (line.CancelledAmount != 0)
                        Issue("InvoiceCancellationMismatch", "Dòng phí phiên không có nguồn hủy món để ghi tiền hủy.", "InvoiceDetail", line.InvoiceDetailId, 0, line.CancelledAmount, "VND");
                }
            }
            foreach (var link in invoice.InvoiceOrders)
                if (!orderMap.TryGetValue(link.OrderId, out var order) || order.CustomerId != invoice.CustomerId
                    || order.Details.Any(d => !invoice.Details.Any(x => x.OrderDetailId == d.OrderDetailId)))
                    Issue("InvoiceSourceInvalid", "Hóa đơn thiếu snapshot của đơn đã liên kết.", "Invoice", invoice.InvoiceId);
            // Nghĩa vụ dùng snapshot tiền hủy, độc lập với khoản thực hoàn và lịch sử kho.
            var remaining = invoice.TotalAmount - invoice.Details.Sum(x => x.CancelledAmount);
            if (invoice.Details.Any(x => x.CancelledAmount < 0 || x.CancelledAmount > x.Amount) || remaining < 0)
                Issue("InvoiceCancellationMismatch", "Tiền hủy vượt snapshot hoặc làm nghĩa vụ âm.", "Invoice", invoice.InvoiceId);
            var actualNet = Net(entries.Where(x => invoice.InvoiceOrders.Any(y => y.OrderId == x.OrderId)
                || invoice.Details.Any(y => y.SessionChargeId != null && y.SessionChargeId == x.SessionChargeId)));
            if (invoice.Status == "Paid" && actualNet < remaining)
                Issue("InvoicePaidAmountMismatch", "Hóa đơn Paid chưa thu đủ phần nghĩa vụ còn hiệu lực.",
                    "Invoice", invoice.InvoiceId, remaining, actualNet, "VND");
            if (actualNet > remaining)
                Issue(invoice.Details.Any(x => x.CancelledAmount > 0) ? "InvoiceRefundUnresolved" : "InvoiceOverCollected",
                    "Tiền thực thu ròng vượt nghĩa vụ còn lại; cần đối soát khoản dư/hoàn chưa xong.",
                    "Invoice", invoice.InvoiceId, 0, actualNet - remaining, "VND");
            if (invoice.Status == "Paid" && remaining > 0 && !entries.Any(x =>
                x.Type == "Payment" && (invoice.InvoiceOrders.Any(y => y.OrderId == x.OrderId)
                    || invoice.Details.Any(y => y.SessionChargeId != null && y.SessionChargeId == x.SessionChargeId))))
                Issue("InvoiceMoneyMissing", "Hóa đơn Paid không có nguồn tiền đã xác định.", "Invoice", invoice.InvoiceId);
        }
        foreach (var payment in data.Payments.Where(x => x.InvoiceId != null || x.Status == "Succeeded"))
        {
            var sourceInvoices = data.Invoices.Where(x =>
                (payment.OrderId is int oid && x.InvoiceOrders.Any(y => y.OrderId == oid))
                || (payment.SessionChargeId is int cid && x.Details.Any(y => y.SessionChargeId == cid))).ToArray();
            var invoice = data.Invoices.SingleOrDefault(x => x.InvoiceId == payment.InvoiceId);
            if (sourceInvoices.Length > 1 || (payment.InvoiceId is null && sourceInvoices.Length != 0)
                || (payment.InvoiceId is not null && (invoice is null || !sourceInvoices.Contains(invoice))))
                Issue("PaymentInvoiceMismatch", "Payment chưa gắn hoặc gắn sai hóa đơn của nguồn.", "Payment", payment.PaymentId);
        }
        foreach (var group in data.Invoices.SelectMany(x => x.Details).GroupBy(x => (x.OrderDetailId, x.SessionChargeId)))
            if (group.Count() > 1)
                foreach (var line in group) Issue("InvoiceSourceDuplicate", "Nguồn xuất hiện trên nhiều dòng hóa đơn.", "InvoiceDetail", line.InvoiceDetailId);
        return issues.OrderBy(x => x.Code, StringComparer.Ordinal).ThenBy(x => x.References[0].EntityType)
            .ThenBy(x => x.References[0].EntityId).ToArray();
    }
}
