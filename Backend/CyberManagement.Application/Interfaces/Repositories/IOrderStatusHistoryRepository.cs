using CyberManagement.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Text;

namespace CyberManagement.Application.Interfaces.Repositories
{
    public interface IOrderStatusHistoryRepository
    {
        Task AddAsync(OrderStatusHistory history);

        Task<IEnumerable<OrderStatusHistory>> GetByOrderIdAsync(int orderId);
    }
}
