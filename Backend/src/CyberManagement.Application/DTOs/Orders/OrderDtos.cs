using System.Text.Json.Serialization;

namespace CyberManagement.Application.DTOs.Orders;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CreateOrderItem(int ProductId, int Quantity);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CreateOrderRequest(int GamingSessionId, List<CreateOrderItem> Items,
    string PaymentMethod, string? VoucherCode = null);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record StaffCreateOrderRequest(int CustomerId, int? GamingSessionId,
    List<CreateOrderItem> Items, string PaymentMethod, string? VoucherCode = null);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record OrderVersionRequest(string ExpectedVersion);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CashCollectionRequest(string ExpectedVersion, bool CashReceived);

public sealed record OrderItemDto(int Id, int ProductId, string ProductNameSnapshot, int Quantity,
    decimal UnitPrice, decimal DiscountAmount, decimal NetAmount, string Status, string? CancelReason);
public sealed record VoucherSnapshotDto(int PromotionId, string Code, string DiscountType,
    decimal DiscountValue, decimal DiscountAmount);
public sealed record OrderPaymentDto(string? Method, string PaymentStatus, decimal CollectedAmount,
    decimal RefundedAmount, decimal RefundPendingAmount, decimal RefundUnresolvedAmount,
    decimal OutstandingAmount, int? PaymentId, string? PaymentUrl = null,
    DateTimeOffset? ExpiresAtUtc = null, bool RequiresReconciliation = false);
public sealed record OrderDto(int Id, int CustomerId, int? GamingSessionId, int? CreatedByEmployeeId,
    DateTime CreatedAtUtc, string Status, string Version, decimal Subtotal, decimal DiscountAmount,
    decimal OriginalPayable, decimal CancelledAmount, decimal PayableAmount, VoucherSnapshotDto? Voucher,
    IReadOnlyList<OrderItemDto> Items, OrderPaymentDto Payment)
{
    // Cancellation workflow is delivered in S3-K-1; keep the agreed response shape.
    public IReadOnlyList<OrderCancellationDto> CancellationRequests { get; init; } = [];
}
public sealed record OrderCancellationDto(int Id, int OrderId, IReadOnlyList<int> ItemIds, string Status,
    string Reason, string? ResolutionReason, DateTimeOffset RequestedAtUtc, DateTimeOffset? ResolvedAtUtc);
public sealed record OrderPageDto(IReadOnlyList<OrderDto> Items, int Page, int PageSize, int TotalCount);
public sealed record OrderResult(OrderDto Order, bool Replayed);
public sealed record OrderActor(string Type, int Id)
{
    public int? EmployeeId => Type == "Staff" ? Id : null;
}
