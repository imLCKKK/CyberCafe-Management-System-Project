namespace CyberManagement.Application.DTOs.Inventory;

// Dữ liệu đọc kho; ID giữ kiểu int theo schema chung.
public sealed record InventoryItemDto(
    int Id, string Name, int StockQuantity, bool IsActive,
    decimal CurrentPrice, int CategoryId);
