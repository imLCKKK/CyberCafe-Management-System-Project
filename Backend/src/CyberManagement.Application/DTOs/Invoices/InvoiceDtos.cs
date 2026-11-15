using System.Text.Json.Serialization;

namespace CyberManagement.Application.DTOs.Invoices;

// Chỉ nhận nguồn; tiền, khách và actor do server xác định.
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record IssueInvoiceRequest
{
    public required string Kind { get; init; }
    public required List<int> OrderIds { get; init; }
    public int? GamingSessionId { get; init; }
}

public sealed record InvoiceLineDto(int Id, int? OrderDetailId, int? SessionChargeId,
    string ItemType, string Description, int Quantity, decimal UnitPrice,
    decimal Subtotal, decimal DiscountAmount, decimal Amount, decimal CancelledAmount,
    string? VoucherCode, string? VoucherDiscountType, decimal? VoucherDiscountValue);

public sealed record InvoicePaymentDto(int Id, string Type, string Method, string Status,
    decimal Amount, int? WalletTransactionId, DateTimeOffset? PaidAt);

public sealed record InvoiceDto(int Id, int CustomerId, string CustomerName,
    IReadOnlyList<int> OrderIds, int? GamingSessionId, string ActorType, int? EmployeeId,
    string IssuedBy, DateTimeOffset IssuedAt, decimal Subtotal, decimal Discount,
    decimal GrossAmount, decimal CancelledAmount, decimal RefundAmount,
    decimal PaidAmount, decimal AmountDue, decimal OverpaidAmount, string Status,
    IReadOnlyList<InvoiceLineDto> Lines, IReadOnlyList<InvoicePaymentDto> Payments);
