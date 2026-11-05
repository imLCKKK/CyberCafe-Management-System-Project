namespace CyberManagement.Domain.Entities;

public class Order
{
    public int OrderId { get; set; }

    public int CustomerId { get; set; }

    public int? GamingSessionId { get; set; }

    public int EmployeeId { get; set; }

    public DateTime OrderTime { get; set; } = DateTime.UtcNow;

    public string Status { get; set; } = "Pending";

    public decimal TotalAmount { get; set; }

    public Customer Customer { get; set; } = null!;

    public GamingSession? GamingSession { get; set; }

    public Employee Employee { get; set; } = null!;

    public ICollection<OrderDetail> Details { get; set; } = new List<OrderDetail>();

    public ICollection<OrderStatusHistory> StatusHistory { get; set; } = new List<OrderStatusHistory>();
}
