using CyberManagement.Application.DTOs;
using System;
using System.Collections.Generic;
using System.Text;

namespace CyberManagement.Application.Interfaces.Services
{
    public interface IInvoiceService
    {
        // Kiểm tra quyền Admin/Staff và lấy danh sách hóa đơn.
        Task<IReadOnlyList<InvoiceResponse>> GetAdminInvoicesAsync(CancellationToken cancellationToken = default);

        // Lấy hóa đơn của khách đang đăng nhập, không nhận ID từ client.
        Task<IReadOnlyList<InvoiceResponse>> GetMyInvoicesAsync(CancellationToken cancellationToken = default);
    }
}
