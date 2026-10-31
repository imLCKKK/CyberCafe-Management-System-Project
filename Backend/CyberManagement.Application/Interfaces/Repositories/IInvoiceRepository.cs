using CyberManagement.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Text;

namespace CyberManagement.Application.Interfaces.Repositories
{
    public interface IInvoiceRepository
    {
        // Các hàm đọc phải Include Payments và InvoiceDetails để ánh xạ sang DTO.
        Task<Invoice?> GetByIdAsync(long id);
        Task<IEnumerable<Invoice>> GetAllAsync();
        Task AddAsync(Invoice invoice);
        Task<IEnumerable<Invoice>> GetByDateRangeAsync(DateTime fromDate, DateTime toDate); // Thống kê doanh thu theo ngày/ca
    }
}
