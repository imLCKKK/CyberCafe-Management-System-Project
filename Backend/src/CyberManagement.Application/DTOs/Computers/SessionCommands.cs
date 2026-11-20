using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace CyberManagement.Application.DTOs.Computers;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record OpenSessionCommand
{
    [Range(1, int.MaxValue)] public int ComputerId { get; init; }
    [Range(1, int.MaxValue)] public int CustomerId { get; init; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CloseSessionCommand;

public sealed record SessionDto(int GamingSessionId, int ComputerId, int CustomerId,
    string ComputerName, string TypeName, string? CPU, string? RAM, string? GPU,
    DateTimeOffset StartTimeUtc, DateTimeOffset? EndTimeUtc, DateTimeOffset? LastChargedAtUtc,
    decimal? PricePerHourSnapshot, decimal TotalCost, string Status,
    int? OpenedByEmployeeId, string? ClosedByActorType, string? CloseReason, int? InvoiceId);

public sealed record SessionHomeDto(decimal WalletBalance, SessionDto? Session);

public sealed record SessionChange(int CustomerId, int ComputerId, int GamingSessionId);

// Internal result captured under the session lock, in the caller's transaction.
public sealed record SessionSettlementResult(int GamingSessionId, string Status, DateTime? EndTime);
