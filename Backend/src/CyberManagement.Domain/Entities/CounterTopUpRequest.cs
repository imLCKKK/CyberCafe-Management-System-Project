namespace CyberManagement.Domain.Entities;

public sealed class CounterTopUpRequest
{
    public int RequestId { get; set; }
    public int CustomerId { get; set; }
    public Guid RequestKey { get; set; }
    public decimal Amount { get; set; }
    public decimal BonusAmount { get; set; }
    public decimal TotalAmount { get; set; }
    public string Status { get; set; } = "Pending";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ApprovedAt { get; set; }
    public int? ApprovedByEmployeeId { get; set; }
    public decimal? CashAmountReceived { get; set; }
    public int? WalletTransactionId { get; set; }
    public Customer Customer { get; set; } = null!;
    public Employee? ApprovedByEmployee { get; set; }
    public WalletTransaction? WalletTransaction { get; set; }
}
