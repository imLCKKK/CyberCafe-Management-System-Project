using System;
using System.Collections.Generic;
using System.Text;

namespace CyberManagement.Application.DTOs.Vouchers
{
    // Thông tin voucher trả về cho màn hình quản trị.
    public sealed record VoucherDto(
        int Id,
        string Code,
        string Name,
        string DiscountType,
        decimal DiscountValue,
        decimal MinOrderAmount,
        decimal? MaxDiscountAmount,
        DateTimeOffset StartDate,
        DateTimeOffset EndDate,
        int? TotalUsageLimit,
        int? PerCustomerUsageLimit,
        bool IsActive);
}
