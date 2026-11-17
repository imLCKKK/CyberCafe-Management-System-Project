using System.Globalization;
using System.Text.Encodings.Web;
using CyberManagement.Application.DTOs.Invoices;

namespace CyberManagement.API.Controller;

// Bản in chỉ dùng snapshot của DTO đã kiểm tra quyền, không đọc lại giá món.
internal static class InvoicePrint
{
    public const string ContentPolicy = "default-src 'none'; style-src 'unsafe-inline'; frame-ancestors 'self'; base-uri 'none'; form-action 'none'";
    public static string Render(InvoiceDto invoice)
    {
        static string H(string? value) => HtmlEncoder.Default.Encode(value ?? "");
        static string Money(decimal value) => value.ToString("N0", CultureInfo.GetCultureInfo("vi-VN")) + " đ";
        static string Time(DateTimeOffset? value) => value.HasValue
            ? H(value.Value.ToOffset(TimeSpan.FromHours(7)).ToString("dd/MM/yyyy HH:mm:ss 'UTC+07:00'", CultureInfo.InvariantCulture))
            : "Chưa có";
        var rows = string.Join("", invoice.Lines.Select(line => $"<tr><td>{H(line.Description)}<small>{H(line.VoucherCode)}</small></td><td>{line.Quantity}</td><td>{Money(line.UnitPrice)}</td><td>{Money(line.DiscountAmount)}</td><td>{Money(line.Amount)}</td><td>{Money(line.CancelledAmount)}</td></tr>"));
        var paymentRows = string.Join("", invoice.Payments.Select(payment =>
        {
            var timing = "";
            if (payment.Status == "Succeeded")
            {
                var label = payment.PaymentTimeBasis == "Confirmation"
                    ? "Ghi nhận theo thời điểm hệ thống xác nhận" : "Ghi nhận theo thời điểm nguồn";
                timing = $"<small>{label}: {Time(payment.PaymentTime)}</small><small>Hệ thống xác nhận: {Time(payment.ConfirmedAt)}</small>";
                if (payment.Method == "ZaloPay")
                {
                    timing += payment.ProviderOccurredAt.HasValue
                        ? $"<small>Thời điểm giao dịch từ nhà cung cấp: {Time(payment.ProviderOccurredAt)}</small>"
                        : "<small>Chưa có thời điểm giao dịch từ nhà cung cấp.</small>";
                    if (payment.PaymentTimeBasis == "Confirmation")
                        timing += "<small>Mốc ghi nhận dùng thời điểm hệ thống xác nhận; không khẳng định thời điểm giao dịch thực tế.</small>";
                }
            }
            return $"<li>#{payment.Id} · {(payment.Type == "Refund" ? "Hoàn" : "Thu")} {H(payment.Method)} · {Money(payment.Amount)} · {H(payment.Status)}{timing}</li>";
        }));
        return $$"""
            <!doctype html><html lang="vi"><head><meta charset="utf-8"><title>Hóa đơn #{{invoice.Id}}</title>
            <style>@page{size:A4;margin:16mm}body{font:14px/1.5 Arial,sans-serif;color:#182026;margin:24px}h1{font-size:24px}small{display:block;color:#52606d}table{width:100%;border-collapse:collapse;margin:24px 0}th,td{padding:9px;border-bottom:1px solid #ccd2d8;text-align:right}th:first-child,td:first-child{text-align:left}.totals{margin-left:auto;max-width:400px}.totals p{display:flex;justify-content:space-between;gap:24px}footer{margin-top:32px;color:#52606d}@media print{body{margin:0} }</style></head>
            <body><header><strong>NEXORA CYBER</strong><h1>Hóa đơn #{{invoice.Id}}</h1><p>Khách hàng: {{H(invoice.CustomerName)}} · KH{{invoice.CustomerId}}</p><p>Phát hành: {{H(invoice.IssuedAt.ToString("yyyy-MM-dd HH:mm 'UTC'", CultureInfo.InvariantCulture))}} · {{H(invoice.IssuedBy)}}</p><p>Trạng thái: {{H(invoice.Status)}} · Đơn: {{H(string.Join(", ", invoice.OrderIds))}} · Phiên: {{invoice.GamingSessionId}}</p></header>
            <table><thead><tr><th>Nội dung</th><th>SL</th><th>Đơn giá</th><th>Giảm</th><th>Sau giảm</th><th>Đã hủy</th></tr></thead><tbody>{{rows}}</tbody></table>
            <section class="totals"><p>Trước giảm <strong>{{Money(invoice.Subtotal)}}</strong></p><p>Giảm giá <strong>{{Money(invoice.Discount)}}</strong></p><p>Giá trị lúc phát hành <strong>{{Money(invoice.GrossAmount)}}</strong></p><p>Giá trị đã hủy <strong>{{Money(invoice.CancelledAmount)}}</strong></p><p>Đã hoàn thành công <strong>{{Money(invoice.RefundAmount)}}</strong></p><p>Đã thu ròng <strong>{{Money(invoice.PaidAmount)}}</strong></p><p>Còn phải thu <strong>{{Money(invoice.AmountDue)}}</strong></p><p>Tiền dư / cần đối soát <strong>{{Money(invoice.OverpaidAmount)}}</strong></p></section>
            {{(invoice.Payments.Count > 0 ? "<section><h2>Giao dịch thu / hoàn</h2><ul>" + paymentRows + "</ul></section>" : "")}}
            <footer>Chứng từ thanh toán nội bộ. Giá và giảm giá được giữ theo thời điểm phát hành. Khoản hoàn đang chờ chưa được tính là đã hoàn.</footer></body></html>
            """;
    }
}
