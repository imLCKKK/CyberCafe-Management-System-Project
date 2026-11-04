using CyberManagement.Application.DTOs.Inventory;
using CyberManagement.Application.Interfaces.Repositories;
using CyberManagement.Infrastructure.Persistance;
using Microsoft.EntityFrameworkCore;

namespace CyberManagement.Infrastructure.Repositories;

public sealed class InventoryQueries(ApplicationDbContext context) : IInventoryQueries
{
    // Đọc cả hàng hết tồn và ngừng bán; Stock đã là tồn do SQL ghi nhận.
    public async Task<IReadOnlyList<InventoryItemDto>> GetInventoryAsync(
        CancellationToken cancellationToken = default) =>
        await context.Products.AsNoTracking()
            .OrderBy(product => product.ProductId)
            .Select(product => new InventoryItemDto(
                product.ProductId, product.ProductName, product.Stock,
                product.IsActive, product.Price, product.CategoryId))
            .ToListAsync(cancellationToken);
}
