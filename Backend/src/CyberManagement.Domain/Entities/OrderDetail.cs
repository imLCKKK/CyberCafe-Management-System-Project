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

    // Trạng thái nguồn theo contract checkout, để chụp đúng dòng đã hủy.
    public string Status { get; set; } = "Pending";

    public Order Order { get; set; } = null!;

    public Product Product { get; set; } = null!;
}
