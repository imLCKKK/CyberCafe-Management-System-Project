using CyberManagement.Application.DTOs.Inventory;
using CyberManagement.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Text;

namespace CyberManagement.Application.Interfaces.Repositories
{
    public interface IInventoryMovementRepository
    {
        // Đọc tối đa 500 biến động mới nhất.
        Task<IReadOnlyList<InventoryMovementDto>> GetRecentAsync(
            CancellationToken cancellationToken = default);

        // Tìm kết quả của thao tác đã xử lý để chống gửi lặp.
        Task<InventoryTransaction?> GetByOperationKeyAsync(
            string operationKey,
            CancellationToken cancellationToken = default);

        // Kiểm tra dòng món đã xuất/hoàn tồn hay chưa.
        Task<InventoryTransaction?> GetByOrderDetailAsync(
            int orderDetailId,
            string type,
            CancellationToken cancellationToken = default);

        // Đưa biến động vào DbContext; bước lưu do Unit of Work phụ trách.
        Task AddAsync(InventoryTransaction movement, CancellationToken cancellationToken = default);
    }
}
