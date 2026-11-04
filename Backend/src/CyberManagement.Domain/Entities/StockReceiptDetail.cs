namespace CyberManagement.Domain.Entities;

public class StockReceiptDetail
{
    public int ReceiptDetailId { get; set; }

    public int ReceiptId { get; set; }

    public int ProductId { get; set; }

    public int Quantity { get; set; }

    public decimal UnitCost { get; set; }

    public StockReceipt StockReceipt { get; set; } = null!;

    public Product Product { get; set; } = null!;
}
