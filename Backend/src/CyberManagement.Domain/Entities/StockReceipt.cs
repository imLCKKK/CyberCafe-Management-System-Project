namespace CyberManagement.Domain.Entities;

public class StockReceipt
{
    public int ReceiptId { get; set; }

    public int SupplierId { get; set; }

    public int EmployeeId { get; set; }

    public DateTime ReceiptDate { get; set; }

    public decimal TotalAmount { get; set; }

    public Supplier Supplier { get; set; } = null!;

    public Employee Employee { get; set; } = null!;

    public ICollection<StockReceiptDetail> Details { get; set; } = new List<StockReceiptDetail>();
}
