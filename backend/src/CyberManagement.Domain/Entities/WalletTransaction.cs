namespace CyberManagement.Domain.Entities;

public class WalletTransaction
{
    public int TransactionId { get; set; }

    public int CustomerId { get; set; }

    public decimal Amount { get; set; }

    public decimal BalanceAfter { get; set; }

    public string Type { get; set; } = null!;

    public string? Description { get; set; }

    // Khóa chống xử lý lặp do nơi gọi cấp (ví dụ Idempotency-Key của nạp tại quầy). Null = giao dịch không cần khóa.
    // Duy nhất theo (CustomerId, IdempotencyKey) khi khác null.
    public string? IdempotencyKey { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Customer Customer { get; set; } = null!;
}
