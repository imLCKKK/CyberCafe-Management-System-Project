using CyberManagement.Application.DTOs;
using System;
using System.Collections.Generic;
using System.Text;

namespace CyberManagement.Application.Interfaces.Services
{
    public interface IOrderService
    {
        Task<OrderDTOs?> GetByIdAsync(long id);
        Task<IEnumerable<OrderDTOs>> GetAllAsync(string? status = null);
        Task<IEnumerable<OrderDTOs>> GetMineAsync();
        Task<OrderItemCancelRequestDTO?> GetCancellationRequestAsync(long orderId, long orderDetailId, long requestId);
        Task<IEnumerable<OrderDTOs>> GetByCustomerIdAsync(long customerId);
        Task<IEnumerable<OrderDTOs>> GetByStatusAsync(string status);
        Task<IEnumerable<OrderStatusHistoryDTOs>> GetStatusHistoryAsync(long orderId);
        Task ConfirmItemAsync(long orderId, long orderDetailId);
        Task CancelItemAsync(long orderId, long orderDetailId, string reason);
        Task<OrderItemCancelRequestDTO> RequestItemCancellationAsync(long orderId, long orderDetailId);
        Task RejectItemCancellationAsync(long orderId, long orderDetailId, long requestId);

        // Khách đã xác thực phải có phiên đang hoạt động; trừ ví và giữ tồn kho trong cùng transaction.
        Task<OrderDTOs> CreateOrderAsync(CreateOrderRequest request);

        [Obsolete("Use ConfirmItemAsync(orderId, orderDetailId).")]
        Task<bool> ConfirmOrderAsync(long orderId, string paymentMethod);

        [Obsolete("Use CancelItemAsync(orderId, orderDetailId, reason).")]
        Task<bool> CancelOrderAsync(long orderId);
    }
}
