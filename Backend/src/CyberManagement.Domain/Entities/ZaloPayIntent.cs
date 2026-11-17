namespace CyberManagement.Domain.Entities;

// A verified gateway receipt is separate from wallet credit or Order fulfillment (S2-M-3).
public sealed class ZaloPayIntent
{
    public int IntentId { get; set; }
    public int CustomerId { get; set; }
    public int? OrderId { get; set; }
    public string Purpose { get; set; } = null!;
    public string OperationKey { get; set; } = null!;
    public decimal ExpectedAmountVnd { get; set; }
    public int ProviderAppId { get; set; }
    public string MerchantTransactionId { get; set; } = null!;
    public string Status { get; set; } = "Pending";
    public string? PaymentUrl { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset ExpiresAtUtc { get; set; }
    public string? ProviderTransactionId { get; set; }
    public decimal? ProviderAmountVnd { get; set; }
    public DateTimeOffset? ProviderOccurredAtUtc { get; set; }
    public DateTimeOffset? VerifiedAtUtc { get; set; }
    public bool RequiresReconciliation { get; set; }
    public string? FailureReason { get; set; }
    public Guid? QueryLeaseToken { get; set; }
    public DateTimeOffset? QueryLeaseUntilUtc { get; set; }
    public DateTimeOffset? NextQueryAtUtc { get; set; }
    public DateTimeOffset? LastQueryAtUtc { get; set; }
    public DateTimeOffset? AppliedAtUtc { get; set; }
    public int? WalletTransactionId { get; set; }
    public int? PaymentId { get; set; }
    public string? SettlementFailureReason { get; set; }
    public Customer Customer { get; set; } = null!;
    public Order? Order { get; set; }
    public WalletTransaction? WalletTransaction { get; set; }
    public Payment? Payment { get; set; }
}
