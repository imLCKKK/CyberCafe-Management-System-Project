using CyberManagement.Application.Exceptions;
using CyberManagement.Application.Interfaces.Repositories;
using CyberManagement.Domain.Entities;
using CyberManagement.Infrastructure.Persistance;
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

        private void RequireTransaction()
        {
            if (_context.Database.CurrentTransaction is null)
            {
                throw new InvalidOperationException(
                    "Phải mở transaction trước khi thao tác kho.");
            }
        }

        public async Task<OrderDetail?> GetOrderDetailAsync(int orderDetailId, CancellationToken cancellationToken = default)
        {
            if (orderDetailId <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(orderDetailId));
            }

            RequireTransaction();

            // Caller khóa nguồn Order trước; hàm này đọc dòng món để đối chiếu.
            return await _context.OrderDetails
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    item => item.OrderDetailId == orderDetailId,
                    cancellationToken);
        }

        public async Task<Product?> GetProductForUpdateAsync(int productId, CancellationToken cancellationToken = default)
        {
            if (productId <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(productId));
            }

            RequireTransaction();

            // Khóa sản phẩm để các thao tác cập nhật tồn không chạy chồng nhau.
            var product = await _context.Products
                .FromSqlInterpolated(
                    $"SELECT * FROM [dbo].[Products] WITH (UPDLOCK, HOLDLOCK) WHERE [ProductId] = {productId}")
                .AsTracking()
                .SingleOrDefaultAsync(cancellationToken);

            if (product is null)
            {
                return null;
            }

            var entry = _context.Entry(product);

            if (entry.State == EntityState.Deleted)
            {
                throw new InvalidOperationException(
                    "Sản phẩm đang được đánh dấu xóa trong transaction.");
            }

            // Làm mới dữ liệu nếu sản phẩm đã được đọc trước khi lấy khóa.
            // Giữ lại thay đổi chưa lưu của thao tác hiện tại.
            if (entry.State == EntityState.Unchanged)
            {
                await entry.ReloadAsync(cancellationToken);
            }

            return product;
        }

        public async Task<IReadOnlyList<Product>> GetProductsForUpdateAsync(IReadOnlyCollection<int> productIds, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(productIds);

            if (productIds.Any(id => id <= 0))
            {
                throw new ArgumentException(
                    "ID sản phẩm phải lớn hơn 0.",
                    nameof(productIds));
            }

            RequireTransaction();

            var products = new List<Product>();

            // Các luồng cùng khóa theo một thứ tự để hạn chế deadlock.
            // Dùng Distinct() để tránh khóa trùng khi có nhiều dòng món cùng sản phẩm.
            foreach (var productId in productIds.Distinct().OrderBy(id => id))
            {
                var product = await GetProductForUpdateAsync(productId, cancellationToken);

                if (product is null)
                {
                    throw new EntityNotFoundException(
                        $"Không tìm thấy sản phẩm có ID {productId}.");
                }

                products.Add(product);
            }

            return products;
        }
    }
}
