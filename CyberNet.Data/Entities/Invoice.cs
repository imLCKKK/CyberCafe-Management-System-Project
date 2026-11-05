namespace CyberNet.Data.Entities;

public class Invoice
{
    public int InvoiceId { get; set; }
    public int? OrderId { get; set; }
    public int? SessionId { get; set; }
    public decimal TotalAmount { get; set; }
    public DateTime? PaymentTime { get; set; }

    public Order? Order { get; set; }
    public GamingSession? Session { get; set; }
    public ICollection<Payment> Payments { get; set; } = new List<Payment>();
}
