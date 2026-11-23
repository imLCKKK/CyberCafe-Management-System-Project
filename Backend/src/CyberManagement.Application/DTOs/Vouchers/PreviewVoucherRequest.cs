using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json.Serialization;

namespace CyberManagement.Application.DTOs.Vouchers
{
    // Giỏ hàng và mã cần tính thử; chưa ghi hoặc giữ lượt dùng.
    [JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
    public sealed record PreviewVoucherRequest
    {
        public required string Code { get; init; }

        public required List<VoucherCartItem> Items { get; init; }
    }

    // Một món trong giỏ; giá do server đọc từ database.
    [JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
    public sealed record VoucherCartItem
    {
        public required int ProductId { get; init; }
        public required int Quantity { get; init; }
    }
}
