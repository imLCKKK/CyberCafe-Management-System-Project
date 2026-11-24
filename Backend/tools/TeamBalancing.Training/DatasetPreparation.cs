using System.Globalization;
using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CyberManagement.Application.DTOs.Tournaments;
using CyberManagement.Application.Exceptions;
using CyberManagement.Application.Services;

namespace CyberManagement.TeamBalancing.Training;

public sealed record PreparedDataset(PreparationResult Manifest, IReadOnlyDictionary<string, PreparedMatch[]> Rows,
    object EvaluationBaseline);

public static class DatasetPreparation
{
    public static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        UnmappedMemberHandling = System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow,
        PropertyNameCaseInsensitive = false, WriteIndented = true
    };

    public static PreparedDataset Prepare(DatasetSource source, IReadOnlyList<MatchRecord> records,
        string sourceManifestHash, string codeCommit)
    {
        Require(source is not null && !string.IsNullOrWhiteSpace(source.DatasetVersion)
            && source.FeatureVersion == TeamBalanceVersions.Feature && PublicUrl(source.SourceUrl)
            && source.DataKind is "unverified" or "observed-matches" or "generated-fixture"
            && !string.IsNullOrWhiteSpace(source.UsageTerms)
            && source.CollectorCommit is not null && System.Text.RegularExpressions.Regex.IsMatch(source.CollectorCommit, "^[a-f0-9]{40}$")
            && source.CollectedAtUtc.Offset == TimeSpan.Zero && source.CollectedAtUtc != default
            && source.CollectedAtUtc <= DateTimeOffset.UtcNow, "Manifest nguồn thiếu version, quyền sử dụng, commit hoặc thời gian hợp lệ.");
        var unique = new Dictionary<string, MatchRecord>(StringComparer.Ordinal);
        var aliases = new Dictionary<string, string>(StringComparer.Ordinal);
        var duplicates = 0;
        var excluded = new List<string>();
        foreach (var record in records)
        {
            Require(record is not null && Key(record.MatchId) && record.Aliases is not null
                && record.Aliases.All(Key) && record.Aliases.Distinct(StringComparer.Ordinal).Count() == record.Aliases.Count,
                "Thiếu matchId hoặc alias không hợp lệ.");
            if (unique.TryGetValue(record.MatchId, out var previous))
            {
                Require(TeamBalanceJson.Hash(previous) == TeamBalanceJson.Hash(record), "Cùng matchId có nội dung/kết quả khác nhau.");
                duplicates++;
                continue;
            }
            foreach (var key in record.Aliases.Append(record.MatchId).Distinct(StringComparer.Ordinal))
            {
                Require(!aliases.ContainsKey(key), "Alias xuất hiện ở hai trận; cần đối chiếu và hợp nhất nguồn trước khi split.");
                aliases.Add(key, record.MatchId);
            }
            unique.Add(record.MatchId, record);
        }

        var valid = new List<MatchRecord>();
        var stats = new Dictionary<string, TeamBalancePlayerStats[]>(StringComparer.Ordinal);
        var snapshotIds = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var record in unique.Values)
        {
            Require(record.ResultOrigin == "provider" && PublicUrl(record.ResultSourceUrl)
                && Key(record.GroupId) && Key(record.Patch) && !string.IsNullOrWhiteSpace(record.Scope)
                && record.StartedAtUtc != default && record.StartedAtUtc.Offset == TimeSpan.Zero
                && record.CompletedAtUtc > record.StartedAtUtc && record.CompletedAtUtc.Offset == TimeSpan.Zero
                && record.ResultVerifiedAtUtc >= record.CompletedAtUtc && record.ResultVerifiedAtUtc.Offset == TimeSpan.Zero
                && record.ResultVerifiedAtUtc <= source.CollectedAtUtc,
                "Trận thiếu nguồn kết quả, group/scope/patch hoặc mốc thời gian hợp lệ.");
            Require(record.Winner is "A" or "B" or "Draw" or "Remake" or "Abandoned", "Nhãn kết quả thiếu hoặc không xác định.");
            if (record.Winner is not ("A" or "B"))
            {
                excluded.Add(record.MatchId + ":" + record.Winner);
                continue;
            }
            Require(record.TeamA is { Count: 5 } && record.TeamB is { Count: 5 }, "Trận phải có hai roster 5 người.");
            var players = record.TeamA.Concat(record.TeamB).ToArray();
            Require(players.All(p => p is not null && Key(p.PlayerKey) && p.Snapshot is not null
                && TeamBalanceValidation.Roles.Contains(p.PreferredRole, StringComparer.Ordinal))
                && players.Select(p => p.PlayerKey).Distinct(StringComparer.Ordinal).Count() == 10,
                "Player key trùng/thiếu, vai trò hoặc snapshot không hợp lệ.");
            var riotIds = players.Select(p => TeamBalanceValidation.NormalizeRiotId(p.Snapshot.Stats.GetProperty("riotId").GetString())).ToArray();
            Require(riotIds.Distinct(StringComparer.OrdinalIgnoreCase).Count() == 10, "Riot ID bị trùng trong trận.");
            var playerStats = players.Select((p, i) => TeamBalanceValidation.Snapshot(p.Snapshot, riotIds[i], record.StartedAtUtc)).ToArray();
            foreach (var player in players)
            {
                var proof = player.Snapshot.Provenance;
                Require(!snapshotIds.TryGetValue(proof.SnapshotId, out var savedHash) || savedHash == proof.SnapshotSha256,
                    "Snapshot ID được dùng lại với nội dung khác; cần giữ snapshot bất biến.");
                snapshotIds[proof.SnapshotId] = proof.SnapshotSha256;
            }
            Require(playerStats.All(s => s.Scope == record.Scope), "Scope snapshot không khớp trận.");
            Require(players.All(p => DateTimeOffset.Parse(p.Snapshot.Provenance.CapturedAtUtc, CultureInfo.InvariantCulture) < record.StartedAtUtc),
                "Snapshot phải được thu trước khi trận bắt đầu.");
            stats.Add(record.MatchId, playerStats);
            valid.Add(record);
        }
        Require(valid.Count >= 1000, "Data gate: cần ít nhất 1.000 trận gốc hợp lệ, không tính augment.");
        var ordered = valid.OrderBy(r => r.StartedAtUtc).ThenBy(r => r.MatchId, StringComparer.Ordinal).ToArray();
        var trainBefore = ordered[(int)(ordered.Length * .70)].StartedAtUtc;
        var testFrom = ordered[(int)(ordered.Length * .85)].StartedAtUtc;
        int Fold(MatchRecord row) => row.StartedAtUtc < trainBefore ? 0 : row.StartedAtUtc < testFrom ? 1 : 2;
        var quarantined = ordered.GroupBy(r => r.GroupId, StringComparer.Ordinal)
            .Where(g => g.Select(Fold).Distinct().Count() != 1
                || g.Any(r => Fold(r) == 0 && r.ResultVerifiedAtUtc >= trainBefore || Fold(r) == 1 && r.ResultVerifiedAtUtc >= testFrom))
            .Select(g => g.Key).Order(StringComparer.Ordinal).ToArray();
        var quarantineKeys = quarantined.ToHashSet(StringComparer.Ordinal);
        var folds = Enumerable.Range(0, 3).Select(i => ordered.Where(r => !quarantineKeys.Contains(r.GroupId) && Fold(r) == i).ToArray()).ToArray();
        Require(folds[0].Length >= 600 && folds[1].Length >= 150 && folds[2].Length >= 150,
            "Data gate: sau quarantine cần train >=600, validation/test mỗi tập >=150 trận.");
        Require(folds.Sum(f => f.Length) >= 1000, "Data gate: cần >=1.000 trận sau loại nhóm qua ranh giới.");
        Require(folds[0].Max(r => r.StartedAtUtc) < folds[1].Min(r => r.StartedAtUtc)
            && folds[1].Max(r => r.StartedAtUtc) < folds[2].Min(r => r.StartedAtUtc), "Fold thời gian bị chồng lấn.");
        var playerSets = folds.Select(f => f.SelectMany(Players).Select(p => p.PlayerKey).ToHashSet(StringComparer.Ordinal)).ToArray();
        var uniquePlayers = playerSets.SelectMany(s => s).Distinct(StringComparer.Ordinal).Count();
        Require(uniquePlayers >= 200, "Data gate: cần ít nhất 200 người chơi.");
        Require(folds.SelectMany(f => f).SelectMany(Players).GroupBy(p => p.PlayerKey, StringComparer.Ordinal)
            .All(g => g.Select(p => TeamBalanceValidation.NormalizeRiotId(p.Snapshot.Stats.GetProperty("riotId").GetString()))
                .Distinct(StringComparer.OrdinalIgnoreCase).Count() == 1), "Player key không ổn định giữa các trận.");
        Require(folds.SelectMany(f => f).SelectMany(Players)
            .GroupBy(p => TeamBalanceValidation.NormalizeRiotId(p.Snapshot.Stats.GetProperty("riotId").GetString()), StringComparer.OrdinalIgnoreCase)
            .All(g => g.Select(p => p.PlayerKey).Distinct(StringComparer.Ordinal).Count() == 1), "Một người bị cấp nhiều player key.");

        var medians = TeamBalanceFeatures.FitMedians(folds[0].SelectMany(r => stats[r.MatchId]));
        var names = new[] { "train", "validation", "test" };
        var prepared = new Dictionary<string, PreparedMatch[]>(StringComparer.Ordinal);
        var manifests = new List<FoldManifest>();
        for (var i = 0; i < folds.Length; i++)
        {
            var rows = folds[i].Select(r => PrepareMatch(r, stats[r.MatchId], medians)).ToArray();
            var positives = rows.Count(r => r.Label);
            Require(positives >= 30 && rows.Length - positives >= 30, "Data gate: mỗi fold cần >=30 mẫu mỗi nhãn sau định hướng A/B.");
            // Augmentation happens after split; validation/test keep exactly one row per original match.
            prepared.Add(names[i], i == 0 ? rows.SelectMany(r => new[] { r, r with { Label = !r.Label, Features = r.Features.Select(x => -x).ToArray() } }).ToArray() : rows);
            manifests.Add(new(names[i], rows.Length, positives, folds[i].Select(r => new SplitMatch(
                r.MatchId, r.Aliases.Order(StringComparer.Ordinal).ToArray(), r.GroupId,
                Players(r).Select(p => p.PlayerKey).Order(StringComparer.Ordinal).ToArray(), r.StartedAtUtc,
                r.ResultVerifiedAtUtc,
                Players(r).Select(p => p.Snapshot.Provenance.SnapshotSha256).ToArray())).ToArray()));
        }
        var manifest = new PreparationResult(source, sourceManifestHash, codeCommit, System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription,
            AssemblyHash(typeof(DatasetPreparation).Assembly.Location), AssemblyHash(typeof(TeamBalanceFeatures).Assembly.Location),
            folds[0].Select(r => r.Scope).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray(),
            folds[0].Select(r => r.Patch).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray(),
            trainBefore, testFrom, folds.Sum(f => f.Length), uniquePlayers, duplicates, excluded.Order(StringComparer.Ordinal).ToArray(), quarantined,
            manifests, medians, playerSets[0].Intersect(playerSets[1]).Count(), playerSets[0].Intersect(playerSets[2]).Count(),
            "Temporal split; cold-player performance and post-proposal match balance are unverified.", "prepared-only; no trained model or model metrics");
        return new(manifest, prepared, Baseline(folds[2], medians));
    }

    private static PreparedMatch PrepareMatch(MatchRecord match, TeamBalancePlayerStats[] stats, double[] medians)
    {
        var features = stats.Select(s => TeamBalanceFeatures.Player(s, medians)).ToArray();
        var difference = TeamBalanceFeatures.Difference(features[..5], features[5..]);
        var reverse = (SHA256.HashData(Encoding.UTF8.GetBytes(match.MatchId))[0] & 1) != 0;
        return new(match.MatchId, match.GroupId, match.StartedAtUtc, reverse ? match.Winner == "B" : match.Winner == "A",
            reverse ? difference.Select(x => -x).ToArray() : difference);
    }

    private static object Baseline(IReadOnlyList<MatchRecord> matches, IReadOnlyList<double> medians)
    {
        var gaps = new List<double>();
        var invalid = new List<string>();
        var details = new List<AllocationBaselineRow>();
        var optimizerRosters = new List<AllocationRoster>(matches.Count);
        var balancing = new TeamBalancingService();
        foreach (var match in matches)
        {
            // Match datasets have no SQL registrations: stable pseudonymous order provides tie-break IDs.
            var participants = Players(match).OrderBy(p => p.PlayerKey, StringComparer.Ordinal).Select((p, i) =>
                new TeamBalanceParticipant(i + 1, i + 1, "Active", "Registered", null, null, true,
                    p.Snapshot.Stats.GetProperty("riotId").GetString()!, p.PreferredRole, p.Snapshot)).ToArray();
            var input = new TeamBalanceInput(TeamBalanceVersions.Contract, TeamBalanceVersions.Feature, TeamBalanceVersions.Policy,
                TeamBalanceVersions.Baseline, 1, 1, "valorant", "RegistrationClosed", 5, 2, false, "AAAAAAAAAAE=", "",
                match.StartedAtUtc, new(0, null, 0), participants);
            input = input with { RosterVersion = TeamBalanceValidation.RosterVersion(input) };
            var stats = TeamBalanceValidation.Roster(input);
            var vectors = stats.ToDictionary(p => p.Key, p => TeamBalanceFeatures.Player(p.Value, medians));
            var warnings = stats.Values.SelectMany(s => s.Warnings).Distinct().Order(StringComparer.Ordinal).ToArray();
            optimizerRosters.Add(new(match.MatchId, participants.Select(p =>
                new AllocationPlayer(p.PreferredRole, vectors[p.RegistrationId])).ToArray(), warnings));
            try
            {
                var proposal = balancing.Propose(input);
                gaps.Add(proposal.Quality.RankMeanGap);
                details.Add(new(match.MatchId, proposal.Quality.RankMeanGap, TeamBalanceFeatures.Difference(
                    proposal.Teams[0].Members.Select(m => vectors[m.RegistrationId]).ToArray(),
                    proposal.Teams[1].Members.Select(m => vectors[m.RegistrationId]).ToArray()), proposal.Warnings));
            }
            catch (TeamBalanceException error) when (error.Code == "ROLE_CONSTRAINT_UNSATISFIABLE") { invalid.Add(match.MatchId); }
        }
        return new
        {
            method = TeamBalanceVersions.Baseline, fold = "test", totalRosters = matches.Count,
            validRosters = gaps.Count, invalidRoleRosters = invalid, validityRate = (double)gaps.Count / matches.Count,
            validRosterDetails = details,
            optimizerRosters,
            meanRankGapOnValidRosters = gaps.Count == 0 ? (double?)null : gaps.Average(),
            predictionBaseline = new { method = "constant-0.5", brier = .25, logLoss = Math.Log(2) },
            warning = "Rank gap is descriptive; no observed outcomes exist for newly proposed teams. Rank-only learned B2 and M1 are step 2."
        };
    }

    private static IEnumerable<MatchPlayer> Players(MatchRecord record) => record.TeamA.Concat(record.TeamB);
    private static string AssemblyHash(string path) => "sha256:" + Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(path)));
    private static bool Key(string? value) => !string.IsNullOrWhiteSpace(value) && value.Length <= 200 && !value.Any(char.IsControl);
    private static bool PublicUrl(string? value) => Uri.TryCreate(value, UriKind.Absolute, out var uri)
        && uri.Scheme == Uri.UriSchemeHttps && string.IsNullOrEmpty(uri.UserInfo);
    private static void Require([DoesNotReturnIf(false)] bool condition, string message)
    {
        if (!condition) throw new TeamBalanceException("DATASET_INVALID", message);
    }
}
