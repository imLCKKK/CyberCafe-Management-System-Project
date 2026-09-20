using CyberManagement.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Text;

namespace CyberManagement.Application.Interfaces.Repositories
{
    public interface IOrderRepository
    {
        Task<Order?> GetWithDetailsByIdAsync(int id);// Lấy Order kèm theo OrderDetails (cần include thêm bảng Product) (dùng Include trong EF Core)
        Task<IEnumerable<Order>> GetAllAsync();
        Task<IEnumerable<Order>> GetByCustomerIdAsync(int customerId);
        Task<Order> AddAsync(Order order);
        Task UpdateAsync(Order order);

        Task<IEnumerable<Order>> GetOrdersByStatusAsync(string status); //dùng hàm này khi cần sản phẩm chờ được xử lý (ví dụ: pha chế đồ uống, nấu,...)
    }
}
