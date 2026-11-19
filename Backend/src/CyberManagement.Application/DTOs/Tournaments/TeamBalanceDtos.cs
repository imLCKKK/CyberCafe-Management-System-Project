namespace CyberManagement.Application.DTOs.Tournaments;

public static class TeamBalanceVersions
{
    public const string Contract = "team-balance.v1";
    public const string Feature = "opgg-competitive.v1";
    public const string Policy = "valorant-5v5.v1";
    public const string Baseline = "rank-snake-v1";
}

public sealed record TeamBalanceFeePolicy(decimal EntryFee, DateTimeOffset? FullRefundBeforeUtc, decimal RefundPercentAfter);

// Internal input populated by the tournament server, never by a public request body.
public sealed record TeamBalanceParticipant(
    int RegistrationId, int CustomerId, string CustomerStatus, string RegistrationStatus,
    decimal? FeeCharged, decimal? FeeRefunded, bool FeeEligible,
    string RiotId, string PreferredRole, OpggSnapshot Snapshot);

public sealed record TeamBalanceInput(
    string ContractVersion, string FeatureVersion, string PolicyVersion, string Strategy,
    int TournamentId, int GameId, string GameKey, string Status, int TeamSize, int TeamCount,
    bool HasConfirmedTeams, string ExpectedVersion, string RosterVersion,
    DateTimeOffset GeneratedAtUtc, TeamBalanceFeePolicy FeePolicy,
    IReadOnlyList<TeamBalanceParticipant> Participants);

public sealed record TeamBalanceMember(int RegistrationId, string AssignedRole);
public sealed record TeamBalanceTeam(string TeamKey, IReadOnlyList<TeamBalanceMember> Members);
public sealed record TeamBalanceQuality(
    double? PredictedWinProbabilityA, double? PredictedImbalance, double RankMeanGap, bool RoleCoverageValid);

public sealed record TeamBalanceProposal(
    string ContractVersion, int TournamentId, string RosterVersion, string ProposalId, string Method,
    string? ModelVersion, string? ModelSha256, string FeatureVersion, string PolicyVersion,
    string? DatasetVersion, string? BaselineVersion, IReadOnlyList<TeamBalanceTeam> Teams,
    TeamBalanceQuality Quality, IReadOnlyList<string> Warnings, bool Persisted);

public sealed record TeamBalancePlayerStats(
    int RankTier, int Matches, string Scope, double? WinRate, double? HeadshotRate,
    double? DamagePerRound, double? Kda, IReadOnlyList<string> Warnings);
