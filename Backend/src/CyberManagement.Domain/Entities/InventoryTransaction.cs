namespace CyberManagement.Domain.Entities;

public class InventoryTransaction
{
    public int InvTransId { get; set; }

    public int ProductId { get; set; }

    public int? EmployeeId { get; set; }

    public string Type { get; set; } = null!;

    public int Quantity { get; set; }

    public string ReferenceType { get; set; } = null!;

    public int? ReceiptId { get; set; }

    public int? OrderId { get; set; }

    public int Before { get; set; }
    public int After { get; set; }
    public string Reason { get; set; } = null!;
    public int? OrderDetailId { get; set; }
    public OrderDetail? OrderDetail { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public Product Product { get; set; } = null!;

    public Employee? Employee { get; set; } = null!;

    public StockReceipt? StockReceipt { get; set; }

    public Order? Order { get; set; }
}
