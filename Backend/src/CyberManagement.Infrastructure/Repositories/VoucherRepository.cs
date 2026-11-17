using CyberManagement.Application.DTOs.Vouchers;
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
    // Đọc, thêm và khóa dữ liệu voucher trong database.
    public sealed class VoucherRepository : IVoucherRepository
    {
        private readonly ApplicationDbContext _context;

        // Chỉ ghi nhớ voucher đã khóa trong transaction hiện tại.
        private readonly Dictionary<int, Promotion> _lockedPromotions = new();
        private Guid? _transactionId;

        public VoucherRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        // Lấy danh sách voucher để hiển thị.
        public async Task<IReadOnlyList<VoucherDto>> GetAllAsync(CancellationToken cancellationToken = default)
        {
            return await _context.Promotions
                .AsNoTracking()
                .OrderBy(x => x.PromotionId)
                .Select(x => new VoucherDto(
                    x.PromotionId,
                    x.Code,
                    x.PromoName,
                    x.DiscountType,
                    x.DiscountValue,
                    x.MinOrderAmount,
                    x.MaxDiscountAmount,
                    x.StartDate,
                    x.EndDate,
                    x.TotalUsageLimit,
                    x.PerCustomerUsageLimit,
                    x.IsActive))
                .ToListAsync(cancellationToken);
        }

        // Tìm voucher theo ID; trả null nếu không có.
        public Task<Promotion?> GetByIdAsync(int promotionId, CancellationToken cancellationToken = default)
        {
            return _context.Promotions
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    x => x.PromotionId == promotionId,
                    cancellationToken);
        }

        // Tìm voucher theo mã đã chuẩn hóa.
        public Task<Promotion?> GetByCodeAsync(string normalizedCode, CancellationToken cancellationToken = default)
        {
            return _context.Promotions
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    x => x.Code == normalizedCode,
                    cancellationToken);
        }

        // Lấy và khóa voucher theo ID trước khi sửa.
        public async Task<Promotion?> GetByIdForUpdateAsync(int promotionId, CancellationToken cancellationToken = default)
        {
            RequireTransaction();

            // Đọc SQL thật và giữ khóa đến khi transaction kết thúc.
            var snapshot = await _context.Promotions
                .FromSqlInterpolated(
                    $"SELECT * FROM [dbo].[Promotions] WITH (UPDLOCK, HOLDLOCK) WHERE [PromotionId] = {promotionId}")
                .AsNoTracking()
                .SingleOrDefaultAsync(cancellationToken);

            return TrackLockedPromotion(snapshot);
        }

        // Khóa voucher theo mã để các thao tác dùng lượt lần lượt xử lý.
        public async Task<Promotion?> GetByCodeForUpdateAsync(string normalizedCode, CancellationToken cancellationToken = default)
        {
            RequireTransaction();

            var snapshot = await _context.Promotions
                .FromSqlInterpolated(
                    $"SELECT * FROM [dbo].[Promotions] WITH (UPDLOCK, HOLDLOCK) WHERE [Code] = {normalizedCode}")
                .AsNoTracking()
                .SingleOrDefaultAsync(cancellationToken);

            return TrackLockedPromotion(snapshot);
        }

        // Kiểm tra mã bị trùng; bỏ qua chính voucher đang sửa.
        public async Task<bool> CodeExistsAsync(string normalizedCode, int? excludedPromotionId = null, CancellationToken cancellationToken = default)
        {
            var query = _context.Promotions
                .Where(x => x.Code == normalizedCode);

            if (excludedPromotionId.HasValue)
            {
                var excludedId = excludedPromotionId.Value;

                query = query.Where(
                    x => x.PromotionId != excludedId);
            }

            return await query.AnyAsync(cancellationToken);
        }

        // Lấy giá và trạng thái món từ database để tính thử tiền giảm.
        public async Task<IReadOnlyList<Product>> GetProductsForPreviewAsync(IReadOnlyCollection<int> productIds, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(productIds);

            var ids = productIds.Distinct().ToArray();

            // Đọc cả trạng thái danh mục để service kiểm tra món hợp lệ.
            return await _context.Products
                .AsNoTracking()
                .Include(x => x.Category)
                .Where(x => ids.Contains(x.ProductId))
                .OrderBy(x => x.ProductId)
                .ToListAsync(cancellationToken);
        }

        // Đọc lại và khóa đơn cùng dòng món trước khi áp hoặc hoàn lượt voucher.
        public async Task<Order?> GetOrderForUpdateAsync(int orderId, CancellationToken cancellationToken = default)
        {
            RequireTransaction();
            _context.ChangeTracker.DetectChanges();

            var snapshot = await _context.Orders
                .FromSqlInterpolated(
                    $"SELECT * FROM [dbo].[Orders] WITH (UPDLOCK, HOLDLOCK) WHERE [OrderId] = {orderId}")
                .AsNoTracking()
                .SingleOrDefaultAsync(cancellationToken);

            if (snapshot is null)
            {
                return null;
            }

            var detailSnapshots = await _context.OrderDetails
                .FromSqlInterpolated(
                    $"SELECT * FROM [dbo].[OrderDetails] WITH (UPDLOCK, HOLDLOCK) WHERE [OrderId] = {orderId}")
                .AsNoTracking()
                .OrderBy(x => x.OrderDetailId)
                .ToListAsync(cancellationToken);

            var detailIds = detailSnapshots
                .Select(x => x.OrderDetailId)
                .ToHashSet();

            // Caller phải lưu đơn/dòng món trước khi gọi rule voucher.
            var trackedDetails = _context.ChangeTracker
                .Entries<OrderDetail>()
                .Where(x => x.Entity.OrderId == orderId)
                .ToArray();

            if (trackedDetails.Any(x =>
                    x.State != EntityState.Unchanged
                    || !detailIds.Contains(x.Entity.OrderDetailId)))
            {
                throw new BusinessRuleException(
                    "Dòng món chưa được lưu hoặc đã thay đổi. Hãy đọc lại nguồn đơn.");
            }

            var order = TrackFresh(
                snapshot,
                x => x.OrderId == orderId);

            order.Details = detailSnapshots
                .Select(detail => TrackFresh(
                    detail,
                    x => x.OrderDetailId == detail.OrderDetailId))
                .ToList();

            return order;
        }

        // Thêm voucher vào EF, chờ SaveChangesAsync lưu xuống database.
        public async Task AddAsync( Promotion promotion, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(promotion);
            RequireTransaction();

            await _context.Promotions.AddAsync(
                promotion,
                cancellationToken);
        }

        // Yêu cầu có transaction và xóa danh sách khóa khi sang transaction khác.
        private void RequireTransaction()
        {
            var transaction = _context.Database.CurrentTransaction;

            if (transaction is null)
            {
                throw new InvalidOperationException(
                    "Phải mở transaction trước khi ghi hoặc khóa dữ liệu voucher.");
            }

            if (_transactionId != transaction.TransactionId)
            {
                _lockedPromotions.Clear();
                _transactionId = transaction.TransactionId;
            }
        }

        // Cho EF theo dõi voucher đã khóa; giữ thay đổi sau khi khóa.
        private Promotion? TrackLockedPromotion(Promotion? snapshot)
        {
            if (snapshot is null)
            {
                return null;
            }

            _context.ChangeTracker.DetectChanges();

            var entry = _context.ChangeTracker
                .Entries<Promotion>()
                .SingleOrDefault(
                    x => x.Entity.PromotionId == snapshot.PromotionId);

            if (entry is not null
                && entry.State is EntityState.Added or EntityState.Deleted)
            {
                throw new BusinessRuleException(
                    "Không thể thao tác voucher đang được thêm hoặc xóa.");
            }

            // Giữ các thay đổi phát sinh sau khi đã khóa trong cùng transaction.
            if (entry is not null
                && _lockedPromotions.TryGetValue(
                    snapshot.PromotionId, out var locked)
                && ReferenceEquals(entry.Entity, locked))
            {
                return locked;
            }

            var promotion = TrackFresh(
                snapshot,
                x => x.PromotionId == snapshot.PromotionId);

            _lockedPromotions[promotion.PromotionId] = promotion;

            return promotion;
        }

        // Làm mới entity từ SQL; không âm thầm ghi đè thay đổi chưa lưu.
        private T TrackFresh<T>(T snapshot, Func<T, bool> matches) where T : class
        {
            var entry = _context.ChangeTracker
                .Entries<T>()
                .SingleOrDefault(x => matches(x.Entity));

            if (entry is null)
            {
                _context.Attach(snapshot);
                return snapshot;
            }

            if (entry.State != EntityState.Unchanged)
            {
                throw new BusinessRuleException(
                    "Dữ liệu đang có thay đổi chưa lưu trước khi lấy khóa.");
            }

            entry.CurrentValues.SetValues(snapshot);
            entry.OriginalValues.SetValues(snapshot);
            entry.State = EntityState.Unchanged;

            return entry.Entity;
        }
    }
}
