using CyberManagement.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Text;

namespace CyberManagement.Application.Interfaces.Repositories
{
    // Quản lý lịch sử dùng và hoàn lượt voucher.
    public interface IPromotionUsageRepository
    {
        // Đọc lịch sử sử dụng gắn với một đơn.
        Task<PromotionUsage?> GetByOrderIdAsync(int orderId, CancellationToken cancellationToken = default);

        // Có lịch sử thì không được đổi mã voucher.
        Task<bool> HasHistoryAsync(int promotionId, CancellationToken cancellationToken = default);

        // Đếm lượt đang dùng toàn hệ thống: ReleasedAt bằng null.
        Task<int> CountActiveAsync(int promotionId, CancellationToken cancellationToken = default);
        // Đếm lượt đang dùng của một khách cho voucher này.
        Task<int> CountActiveByCustomerAsync(int promotionId, int customerId, CancellationToken cancellationToken = default);

        // Thêm một lượt sử dụng, chưa tự commit.
        Task AddAsync(PromotionUsage usage, CancellationToken cancellationToken = default);

        // Đánh dấu hoàn lượt đúng một lần, giữ lại lịch sử.
        // Yêu cầu đã khóa đơn và voucher trong transaction chung.
        Task<bool> ReleaseAsync(int promotionUsageId,  DateTimeOffset releasedAt, CancellationToken cancellationToken = default);
    }
}
