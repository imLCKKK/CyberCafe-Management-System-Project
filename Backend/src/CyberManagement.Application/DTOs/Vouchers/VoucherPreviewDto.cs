using System;
using System.Collections.Generic;
using System.Text;

namespace CyberManagement.Application.DTOs.Vouchers
{
    // Kết quả tính thử; TotalAmount = Subtotal - DiscountAmount.
    public sealed record VoucherPreviewDto(
        string Code,
        decimal Subtotal,
        decimal DiscountAmount,
        decimal TotalAmount);
}
