namespace CyberManagement.Domain.Entities;

public class OrderDetail
{
    public int OrderDetailId { get; set; }

    public int OrderId { get; set; }

    public int ProductId { get; set; }

    public int Quantity { get; set; }

    public decimal UnitPrice { get; set; }
    public string ProductNameSnapshot { get; set; } = null!;
    public decimal DiscountAmount { get; set; }

    public Order Order { get; set; } = null!;

    public Product Product { get; set; } = null!;
}
