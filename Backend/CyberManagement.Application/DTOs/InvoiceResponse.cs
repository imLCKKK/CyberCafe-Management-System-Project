namespace CyberManagement.Application.DTOs;

// Hợp đồng danh sách hóa đơn theo PDF. Chỉ ánh xạ khi entity/ledger có đủ dữ liệu.
// InvoiceDTOs cũ vẫn phục vụ endpoint cũ, không đổi tên tiền để giả lập hợp đồng mới.
public sealed class InvoiceResponse
{
    public long Id { get; init; }
    public long CustomerId { get; init; }
    public long? OrderId { get; init; }
    public long? GamingSessionId { get; init; }
    public decimal GrossAmount { get; init; }
    public decimal RefundAmount { get; init; }
    public decimal PaidAmount { get; init; }
    public string Status { get; init; } = string.Empty;
    public DateTimeOffset IssuedAt { get; init; }
}