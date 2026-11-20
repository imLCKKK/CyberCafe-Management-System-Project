using CyberManagement.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Text;

namespace CyberManagement.Application.Interfaces.Repositories
{
    // Dữ liệu SQL cần dùng để tính doanh thu và đối soát.
    // Đây là dữ liệu nội bộ, không trả trực tiếp ra API.
    public sealed record RevenueReadSet(
        IReadOnlyList<Payment> Payments,
        IReadOnlyList<WalletTransaction> WalletTransactions,
        IReadOnlyList<CounterTopUpRequest> CounterTopUps,
        IReadOnlyList<Order> Orders,
        IReadOnlyList<SessionCharge> SessionCharges,
        IReadOnlyList<InventoryTransaction> InventoryTransactions,
        IReadOnlyList<PromotionUsage> PromotionUsages,
        IReadOnlyList<Invoice> Invoices);

    public interface IRevenueQueries
    {
        // Đọc nhân viên và quyền hiện tại để kiểm tra quyền báo cáo.
        Task<Employee?> GetEmployeeAsync(
            int employeeId,
            CancellationToken cancellationToken = default);

        // Đọc dữ liệu nhất quán phục vụ báo cáo và đối soát.
        // Khoảng thời gian gồm fromInclusive, không gồm toExclusive.
        Task<RevenueReadSet> ReadAsync(
            DateTimeOffset fromInclusive,
            DateTimeOffset toExclusive,
            CancellationToken cancellationToken = default);
    }
}
