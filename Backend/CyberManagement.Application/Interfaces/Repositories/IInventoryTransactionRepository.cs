using CyberManagement.Application.DTOs;
using CyberManagement.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Text;

namespace CyberManagement.Application.Interfaces.Repositories
{
    public interface IInventoryTransactionRepository
    {
        // Thêm vào DbContext, chưa tự SaveChanges hoặc commit.
        Task AddAsync(InventoryTransaction transaction);
        // Giữ lại hợp đồng hiện có để các phần dùng chung không bị đổi.
        Task<IEnumerable<InventoryTransaction>> GetByProductIdAsync(long productId);
        // API lịch sử kho: tối đa 500 dòng mới nhất.
        Task<IReadOnlyList<InventoryMovementDto>> GetRecentAsync (CancellationToken cancellationToken = default);
    }
}
