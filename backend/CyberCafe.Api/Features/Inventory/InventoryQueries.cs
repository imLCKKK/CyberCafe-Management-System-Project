using CyberCafe.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace CyberCafe.Api.Features.Inventory;

public sealed class InventoryQueries
{
	private readonly CyberCafeDbContext _context;

	public InventoryQueries(CyberCafeDbContext context)
	{
		_context = context;
	}

	// Đọc tồn kho, gồm cả sản phẩm hết hàng hoặc ngừng bán.
	public async Task<IReadOnlyList<InventoryItemResponse>> GetInventoryAsync(
		CancellationToken cancellationToken = default)
	{
		return await _context.Products
			.AsNoTracking()
			.OrderBy(product => product.Id)
			.Select(product => new InventoryItemResponse
			{
				Id = product.Id,
				Name = product.Name,
				StockQuantity = product.StockQuantity,
				IsActive = product.IsActive,
				CurrentPrice = product.CurrentPrice,
				CategoryId = product.CategoryId
			})
			.ToListAsync(cancellationToken);
	}
}