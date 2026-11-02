namespace CyberManagement.Domain.Entities;

public class InventoryTransaction
{
    public int InvTransId { get; set; }

    public int ProductId { get; set; }

    public int EmployeeId { get; set; }

    public string Type { get; set; } = null!;

    public int Quantity { get; set; }

    public string ReferenceType { get; set; } = null!;

    public int? ReceiptId { get; set; }

    public int? OrderId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Product Product { get; set; } = null!;

    public Employee Employee { get; set; } = null!;

    public StockReceipt? StockReceipt { get; set; }

    public Order? Order { get; set; }
}
