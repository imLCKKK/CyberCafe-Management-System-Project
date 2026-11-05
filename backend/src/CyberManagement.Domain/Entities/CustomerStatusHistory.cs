namespace CyberManagement.Domain.Entities;

public class CustomerStatusHistory
{
    public int HistoryId { get; set; }

    public int CustomerId { get; set; }

    public string? OldStatus { get; set; }

    public string NewStatus { get; set; } = null!;

    public string? Reason { get; set; }

    public int ChangedBy { get; set; }

    public DateTime ChangedAt { get; set; } = DateTime.UtcNow;

    public Customer Customer { get; set; } = null!;

    public Employee ChangedByEmployee { get; set; } = null!;
}
