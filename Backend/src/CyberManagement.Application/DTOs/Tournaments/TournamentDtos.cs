namespace CyberManagement.Application.DTOs.Tournaments;

public sealed record GameDto(int GameId, string GameName, int TeamSize, bool IsActive);

public sealed record GameRankDto(int RankId, int GameId, string RankName, int RankOrder);

public sealed record TournamentDto(
    int Id,
    int GameId,
    string GameName,
    int TeamSize,
    string Name,
    DateTime RegistrationStartUtc,
    DateTime RegistrationEndUtc,
    DateTime StartTimeUtc,
    decimal EntryFee,
    int RegisteredCount,
    string Status,
    int CreatedByEmployeeId);

public sealed record CreateTournamentRequest(
    int GameId,
    string Name,
    DateTime RegistrationStartUtc,
    DateTime RegistrationEndUtc,
    DateTime StartTimeUtc,
    decimal EntryFee);

public sealed record UpdateTournamentRequest(
    int GameId,
    string Name,
    DateTime RegistrationStartUtc,
    DateTime RegistrationEndUtc,
    DateTime StartTimeUtc,
    decimal EntryFee);

public sealed record TournamentRegistrationRequest(
    string RiotId,
    int RankId,
    string? PreferredRole,
    decimal? WinRate,
    int? HoursPlayed);

public sealed record TournamentRegistrationDto(
    int Id,
    int TournamentId,
    int CustomerId,
    string CustomerName,
    string? RiotId,
    int RankId,
    string RankName,
    string? PreferredRole,
    decimal WinRate,
    int HoursPlayed,
    decimal? SkillScore,
    string Status,
    DateTime RegisteredAtUtc);

public sealed record ClientTournamentRegistrationDto(
    int Id,
    string CustomerName,
    string? RiotId,
    string RankName,
    string? PreferredRole,
    decimal WinRate,
    int HoursPlayed,
    string Status,
    DateTime RegisteredAtUtc,
    bool IsCurrentCustomer);
