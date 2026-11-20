using System;
using System.Collections.Generic;
using System.Text;

namespace CyberManagement.Application.DTOs.Revenue
{
    // Khoảng ngày cần xem báo cáo.
    public sealed record RevenueFilterRequest
    {
        public DateOnly From { get; init; }
        public DateOnly To { get; init; }
    }

    // Thu và hoàn theo nguồn dịch vụ: Food hoặc Gaming.
    public sealed record RevenueSourceDto(
        string Source,
        decimal CollectedAmount,
        decimal RefundedAmount,
        decimal NetRevenue);

    // Thu ròng từng ngày, dùng cho bảng và biểu đồ.
    public sealed record RevenueDailyDto(
        DateOnly Date,
        decimal FoodRevenue,
        decimal GamingRevenue,
        decimal NetRevenue);

    // Truy vết khoản thu/hoàn và căn cứ thời gian đã lưu.
    // EntityType là Payment hoặc WalletTransaction; thời gian chưa có bằng chứng giữ null.
    public sealed record RevenueTransactionDto(
        string EntityType,
        int EntityId,
        int? OrderId,
        int? OrderDetailId,
        int? SessionChargeId,
        string Method,
        string Type,
        string Status,
        decimal Amount,
        DateTimeOffset? PaymentTime,
        DateTimeOffset? ConfirmedAt,
        DateTimeOffset? ProviderOccurredAt,
        string? PaymentTimeBasis);

    // Toàn bộ kết quả báo cáo doanh thu.
    // TopUpCollectedAmount là tiền nạp thực nhận, không gồm tiền tặng hoặc doanh thu dịch vụ.
    public sealed record RevenueReportDto(
        DateOnly From,
        DateOnly To,
        decimal CollectedAmount,
        decimal RefundedAmount,
        decimal NetRevenue,
        decimal TopUpCollectedAmount,
        IReadOnlyList<RevenueSourceDto> Sources,
        IReadOnlyList<RevenueDailyDto> Daily);
}
