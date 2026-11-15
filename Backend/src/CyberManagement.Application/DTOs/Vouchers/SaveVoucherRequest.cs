using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json.Serialization;

namespace CyberManagement.Application.DTOs.Vouchers
{
    // Dữ liệu tạo/sửa voucher; từ chối trường JSON không khai báo.
    [JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
    public sealed record SaveVoucherRequest
    {
        public required string Code { get; init; }
        public required string Name { get; init; }

        // Percent: giảm theo %; Fixed: giảm số tiền cố định.
        public required string DiscountType { get; init; }
        public required decimal DiscountValue { get; init; }

        // Ngưỡng tiền món trước khi giảm.
        public required decimal MinOrderAmount { get; init; }

        // Null nghĩa là không đặt trần giảm.
        public required decimal? MaxDiscountAmount { get; init; }

        // Có hiệu lực từ StartDate đến trước EndDate.
        public required DateTimeOffset StartDate { get; init; }
        public required DateTimeOffset EndDate { get; init; }

        // Null nghĩa là không giới hạn lượt tương ứng.
        public required int? TotalUsageLimit { get; init; }
        public required int? PerCustomerUsageLimit { get; init; }

        public required bool IsActive { get; init; }
    }
}
