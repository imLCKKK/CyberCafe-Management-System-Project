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

    public decimal? HourlyRateSnapshot { get; set; }

    public decimal? PricePerHourSnapshot { get; set; }

    public string Status { get; set; } = "Active";

    public int? OpenedByEmployeeId { get; set; }
    public string? OpenOperationKey { get; set; }
    public string? CloseOperationKey { get; set; }
    public string? ClosedByActorType { get; set; }
    public int? ClosedByEmployeeId { get; set; }
    public string? CloseReason { get; set; }
    public int? ClosingInvoiceId { get; set; }

    public Computer Computer { get; set; } = null!;

    public Customer Customer { get; set; } = null!;

    public ICollection<SessionCharge> Charges { get; set; } = new List<SessionCharge>();
}
