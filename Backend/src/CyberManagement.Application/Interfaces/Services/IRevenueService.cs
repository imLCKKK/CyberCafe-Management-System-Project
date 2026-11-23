using CyberManagement.Application.DTOs.Revenue;
using System;
using System.Collections.Generic;
using System.Text;

namespace CyberManagement.Application.Interfaces.Services
{
    public interface IRevenueService
    {
        // Tổng hợp doanh thu theo khoảng ngày và nguồn dịch vụ.
        Task<RevenueReportDto> GetRevenueAsync(
            RevenueFilterRequest request,
            int employeeId,
            CancellationToken cancellationToken = default);

        // Đối chiếu tiền, đơn/phiên, kho, voucher và hóa đơn.
        Task<ReconciliationReportDto> GetReconciliationAsync(
            RevenueFilterRequest request,
            int employeeId,
            CancellationToken cancellationToken = default);
    }
}
