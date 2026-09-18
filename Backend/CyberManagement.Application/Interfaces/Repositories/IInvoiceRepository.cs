using CyberManagement.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Text;

namespace CyberManagement.Application.Interfaces.Repositories
{
    public interface IInvoiceRepository
    {
        Task<Invoice?> GetByIdAsync(int id);
        Task<Invoice?> GetByOrderIdAsync(int orderId);
        Task AddAsync(Invoice invoice);
        Task<IEnumerable<Invoice>> GetByDateRangeAsync(DateTime fromDate, DateTime toDate); // Thống kê doanh thu theo ngày/ca
    }
}
