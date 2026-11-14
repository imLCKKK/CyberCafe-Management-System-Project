using CyberManagement.Application.DTOs.Inventory;
using System;
using System.Collections.Generic;
using System.Text;

namespace CyberManagement.Application.Interfaces.Services
{
    public interface IInventoryService
    {
        // Đọc lịch sử biến động kho.
        Task<IReadOnlyList<InventoryMovementDto>> GetMovementsAsync(CancellationToken cancellationToken = default);

        // Nhập hoặc điều chỉnh tồn thủ công.
        Task<InventoryMovementDto> CreateMovementAsync(CreateInventoryMovementRequest request,int employeeId,CancellationToken cancellationToken = default);

        // Bật/tắt trạng thái bán, giữ nguyên tồn và giá.
        Task<InventoryItemDto> UpdateProductStatusAsync(
            int productId,
            UpdateProductStatusRequest request,
            CancellationToken cancellationToken = default);

        // Luồng đặt món gọi để trừ tồn.
        Task<InventoryMovementDto> ApplySaleAsync(
            int orderDetailId,
            int productId,
            int quantity,
            int? employeeId,
            string operationKey,
            CancellationToken cancellationToken = default);

        // Luồng hủy/hết hạn đơn gọi để hoàn tồn.
        Task<InventoryMovementDto> ApplyReturnAsync(
            int orderDetailId,
            int productId,
            int quantity,
            int? employeeId,
            string operationKey,
            CancellationToken cancellationToken = default);
    }
}
