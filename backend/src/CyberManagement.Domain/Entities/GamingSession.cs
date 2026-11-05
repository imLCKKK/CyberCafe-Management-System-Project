namespace CyberManagement.Domain.Entities;

public class GamingSession
{
    public int GamingSessionId { get; set; }

    public int ComputerId { get; set; }

    public int CustomerId { get; set; }

    public DateTime StartTime { get; set; }

    public DateTime? EndTime { get; set; }

    public DateTime? LastChargedAt { get; set; }

    public decimal TotalCost { get; set; }

    public string Status { get; set; } = "Active";

    public Computer Computer { get; set; } = null!;

    public Customer Customer { get; set; } = null!;

    public ICollection<SessionCharge> Charges { get; set; } = new List<SessionCharge>();
}
