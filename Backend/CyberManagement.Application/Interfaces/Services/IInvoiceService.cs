using CyberManagement.Application.DTOs;
using System;
using System.Collections.Generic;
using System.Text;

namespace CyberManagement.Application.Interfaces.Services
{
    public interface IInvoiceService
    {
        Task<SessionStatementDTO?> GetSessionStatementAsync(long sessionId);
        Task<InvoiceDTOs?> GetByIdAsync(long id);

        Task<InvoiceDTOs?> GetByOrderIdAsync(long orderId);

        Task<IEnumerable<InvoiceDTOs>> GetAllAsync(); 
    }
}
