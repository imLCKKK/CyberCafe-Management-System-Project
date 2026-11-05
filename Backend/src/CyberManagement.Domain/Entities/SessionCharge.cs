namespace CyberManagement.Domain.Entities;

public class SessionCharge
{
    public int ChargeId { get; set; }

    public int GamingSessionId { get; set; }

    public DateTime StartTime { get; set; }

    public DateTime EndTime { get; set; }

    public decimal Amount { get; set; }

    public GamingSession GamingSession { get; set; } = null!;
}
