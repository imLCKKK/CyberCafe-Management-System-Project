using CyberManagement.Application.DTOs;
using CyberManagement.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Text;

namespace CyberManagement.Application.Interfaces.Repositories
{
    public interface IInventoryRepository
    {
        // Xem tồn, bao gồm sản phẩm hết hàng hoặc ngừng bán.
        Task<IReadOnlyList<InventoryItemDto>> GetInventoryAsync(CancellationToken cancellationToken = default);

        // Lấy sản phẩm có tracking, bảo vệ khỏi cập nhật đồng thời.
        // Chỉ gọi bên trong transaction của thao tác ghi.
        Task<Product?> GetProductForUpdateAsync(long productId,CancellationToken cancellationToken = default);
    }
}
