namespace CyberManagement.Domain.Entities;

public class InvoiceOrder
{
    public int InvoiceId { get; set; }

    public int OrderId { get; set; }

    public Invoice Invoice { get; set; } = null!;

    public Order Order { get; set; } = null!;
}
