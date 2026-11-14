namespace CyberManagement.Domain.Entities;

public class WalletTransaction
{
    public int TransactionId { get; set; }

    public int CustomerId { get; set; }

    public decimal Amount { get; set; }

    public decimal BalanceAfter { get; set; }

    public string Type { get; set; } = null!;

    public string? Description { get; set; }

    // Nullable for ledger rows recorded before the shared wallet writer was introduced.
    public string? OperationKey { get; set; }

    public string? Kind { get; set; }

    public string? SourceType { get; set; }

    public string? SourceId { get; set; }

    public int? OriginalDebitId { get; set; }

    public string? ActorType { get; set; }

    public int? ActorEmployeeId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Customer Customer { get; set; } = null!;
}
