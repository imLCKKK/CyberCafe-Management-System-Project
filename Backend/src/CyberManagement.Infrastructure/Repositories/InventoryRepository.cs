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
        private readonly Dictionary<int, Product> _lockedProducts = new();
        private Guid? _lockedTransactionId;

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
            cancellationToken.ThrowIfCancellationRequested();
            _context.ChangeTracker.DetectChanges();

            var transactionId = _context.Database.CurrentTransaction!.TransactionId;
            if (_lockedTransactionId != transactionId)
            {
                _lockedProducts.Clear();
                _lockedTransactionId = transactionId;
            }

            var entry = _context.ChangeTracker.Entries<Product>()
                .SingleOrDefault(x => x.Entity.ProductId == productId);

            if (entry is not null && entry.State is EntityState.Added or EntityState.Deleted)
            {
                throw new BusinessRuleException(
                    "Không thể thao tác kho với sản phẩm đang được thêm hoặc xóa.");
            }

            // Đã giữ khóa trong chính transaction này: giữ thay đổi của caller.
            if (entry is not null
                && _lockedProducts.TryGetValue(productId, out var locked)
                && ReferenceEquals(entry.Entity, locked))
            {
                return locked;
            }

            // Tồn/trạng thái chỉ được sửa sau khi đã lấy khóa.
            if (entry is not null
                && (entry.Property(x => x.Stock).IsModified
                    || entry.Property(x => x.IsActive).IsModified))
            {
                throw new BusinessRuleException(
                    "Phải khóa sản phẩm trước khi sửa tồn hoặc trạng thái bán.");
            }

            // AsNoTracking lấy dữ liệu SQL thật, không tái dùng entity cũ của EF.
            var snapshot = await _context.Products
                .FromSqlInterpolated(
                    $"SELECT * FROM [dbo].[Products] WITH (UPDLOCK, HOLDLOCK) WHERE [ProductId] = {productId}")
                .AsNoTracking()
                .SingleOrDefaultAsync(cancellationToken);

            if (snapshot is null)
            {
                return null;
            }

            Product product;
            if (entry is null)
            {
                product = snapshot;
                _context.Products.Attach(product);
            }
            else
            {
                // Giữ các sửa đổi tên/giá..., cập nhật lại dữ liệu gốc theo SQL.
                var pending = entry.Properties.Where(x => x.IsModified)
                    .ToDictionary(x => x.Metadata.Name, x => x.CurrentValue);

                entry.CurrentValues.SetValues(snapshot);
                entry.OriginalValues.SetValues(snapshot);
                entry.State = EntityState.Unchanged;

                foreach (var (name, value) in pending)
                {
                    var property = entry.Property(name);
                    property.CurrentValue = value;
                    property.IsModified = !Equals(property.OriginalValue, value);
                }

                product = entry.Entity;
            }

            _lockedProducts[productId] = product;
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
