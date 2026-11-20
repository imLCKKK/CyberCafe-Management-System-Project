using System.Globalization;
using CyberManagement.Application.DTOs.Revenue;
using CyberManagement.Application.Interfaces.Repositories;
using CyberManagement.Domain.Entities;

namespace CyberManagement.Application.Services;

internal sealed record RevenueEntry(string Source, RevenueTransactionDto Transaction);
internal sealed record RevenueMoney(IReadOnlyList<RevenueEntry> Entries,
    IReadOnlyList<RevenueTransactionDto> Transactions, IReadOnlyList<ReconciliationIssueDto> Issues,
    decimal TopUpCollectedAmount);

// Chuẩn hóa nguồn thu/hoàn một lần; Payment ví chỉ dùng kiểm tra liên kết ledger.
internal static class RevenueAccounting
{
    private sealed record Source(string Name, int CustomerId, int? OrderId, int? DetailId, int? ChargeId);

    public static RevenueMoney Analyze(RevenueReadSet data, DateTimeOffset from, DateTimeOffset to)
    {
        var issues = new List<ReconciliationIssueDto>();
        var entries = new List<RevenueEntry>();
        var transactions = new List<RevenueTransactionDto>();
        var orders = data.Orders.ToDictionary(x => x.OrderId);
        var charges = data.SessionCharges.ToDictionary(x => x.ChargeId);
        var details = data.Orders.SelectMany(x => x.Details).ToDictionary(x => x.OrderDetailId);
        var payments = data.Payments.ToDictionary(x => x.PaymentId);
        var wallets = data.WalletTransactions.ToDictionary(x => x.TransactionId);
        var mirrors = data.Payments.Where(x => x.WalletTransactionId != null)
            .ToLookup(x => x.WalletTransactionId!.Value);

        Source? PaymentSource(Payment p)
        {
            if ((p.OrderId != null) == (p.SessionChargeId != null)) return null;
            if (p.OrderId is int orderId && orders.TryGetValue(orderId, out var order))
            {
                if (p.OrderDetailId is int detailId && (!details.TryGetValue(detailId, out var detail) || detail.OrderId != orderId)) return null;
                return new("Food", order.CustomerId, orderId, p.OrderDetailId, null);
            }
            if (p.SessionChargeId is int chargeId && charges.TryGetValue(chargeId, out var charge) && charge.GamingSession is not null)
                return new("Gaming", charge.GamingSession.CustomerId, null, null, chargeId);
            return null;
        }
        Source? WalletSource(WalletTransaction w)
        {
            Source? source = null;
            if (w.SourceType == "Order" && Id(w.SourceId, out var orderId) && orders.TryGetValue(orderId, out var order))
                source = new("Food", order.CustomerId, orderId, null, null);
            else if (w.SourceType == "SessionCharge" && Id(w.SourceId, out var chargeId)
                && charges.TryGetValue(chargeId, out var charge) && charge.GamingSession is not null)
                source = new("Gaming", charge.GamingSession.CustomerId, null, null, chargeId);
            else if (w.SourceType == "OrderDetail")
            {
                var parts = w.SourceId?.Split(':');
                if (parts is { Length: 2 } && Id(parts[0], out var oid) && Id(parts[1], out var did)
                    && details.TryGetValue(did, out var detail) && detail.OrderId == oid && orders.TryGetValue(oid, out var parent))
                    source = new("Food", parent.CustomerId, oid, did, null);
            }
            // Một số nguồn đã tích hợp lưu riêng ID dòng; chỉ chấp nhận khi FK Payment chứng minh đủ đơn/dòng.
            if (source is null && w.SourceType == "OrderDetail" && Id(w.SourceId, out var legacyDetailId))
            {
                var linked = mirrors[w.TransactionId].ToArray();
                if (linked.Length == 1 && linked[0].Method == "Wallet" && linked[0].Type == "Refund"
                    && linked[0].OrderDetailId == legacyDetailId) source = PaymentSource(linked[0]);
            }
            // Dữ liệu cũ thiếu metadata chỉ truy nguồn qua FK Payment, không đoán từ Description.
            if (source is null && w.SourceType is null && w.SourceId is null)
            {
                var linked = mirrors[w.TransactionId].ToArray();
                if (linked.Length == 1 && linked[0].Method == "Wallet") source = PaymentSource(linked[0]);
            }
            return source?.CustomerId == w.CustomerId ? source : null;
        }
        void AddIssue(string code, string message, string entity, int id) => issues.Add(Issue(code, message, entity, id));

        foreach (var wallet in data.WalletTransactions)
        {
            if (wallet.Type is "TopUp" or "TournamentFee") continue;
            var source = WalletSource(wallet);
            var linked = mirrors[wallet.TransactionId].ToArray();
            var mirror = linked.Length == 1 ? linked[0] : null;
            var refund = wallet.Type == "OrderRefund";
            var type = refund ? "Refund" : "Payment";
            var time = Utc(wallet.CreatedAt);
            var row = new RevenueTransactionDto("WalletTransaction", wallet.TransactionId, source?.OrderId, source?.DetailId,
                source?.ChargeId, "Wallet", type, "Succeeded", Math.Abs(wallet.Amount), time,
                mirror?.ConfirmedAt, mirror?.ProviderOccurredAt, "Source");
            transactions.Add(row);
            var valid = true;
            if (source is null || (wallet.Type == "OrderPayment" && source.Name != "Food")
                || (wallet.Type == "OrderRefund" && (source.Name != "Food" || source.DetailId is null))
                || (wallet.Type == "GamingCharge" && source.Name != "Gaming"))
            { AddIssue("WalletSourceInvalid", "Ledger ví thiếu hoặc sai nguồn đơn/phí phiên.", "WalletTransaction", wallet.TransactionId); valid = false; }
            if (wallet.Type is not ("OrderPayment" or "OrderRefund" or "GamingCharge")
                || !Positive(Math.Abs(wallet.Amount)) || (refund ? wallet.Amount <= 0 : wallet.Amount >= 0)
                || (wallet.Kind is not null && wallet.Kind != (refund ? "Refund" : "Debit")) || time is null)
            { AddIssue("WalletValuesInvalid", "Type, dấu tiền hoặc thời gian ledger không hợp lệ.", "WalletTransaction", wallet.TransactionId); valid = false; }
            if (refund && (wallet.OriginalDebitId is not int originalId || !wallets.TryGetValue(originalId, out var original)
                || original.CustomerId != wallet.CustomerId || original.Type != "OrderPayment" || original.Amount >= 0
                || !SameSource(source, WalletSource(original))))
            { AddIssue("WalletRefundInvalid", "Khoản hoàn ví không khớp debit gốc.", "WalletTransaction", wallet.TransactionId); valid = false; }
            if (linked.Length > 1 || (mirror is not null && (mirror.Method != "Wallet" || mirror.Status != "Succeeded"
                || mirror.Type != type || mirror.Amount != Math.Abs(wallet.Amount) || !SameSource(source, PaymentSource(mirror))
                || (mirror.OrderDetailId is not null && mirror.OrderDetailId != source?.DetailId)
                || !WalletTimeMatches(mirror, time))))
            { AddIssue("WalletLinkInvalid", "Payment ví không khớp nguồn, số tiền hoặc thời gian ledger.", "WalletTransaction", wallet.TransactionId); valid = false; }
            if (refund && mirror is not null && (mirror.RefundOfPaymentId is not int originalPaymentId
                || !payments.TryGetValue(originalPaymentId, out var originalPayment) || originalPayment.Type != "Payment"
                || originalPayment.Status != "Succeeded" || originalPayment.Method != "Wallet"
                || originalPayment.WalletTransactionId != wallet.OriginalDebitId))
            { AddIssue("WalletLinkInvalid", "Payment hoàn ví không truy đúng khoản thu/debit gốc.", "Payment", mirror.PaymentId); valid = false; }
            if (valid) entries.Add(new(source!.Name, row));
        }

        foreach (var payment in data.Payments)
        {
            if (payment.Method == "Wallet")
            {
                if (payment.Status == "Succeeded" && (payment.WalletTransactionId is not int wid || !wallets.TryGetValue(wid, out var ledger)
                    || ledger.Type is not ("OrderPayment" or "OrderRefund" or "GamingCharge")))
                    AddIssue("WalletLinkInvalid", "Payment ví thành công thiếu ledger liên kết.", "Payment", payment.PaymentId);
                continue;
            }
            var source = PaymentSource(payment);
            var row = new RevenueTransactionDto("Payment", payment.PaymentId, payment.OrderId, payment.OrderDetailId,
                payment.SessionChargeId, payment.Method, payment.Type, payment.Status, payment.Amount,
                payment.PaymentTime, payment.ConfirmedAt, payment.ProviderOccurredAt, payment.PaymentTimeBasis);
            transactions.Add(row);
            if (payment.Status != "Succeeded") continue;
            var valid = true;
            if (source is null)
            { AddIssue("PaymentSourceInvalid", "Khoản thu/hoàn thiếu hoặc sai nguồn.", "Payment", payment.PaymentId); valid = false; }
            if (payment.Method is not ("Cash" or "ZaloPay") || payment.Type is not ("Payment" or "Refund") || !Positive(payment.Amount))
            { AddIssue("PaymentValuesInvalid", "Phương thức, loại hoặc số tiền không hợp lệ.", "Payment", payment.PaymentId); valid = false; }
            if (!ValidTime(payment))
            { AddIssue("PaymentTimeInvalid", "Khoản thành công thiếu căn cứ thời gian thu/hoàn.", "Payment", payment.PaymentId); valid = false; }
            if (payment.Type == "Refund" && (payment.RefundOfPaymentId is not int originalId
                || !payments.TryGetValue(originalId, out var original) || original.Type != "Payment" || original.Status != "Succeeded"
                || original.Method != payment.Method || !SameSource(source, PaymentSource(original))))
            { AddIssue("RefundSourceInvalid", "Khoản hoàn không khớp khoản thu gốc.", "Payment", payment.PaymentId); valid = false; }
            if (valid) entries.Add(new(source!.Name, row));
        }

        decimal topUp = 0;
        foreach (var request in data.CounterTopUps.Where(x => x.Status == "Approved"))
        {
            if (request.ApprovedAt is null)
            { AddIssue("TopUpEvidenceInvalid", "Khoản nạp đã duyệt thiếu giờ xác nhận.", "CounterTopUpRequest", request.RequestId); continue; }
            if (!RevenueService.InPeriod(Utc(request.ApprovedAt.Value), from, to)) continue;
            if (!Positive(request.Amount) || request.BonusAmount < 0 || request.CashAmountReceived != request.Amount
                || request.TotalAmount != request.Amount + request.BonusAmount || request.WalletTransactionId is not int wid
                || !wallets.TryGetValue(wid, out var ledger) || ledger.Type != "TopUp" || ledger.Kind != "Credit"
                || ledger.CustomerId != request.CustomerId || ledger.Amount != request.TotalAmount
                || ledger.SourceType != "CounterTopUp" || ledger.SourceId != request.RequestId.ToString(CultureInfo.InvariantCulture))
            { AddIssue("TopUpEvidenceInvalid", "Tiền thực nhận, tiền tặng hoặc ledger nạp không khớp.", "CounterTopUpRequest", request.RequestId); continue; }
            topUp += request.CashAmountReceived!.Value;
        }
        foreach (var wallet in data.WalletTransactions.Where(x => x.Type == "TopUp"
            && RevenueService.InPeriod(Utc(x.CreatedAt), from, to)))
        {
            if (wallet.SourceType == "CounterTopUp")
            {
                if (!data.CounterTopUps.Any(x => x.WalletTransactionId == wallet.TransactionId && x.Status == "Approved"))
                    AddIssue("TopUpEvidenceInvalid", "Ledger nạp quầy thiếu phiếu đã duyệt.", "WalletTransaction", wallet.TransactionId);
            }
            else if (wallet.SourceType == "ZaloPayTopUp" && wallet.Kind == "Credit" && Positive(wallet.Amount)
                && !string.IsNullOrWhiteSpace(wallet.SourceId)) topUp += wallet.Amount;
            else AddIssue("TopUpSourceInvalid", "Chưa xác định được nguồn thực nhận của khoản nạp.", "WalletTransaction", wallet.TransactionId);
        }
        return new(entries, transactions.OrderBy(x => x.PaymentTime).ThenBy(x => x.EntityType).ThenBy(x => x.EntityId).ToArray(), issues, topUp);
    }

