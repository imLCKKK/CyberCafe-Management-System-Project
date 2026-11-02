namespace CyberCafe.Api.Features.Inventory;

// Dữ liệu một sản phẩm trả về khi xem tồn kho.
public sealed class InventoryItemResponse
{
    public long Id { get; init; }
    public string Name { get; init; } = "";
    public int StockQuantity { get; init; }
    public bool IsActive { get; init; }
    public decimal CurrentPrice { get; init; }
    public long CategoryId { get; init; }
}