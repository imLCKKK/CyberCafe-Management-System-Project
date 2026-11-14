using System.Text.Json.Serialization;

namespace CyberManagement.Application.DTOs.Customers;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CreateCounterTopUpCommand(decimal Amount);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ApproveCounterTopUpCommand(bool CashReceived, decimal CashAmount);

public sealed record CounterTopUpOptionDto(decimal Amount, decimal BonusAmount, decimal TotalAmount);

public sealed record CounterTopUpDto(int RequestId, int CustomerId, string CustomerName, string CustomerPhone,
    decimal Amount, decimal BonusAmount, decimal TotalAmount, string Status, DateTime CreatedAtUtc,
    DateTime? ApprovedAtUtc, int? ApprovedByEmployeeId, int? TransactionId, decimal? BalanceAfter);

public sealed record CounterTopUpPageDto(IReadOnlyList<CounterTopUpDto> Items, int Page, int PageSize, int TotalCount);
public sealed record CounterTopUpResult(CounterTopUpDto Request, bool Replayed);
