namespace CyberManagement.Domain.Entities;

public class Payment
{
    public int PaymentId { get; set; }
    // Khoản thu luôn có nguồn, InvoiceId chỉ được gắn khi phát hành chứng từ.
    public int? InvoiceId { get; set; }
    public int? OrderId { get; set; }
    public int? SessionChargeId { get; set; }
    public int? OrderDetailId { get; set; }
    public int? WalletTransactionId { get; set; }
    public int? RefundOfPaymentId { get; set; }
    public string Type { get; set; } = "Payment";
    public decimal Amount { get; set; }
    public string Method { get; set; } = null!;
    public string Status { get; set; } = "Pending";
    public string OperationKey { get; set; } = null!;
    public string? MerchantTransactionId { get; set; }
    public string? GatewayTransactionId { get; set; }
    public string? Reason { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? PaymentTime { get; set; }
    public DateTimeOffset? ConfirmedAt { get; set; }
    public DateTimeOffset? ProviderOccurredAt { get; set; }
    public string? PaymentTimeBasis { get; set; }
    public Invoice? Invoice { get; set; }
    public Order? Order { get; set; }
    public SessionCharge? SessionCharge { get; set; }
    public OrderDetail? OrderDetail { get; set; }
    public WalletTransaction? WalletTransaction { get; set; }
    public Payment? RefundOfPayment { get; set; }
}
