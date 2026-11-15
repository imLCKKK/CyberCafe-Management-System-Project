using CyberManagement.Application.Interfaces.Repositories;
using CyberManagement.Domain.Entities;
using CyberManagement.Infrastructure.Persistance;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Text;

namespace CyberManagement.Infrastructure.Repositories
{
    // Ghi nhận, đếm và hoàn lượt sử dụng voucher.
    public sealed class PromotionUsageRepository : IPromotionUsageRepository
    {
        private readonly ApplicationDbContext _context;

        public PromotionUsageRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        // Tìm lượt dùng của đơn, kể cả lượt đã hoàn.
        public Task<PromotionUsage?> GetByOrderIdAsync(int orderId, CancellationToken cancellationToken = default)
        {
            return _context.PromotionUsages
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    x => x.OrderId == orderId,
                    cancellationToken);
        }

        // Kiểm tra voucher đã từng được dùng hay chưa.
        public Task<bool> HasHistoryAsync(int promotionId, CancellationToken cancellationToken = default)
        {
            // Tính cả lượt đã hoàn vì vẫn là lịch sử sử dụng.
            return _context.PromotionUsages.AnyAsync(
                x => x.PromotionId == promotionId,
                cancellationToken);
        }

        // Đếm tổng lượt đang dùng trên toàn hệ thống, chưa được hoàn.
        public Task<int> CountActiveAsync(int promotionId, CancellationToken cancellationToken = default)
        {
            return _context.PromotionUsages.CountAsync(
                x => x.PromotionId == promotionId
                    && x.ReleasedAt == null,
                cancellationToken);
        }

        // Đếm lượt đang dùng của một khách cho voucher này.
        public Task<int> CountActiveByCustomerAsync(int promotionId, int customerId, CancellationToken cancellationToken = default)
        {
            return _context.PromotionUsages.CountAsync(
                x => x.PromotionId == promotionId
                    && x.CustomerId == customerId
                    && x.ReleasedAt == null,
                cancellationToken);
        }

        // Thêm lượt dùng vào EF, chờ SaveChangesAsync lưu xuống database.
        public async Task AddAsync(PromotionUsage usage, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(usage);
            RequireTransaction();

            await _context.PromotionUsages.AddAsync(
                usage,
                cancellationToken);
        }

        // Hoàn lượt trực tiếp trong database; true nếu vừa hoàn thành công.
        public async Task<bool> ReleaseAsync(int promotionUsageId, DateTimeOffset releasedAt, CancellationToken cancellationToken = default)
        {
            RequireTransaction();

            // Chỉ cập nhật khi lượt chưa được hoàn.
            var affected = await _context.PromotionUsages
                .Where(x => x.PromotionUsageId == promotionUsageId
                    && x.ReleasedAt == null)
                .ExecuteUpdateAsync(
                    setters => setters.SetProperty(
                        x => x.ReleasedAt,
                        (DateTimeOffset?)releasedAt),
                    cancellationToken);

            return affected == 1;
        }

        // Chỉ cho ghi hoặc hoàn lượt khi đã mở transaction.
        private void RequireTransaction()
        {
            if (_context.Database.CurrentTransaction is null)
            {
                throw new InvalidOperationException(
                    "Phải mở transaction trước khi ghi hoặc hoàn lượt voucher.");
            }
        }

    }
}
