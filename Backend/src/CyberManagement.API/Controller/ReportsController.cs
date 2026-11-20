using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Security.Claims;
using CyberManagement.Application.DTOs.Revenue;
using CyberManagement.Application.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CyberManagement.API.Controller;

[ApiController]
[Route("api/admin/reports")]
[Authorize(Policy = "permission:report.view")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
[ProducesResponseType(StatusCodes.Status400BadRequest)]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
[ProducesResponseType(StatusCodes.Status403Forbidden)]
[ProducesResponseType(StatusCodes.Status409Conflict)]
public sealed class ReportsController(IRevenueService revenue) : ControllerBase
{
    // Báo cáo thu/hoàn dịch vụ, tách riêng tiền nạp ví.
    [HttpGet]
    [ProducesResponseType<RevenueReportDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<RevenueReportDto>> GetRevenue(CancellationToken cancellationToken,
        [FromQuery] string? from = null, [FromQuery] string? to = null)
    {
        var employeeId = EmployeeId();
        return Ok(await revenue.GetRevenueAsync(Filter(from, to), employeeId, cancellationToken));
    }

    // Đọc sai lệch và bằng chứng giao dịch; không sửa nguồn dữ liệu.
    [HttpGet("reconciliation")]
    [ProducesResponseType<ReconciliationReportDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ReconciliationReportDto>> GetReconciliation(CancellationToken cancellationToken,
        [FromQuery] string? from = null, [FromQuery] string? to = null)
    {
        var employeeId = EmployeeId();
        return Ok(await revenue.GetReconciliationAsync(Filter(from, to), employeeId, cancellationToken));
    }

    private RevenueFilterRequest Filter(string? from, string? to)
    {
        var today = DateOnly.FromDateTime(DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(7)).DateTime);
        var end = Date("to", to) ?? today;
        var start = Date("from", from) ?? new DateOnly(end.Year, end.Month, 1);
        return new() { From = start, To = end };
    }

    private DateOnly? Date(string name, string? value)
    {
        if (!Request.Query.TryGetValue(name, out var values)) return null;
        if (values.Count != 1 || !DateOnly.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture,
            DateTimeStyles.None, out var date))
            throw new ValidationException($"{name} phải có đúng một ngày dạng YYYY-MM-DD.");
        return date;
    }

    // JWT nhân viên hiện dùng sub; không nhận employeeId từ query hoặc tài khoản khách.
    private int EmployeeId()
    {
        if (User.FindFirstValue("account_type") is not null || User.FindFirstValue("customer_id") is not null
            || User.IsInRole("Customer") || string.IsNullOrWhiteSpace(User.FindFirstValue("role"))
            || User.FindAll("sub").Count() != 1
            || !int.TryParse(User.FindFirstValue("sub"), NumberStyles.None, CultureInfo.InvariantCulture, out var id) || id <= 0)
            throw new UnauthorizedAccessException("Danh tính nhân viên không hợp lệ.");
        return id;
    }
}
