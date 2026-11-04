namespace CyberNet.Data.Entities;

public class Order
{
    public int OrderId { get; set; }
    public int? CustomerId { get; set; }      // null = khách vãng lai
    public int EmployeeId { get; set; }
    public int? SessionId { get; set; }       // null = không gắn phiên chơi
    public DateTime OrderTime { get; set; }
    public string Status { get; set; } = "Pending";
    public decimal TotalAmount { get; set; }

    public Customer? Customer { get; set; }
    public Employee Employee { get; set; } = null!;
    public GamingSession? Session { get; set; }
    public ICollection<OrderDetail> OrderDetails { get; set; } = new List<OrderDetail>();
    public ICollection<Invoice> Invoices { get; set; } = new List<Invoice>();
}
