using CyberManagement.Application.DTOs;
using CyberManagement.Application.Interfaces.Repositories;
using CyberManagement.Infrastructure.Persistance;
using CyberManagement.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Text;

namespace CyberManagement.Infrastructure.Repositories
{
    public class InventoryRepository : IInventoryRepository
    {
        private readonly ApplicationDbContext _context;

        public InventoryRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IReadOnlyList<InventoryItemDto>> GetInventoryAsync(CancellationToken cancellationToken = default)
        {
            return await _context.Products
                .AsNoTracking()
                .OrderBy(product => product.ProductId)
                .Select(product => new InventoryItemDto
                {
                    Id = product.ProductId,
                    Name = product.ProductName,
                    StockQuantity = product.Stock,
                    IsActive = product.IsActive,
                    CurrentPrice = product.Price,
                    CategoryId = product.CategoryId
                })
                .ToListAsync(cancellationToken);
        }

        public async Task<Product?> GetProductForUpdateAsync(long productId, CancellationToken cancellationToken = default)
        {
            if (productId <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(productId));
            }

            if (_context.Database.CurrentTransaction is null)
            {
                throw new InvalidOperationException("Phải mở transaction trước khi cập nhật kho.");
            }

            var id = productId;

            return await _context.Products
                .FromSqlInterpolated($"SELECT * FROM [Products] WITH (UPDLOCK, ROWLOCK) WHERE [ProductId] = {id}")
                .AsTracking()
                .SingleOrDefaultAsync(cancellationToken);
        }
    }
}
