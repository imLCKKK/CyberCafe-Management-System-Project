using CyberManagement.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Text;

namespace CyberManagement.Application.Interfaces.Repositories
{
    public interface IOrderRepository
    {
        // Include OrderDetails.Product và OrderDetails.CancellationRequests khi lấy đơn hàng.
        Task<Order?> GetWithDetailsByIdAsync(long id);
        Task<IEnumerable<Order>> GetAllAsync();
        Task<IEnumerable<Order>> GetByCustomerIdAsync(long customerId);
        // Trả về OrderId và các OrderDetailId đã được tạo trong transaction xử lý đơn hàng.
        Task<Order> AddAsync(Order order);
        Task UpdateAsync(Order order);

        Task<IEnumerable<Order>> GetOrdersByStatusAsync(string status); //dùng hàm này khi cần sản phẩm chờ được xử lý (ví dụ: pha chế đồ uống, nấu,...)
    }
}
