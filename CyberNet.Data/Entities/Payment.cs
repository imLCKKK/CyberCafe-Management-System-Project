namespace CyberNet.Data.Entities;

public class Payment
{
    public int PaymentId { get; set; }
    public int InvoiceId { get; set; }
    public decimal Amount { get; set; }
    public string? Method { get; set; }
    public DateTime PaymentTime { get; set; }

    public Invoice Invoice { get; set; } = null!;
}
