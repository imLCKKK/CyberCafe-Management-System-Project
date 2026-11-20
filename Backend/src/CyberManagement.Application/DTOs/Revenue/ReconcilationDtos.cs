using System;
using System.Collections.Generic;
using System.Text;

namespace CyberManagement.Application.DTOs.Revenue
{

    // Bản ghi liên quan, dùng để truy lại dữ liệu khi phát hiện sai lệch.
    public sealed record ReconciliationReferenceDto(
        string EntityType,
        int EntityId);

    // Một sai lệch được phát hiện khi đối soát.
    public sealed record ReconciliationIssueDto(
        string Code,
        string Message,
        decimal? ExpectedValue,
        decimal? ActualValue,
        string? Unit,
        IReadOnlyList<ReconciliationReferenceDto> References);

    // Các khoản thành công trong kỳ dùng giờ hệ thống xác nhận thay cho giờ giao dịch từ provider.
    public sealed record ReconciliationConfirmationSummaryDto(
        int TransactionCount,
        decimal CollectedAmount,
        decimal RefundedAmount);

    // Toàn bộ kết quả đối soát trong khoảng ngày đã chọn.
    public sealed record ReconciliationReportDto(
        DateOnly From,
        DateOnly To,
        int CheckedOrders,
        int CheckedSessionCharges,
        int CheckedInvoices,
        IReadOnlyList<ReconciliationIssueDto> Issues,
        IReadOnlyList<RevenueTransactionDto> Transactions,
        ReconciliationConfirmationSummaryDto ConfirmationSummary)
    {
        // Số sai lệch được phát hiện.
        public int IssueCount => Issues.Count;

        // Không phát hiện sai lệch trong phạm vi đã kiểm tra.
        public bool IsBalanced => IssueCount == 0;
    }
}
