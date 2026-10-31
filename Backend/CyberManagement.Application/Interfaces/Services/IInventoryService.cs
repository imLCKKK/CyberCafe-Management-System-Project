using CyberManagement.Application.DTOs;
using System;
using System.Collections.Generic;
using System.Text;

namespace CyberManagement.Application.Interfaces.Services
{
    public interface IInventoryService
    {
        // Xem tồn kho.
        //GET /api/admin/inventory
        Task<IReadOnlyList<InventoryItemDto>> GetInventoryAsync(CancellationToken cancellationToken = default);

        // Xem tối đa 500 biến động mới nhất.
        //GET /api/admin/inventory/movements
        Task<IReadOnlyList<InventoryMovementDto>> GetMovementsAsync(CancellationToken cancellationToken = default);

        // Nhập hàng hoặc điều chỉnh tồn.
        //POST /api/admin/inventory/movements
        Task<InventoryMovementDto> CreateMovementAsync(CreateInventoryMovementRequest request,CancellationToken cancellationToken = default);

        // Bật hoặc ngừng bán sản phẩm.
        //PUT /api/admin/products/{id}/status
        Task<InventoryItemDto> UpdateProductStatusAsync(long productId,UpdateProductStatusRequest request, CancellationToken cancellationToken = default);
    }
}
