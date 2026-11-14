namespace CyberManagement.Domain.Entities;

// Lịch sử gửi/query provider; tiền hoàn chỉ được ghi một lần ở Payments.
public class RefundAttempt
{
    public int RefundAttemptId { get; set; }
    public int PaymentId { get; set; }
    public int ProviderAppId { get; set; }
    public string MerchantRefundId { get; set; } = null!;
    public string? GatewayRefundId { get; set; }
    public string Status { get; set; } = "Created";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? LastQueriedAt { get; set; }
    public string? LastApi { get; set; }
    public int? LastReturnCode { get; set; }
    public int? LastSubReturnCode { get; set; }
    public Payment Payment { get; set; } = null!;
}
