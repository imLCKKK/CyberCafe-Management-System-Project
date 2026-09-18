using CyberManagement.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Text;

namespace CyberManagement.Application.Interfaces.Services
{
    public interface IInvoiceService
    {
        Task<InvoiceDTOs?> GetByIdAsync(int id);

        Task<InvoiceDTOs?> GetByOrderIdAsync(int orderId);

        Task<IEnumerable<InvoiceDTOs>> GetAllAsync(); 
    }
}