    private static bool SameSource(Source? a, Source? b) => a is not null && b is not null
        && a.CustomerId == b.CustomerId && a.OrderId == b.OrderId && a.ChargeId == b.ChargeId;
    private static bool Id(string? value, out int id) => int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out id) && id > 0;
    private static bool Positive(decimal amount) => amount > 0 && amount == decimal.Truncate(amount);
    internal static DateTimeOffset? Utc(DateTime value) => value == default ? null
        : new DateTimeOffset(DateTime.SpecifyKind(value, DateTimeKind.Utc));
    private static bool WalletTimeMatches(Payment mirror, DateTimeOffset? ledgerTime) => ledgerTime is not null
        && mirror.PaymentTime == ledgerTime && mirror.ConfirmedAt is not null
        && (mirror.PaymentTimeBasis == "Source" || (mirror.PaymentTimeBasis == "Confirmation" && mirror.PaymentTime == mirror.ConfirmedAt));
    private static bool ValidTime(Payment payment) => payment.PaymentTime is not null && payment.PaymentTime.Value != default
        && payment.ConfirmedAt is not null && payment.ConfirmedAt.Value != default
        && (payment.PaymentTimeBasis == "Confirmation"
            ? payment.Method == "ZaloPay" && payment.PaymentTime == payment.ConfirmedAt
            : payment.PaymentTimeBasis == "Source" && (payment.Method == "Cash"
                ? payment.PaymentTime == payment.ConfirmedAt
                : payment.ProviderOccurredAt is not null && payment.PaymentTime == payment.ProviderOccurredAt));

    internal static ReconciliationIssueDto Issue(string code, string message, string entity, int id,
        decimal? expected = null, decimal? actual = null, string? unit = null) =>
        new(code, message, expected, actual, unit, [new(entity, id)]);
}
