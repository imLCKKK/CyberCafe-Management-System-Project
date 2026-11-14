namespace CyberManagement.Domain.Entities;

// Mỗi lần dùng voucher gắn với một đơn; ReleasedAt đánh dấu hoàn lượt.
public class PromotionUsage
{
    public int PromotionUsageId { get; set; }
    public int PromotionId { get; set; }
    public int OrderId { get; set; }
    public int CustomerId { get; set; }
    public DateTimeOffset UsedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ReleasedAt { get; set; }
    public Promotion Promotion { get; set; } = null!;
    public Order Order { get; set; } = null!;
    public Customer Customer { get; set; } = null!;
}
