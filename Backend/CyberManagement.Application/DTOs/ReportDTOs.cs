using System.ComponentModel.DataAnnotations;
using System.Globalization;

namespace CyberManagement.Application.DTOs;

public sealed class ReportQueryRequest : IValidatableObject
{
    // Giữ chuỗi để kiểm tra chính xác YYYY-MM-DD; không nhận ngày sai lịch.
    public string? From { get; init; }
    public string? To { get; init; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        DateOnly? from = null;
        DateOnly? to = null;

        if (From is not null)
        {
            if (DateOnly.TryParseExact(From, "yyyy-MM-dd", CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out var parsed))
                from = parsed;
            else
                yield return new ValidationResult(
                    "From must be a valid YYYY-MM-DD date.", [nameof(From)]);
        }

        if (To is not null)
        {
            if (DateOnly.TryParseExact(To, "yyyy-MM-dd", CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out var parsed))
                to = parsed;
            else
                yield return new ValidationResult(
                    "To must be a valid YYYY-MM-DD date.", [nameof(To)]);
        }

        if (from.HasValue && to.HasValue && from.Value > to.Value)
            yield return new ValidationResult(
                "From must not be after To.", [nameof(From), nameof(To)]);
    }
}

public sealed class RevenueByTypeDto
{
    public string Type { get; init; } = string.Empty;
    public decimal Amount { get; init; }
}

public sealed class ReportResponse
{
    // Các số đếm hệ thống hiện tại không lọc theo khoảng ngày, trừ Orders.
    public long Customers { get; init; }
    public long ActiveSessions { get; init; }
    public long Computers { get; init; }
    public long Orders { get; init; }
    public long PendingTopUps { get; init; }
    public List<RevenueByTypeDto> Revenue { get; init; } = new();
    public decimal Refunds { get; init; }
    public decimal CashRevenue { get; init; }
    public decimal NetRevenue { get; init; }
}