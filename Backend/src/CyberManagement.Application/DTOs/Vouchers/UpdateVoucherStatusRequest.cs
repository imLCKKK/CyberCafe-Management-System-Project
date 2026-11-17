using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json.Serialization;

namespace CyberManagement.Application.DTOs.Vouchers
{
    // Bật/tắt voucher cho đơn mới, giữ lịch sử đã có.
    [JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
    public sealed record UpdateVoucherStatusRequest
    {
        public required bool IsActive { get; init; }
    }
}
