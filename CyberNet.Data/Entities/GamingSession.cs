namespace CyberNet.Data.Entities;

public class GamingSession
{
    public int SessionId { get; set; }
    public int ComputerId { get; set; }
    public int CustomerId { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public decimal? TotalCost { get; set; }

    public Computer Computer { get; set; } = null!;
    public Customer Customer { get; set; } = null!;
    public ICollection<Order> Orders { get; set; } = new List<Order>();
    public ICollection<Invoice> Invoices { get; set; } = new List<Invoice>();
}
