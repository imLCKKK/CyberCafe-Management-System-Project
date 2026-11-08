namespace CyberManagement.Domain.Entities;

public class InvoiceDetail
{
    public int InvoiceDetailId { get; set; }

    public int InvoiceId { get; set; }
    // Một dòng chứng từ có đúng một nguồn món hoặc phí phiên.
    public int? OrderDetailId { get; set; }
    public int? SessionChargeId { get; set; }
    public OrderDetail? OrderDetail { get; set; }
    public SessionCharge? SessionCharge { get; set; }

    public string ItemType { get; set; } = null!;

    public string Description { get; set; } = null!;

    public int Quantity { get; set; }

    public decimal UnitPrice { get; set; }

    public decimal Amount { get; set; }

    public Invoice Invoice { get; set; } = null!;
}
