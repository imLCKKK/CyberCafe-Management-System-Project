namespace CyberManagement.Domain.Entities;

public class Invoice
{
    public int InvoiceId { get; set; }

    public int CustomerId { get; set; }

    public int? GamingSessionId { get; set; }

    public int EmployeeId { get; set; }

    public int? PromotionId { get; set; }

    public DateTime InvoiceDate { get; set; } = DateTime.UtcNow;

    public decimal Subtotal { get; set; }

    public decimal Discount { get; set; }

    public decimal TotalAmount { get; set; }

    public string Status { get; set; } = "Unpaid";

    public Customer Customer { get; set; } = null!;

    public GamingSession? GamingSession { get; set; }

    public Employee Employee { get; set; } = null!;

    public Promotion? Promotion { get; set; }

    public ICollection<InvoiceOrder> InvoiceOrders { get; set; } = new List<InvoiceOrder>();

    public ICollection<InvoiceDetail> Details { get; set; } = new List<InvoiceDetail>();

    public ICollection<Payment> Payments { get; set; } = new List<Payment>();
}
