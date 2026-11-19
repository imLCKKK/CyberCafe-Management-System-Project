using CyberManagement.Application.DTOs.Tournaments;

namespace CyberManagement.TeamBalancing.Training;

public sealed record DatasetSource(
    string DatasetVersion, string FeatureVersion, string SourceUrl, string UsageTerms,
    string CollectorCommit, DateTimeOffset CollectedAtUtc, string MatchesSha256, string DataKind = "unverified");

public sealed record MatchPlayer(string PlayerKey, string PreferredRole, OpggSnapshot Snapshot);

public sealed record MatchRecord(
    string MatchId, IReadOnlyList<string> Aliases, string GroupId, string Scope, string Patch,
    DateTimeOffset StartedAtUtc, DateTimeOffset CompletedAtUtc,
    string ResultOrigin, string ResultSourceUrl, DateTimeOffset ResultVerifiedAtUtc,
    string Winner, IReadOnlyList<MatchPlayer> TeamA, IReadOnlyList<MatchPlayer> TeamB);

public sealed record PreparedMatch(
    string MatchId, string GroupId, DateTimeOffset StartedAtUtc, bool Label, double[] Features);

public sealed record SplitMatch(
    string MatchId, IReadOnlyList<string> Aliases, string GroupId, string[] PlayerKeys,
    DateTimeOffset StartedAtUtc, DateTimeOffset ResultVerifiedAtUtc, string[] SnapshotHashes);

public sealed record FoldManifest(string Name, int Count, int PositiveLabels, IReadOnlyList<SplitMatch> Matches);

public sealed record PreparationResult(
    DatasetSource Source, string SourceManifestSha256, string CodeCommit, string Runtime,
    string ToolAssemblySha256, string ApplicationAssemblySha256,
    IReadOnlyList<string> SupportedScopes, IReadOnlyList<string> SupportedPatches,
    DateTimeOffset TrainBeforeUtc, DateTimeOffset TestFromUtc, int OriginalMatches, int UniquePlayers,
    int DuplicateRowsRemoved, IReadOnlyList<string> ExcludedMatches, IReadOnlyList<string> QuarantinedGroups,
    IReadOnlyList<FoldManifest> Folds, IReadOnlyList<double> TrainingMedians,
    int TrainValidationPlayerOverlap, int TrainTestPlayerOverlap,
    string GeneralizationLimit, string Status);

public sealed record AllocationBaselineRow(string MatchId, double RankMeanGap, double[] Features, IReadOnlyList<string> Warnings);
