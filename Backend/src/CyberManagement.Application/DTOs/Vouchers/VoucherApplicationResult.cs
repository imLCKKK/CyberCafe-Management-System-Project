using System;
using System.Collections.Generic;
using System.Text;

namespace CyberManagement.Application.DTOs.Vouchers
{
    // Kết quả nội bộ: snapshot voucher và tiền giảm phân bổ cho đơn.
    public sealed record VoucherApplicationResult(
        int PromotionId,
        string Code,
        string DiscountType,
        decimal DiscountValue,
        decimal Subtotal,
        decimal DiscountAmount,
        decimal TotalAmount,
        IReadOnlyList<VoucherLineDiscountDto> Lines);

    // Số tiền giảm được phân bổ cho toàn bộ một dòng món.
    public sealed record VoucherLineDiscountDto(
        int OrderDetailId,
        decimal DiscountAmount);
}
