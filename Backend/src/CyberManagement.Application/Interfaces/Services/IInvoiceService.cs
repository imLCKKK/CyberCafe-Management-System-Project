using CyberManagement.Application.DTOs.Invoices;

namespace CyberManagement.Application.Interfaces.Services;

public interface IInvoiceService
{
    Task<IReadOnlyList<InvoiceDto>> GetAllAsync(int employeeId, CancellationToken ct = default);
    Task<InvoiceDto> GetByIdAsync(int invoiceId, int employeeId, CancellationToken ct = default);
    Task<IReadOnlyList<InvoiceDto>> GetOwnAsync(int customerId, CancellationToken ct = default);
    Task<InvoiceDto> GetOwnByIdAsync(int invoiceId, int customerId, CancellationToken ct = default);
    Task<InvoiceDto> IssueAsync(IssueInvoiceRequest request, int employeeId, string operationKey, CancellationToken ct = default);
    // Callback/cash/refund gọi sau khi lưu nguồn trong transaction chung; không thu tiền tại đây.
    Task<InvoiceDto?> ApplyFinancialChangeAsync(int? orderId, int? sessionChargeId, CancellationToken ct = default);
    // Caller xác minh hủy từng dòng, giữ khóa nguồn và lưu dữ liệu hủy trước khi gọi.
    Task RecordCancelledOrderLinesAsync(int orderId, IReadOnlyCollection<int> detailIds, CancellationToken ct = default);
}

// Chỉ worker server gọi; không có endpoint hay cờ System từ browser.
public interface ISessionInvoiceIssuer
{
    Task<InvoiceDto?> IssueForAutoClosedSessionAsync(int sessionId, CancellationToken ct = default);
}
