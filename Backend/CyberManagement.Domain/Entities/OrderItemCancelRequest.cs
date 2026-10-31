namespace CyberManagement.Domain.Entities;

public class OrderItemCancelRequest
{
    public long RequestId { get; set; }
    public long OrderDetailId { get; set; }
    public long RequestedByCustomerId { get; set; }
    public string Status { get; set; } = "Pending";
    public DateTime RequestedAt { get; set; } = DateTime.UtcNow;
    public long? HandledByAccountId { get; set; }
    public DateTime? HandledAt { get; set; }
    public virtual OrderDetail OrderDetail { get; set; } = null!;
}
