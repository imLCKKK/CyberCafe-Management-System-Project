using CyberManagement.Application.DTOs.Revenue;
using CyberManagement.Application.Exceptions;
using CyberManagement.Application.Interfaces.Repositories;
using CyberManagement.Application.Interfaces.Services;
using CyberManagement.Application.Interfaces.Validation;

namespace CyberManagement.Application.Services;

public sealed class RevenueService(IRevenueQueries queries,
    IRequestValidator<RevenueFilterRequest> validator) : IRevenueService
{
    private static readonly TimeSpan VietnamOffset = TimeSpan.FromHours(7);

    // Chỉ cộng nguồn tiền đã xác định, không dùng tổng hóa đơn để thay khoản thực thu.
    public async Task<RevenueReportDto> GetRevenueAsync(RevenueFilterRequest request, int employeeId,
        CancellationToken cancellationToken = default)
    {
        var (data, from, to) = await ReadAsync(request, employeeId, cancellationToken);
        var money = RevenueAccounting.Analyze(data, from, to);
        if (money.Issues.Count != 0)
            throw new BusinessRuleException("Chưa đủ căn cứ tính doanh thu. Hãy xem đối soát: "
                + string.Join(", ", money.Issues.Select(x => x.Code).Distinct()));
        var entries = money.Entries.Where(x => InPeriod(x.Transaction.PaymentTime, from, to)).ToArray();
        var sources = new[] { "Food", "Gaming" }.Select(source =>
        {
            var rows = entries.Where(x => x.Source == source).Select(x => x.Transaction).ToArray();
            var collected = rows.Where(x => x.Type == "Payment").Sum(x => x.Amount);
            var refunded = rows.Where(x => x.Type == "Refund").Sum(x => x.Amount);
            return new RevenueSourceDto(source, collected, refunded, collected - refunded);
        }).ToArray();
        var daily = Enumerable.Range(0, request.To.DayNumber - request.From.DayNumber + 1).Select(offset =>
        {
            var date = request.From.AddDays(offset);
            decimal Net(string source) => entries.Where(x => x.Source == source
                && DateOnly.FromDateTime(x.Transaction.PaymentTime!.Value.ToOffset(VietnamOffset).DateTime) == date)
                .Sum(x => x.Transaction.Type == "Payment" ? x.Transaction.Amount : -x.Transaction.Amount);
            var food = Net("Food"); var gaming = Net("Gaming");
            return new RevenueDailyDto(date, food, gaming, food + gaming);
        }).ToArray();
        var totalCollected = sources.Sum(x => x.CollectedAmount);
        var totalRefunded = sources.Sum(x => x.RefundedAmount);
        return new(request.From, request.To, totalCollected, totalRefunded, totalCollected - totalRefunded,
            money.TopUpCollectedAmount, sources, daily);
    }

    // Trả sai lệch và giao dịch liên quan; không tự sửa tiền, tồn hoặc chứng từ.
    public async Task<ReconciliationReportDto> GetReconciliationAsync(RevenueFilterRequest request, int employeeId,
        CancellationToken cancellationToken = default)
    {
        var (data, from, to) = await ReadAsync(request, employeeId, cancellationToken);
        var money = RevenueAccounting.Analyze(data, from, to);
        var confirmation = money.Entries.Select(x => x.Transaction).Where(x =>
            x.PaymentTimeBasis == "Confirmation" && InPeriod(x.PaymentTime, from, to)).ToArray();
        return new(request.From, request.To, data.Orders.Count, data.SessionCharges.Count, data.Invoices.Count,
            RevenueReconciliation.Check(data, money), money.Transactions,
            new(confirmation.Length, confirmation.Where(x => x.Type == "Payment").Sum(x => x.Amount),
                confirmation.Where(x => x.Type == "Refund").Sum(x => x.Amount)));
    }

    private async Task<(RevenueReadSet Data, DateTimeOffset From, DateTimeOffset To)> ReadAsync(
        RevenueFilterRequest request, int employeeId, CancellationToken ct)
    {
        var employee = employeeId > 0 ? await queries.GetEmployeeAsync(employeeId, ct) : null;
        if (employee is null || !employee.IsActive || employee.EmployeeId != employeeId
            || employee.Role is null || !employee.Role.RolePermissions.Any(x => x.Permission?.PermissionName == "report.view"))
            throw new UnauthorizedAccessException("Bạn không có quyền xem báo cáo.");
        validator.Validate(request);
        var from = new DateTimeOffset(request.From.ToDateTime(TimeOnly.MinValue), VietnamOffset).ToUniversalTime();
        var to = new DateTimeOffset(request.To.AddDays(1).ToDateTime(TimeOnly.MinValue), VietnamOffset).ToUniversalTime();
        return (await queries.ReadAsync(from, to, ct), from, to);
    }

    internal static bool InPeriod(DateTimeOffset? time, DateTimeOffset from, DateTimeOffset to) => time >= from && time < to;
}
