using CyberManagement.Application.DTOs;
using CyberManagement.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Text;

namespace CyberManagement.Application.Interfaces.Repositories
{
    public interface IInvoiceRepository
    {
        // Lấy tối đa 500 hóa đơn mới nhất cho quản trị.
        Task<IReadOnlyList<InvoiceResponse>> GetRecentAsync(CancellationToken cancellationToken = default);

        // Lấy tối đa 200 hóa đơn mới nhất của một khách hàng.
        Task<IReadOnlyList<InvoiceResponse>> GetByCustomerIdAsync(long customerId, CancellationToken cancellationToken = default);
    }
}
