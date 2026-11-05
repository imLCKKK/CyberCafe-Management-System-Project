namespace CyberManagement.Domain.Entities;

public class WalletTransaction
{
    public int TransactionId { get; set; }

    public int CustomerId { get; set; }

    public decimal Amount { get; set; }

    public decimal BalanceAfter { get; set; }

    public string Type { get; set; } = null!;

    public string? Description { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Customer Customer { get; set; } = null!;
}
