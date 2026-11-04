namespace CyberManagement.Domain.Entities;

public class OrderStatusHistory
{
    public int HistoryId { get; set; }

    public int OrderId { get; set; }

    public string? OldStatus { get; set; }

    public string NewStatus { get; set; } = null!;

    public int ChangedBy { get; set; }

    public DateTime ChangedAt { get; set; } = DateTime.UtcNow;

    public Order Order { get; set; } = null!;

    public Employee ChangedByEmployee { get; set; } = null!;
}
