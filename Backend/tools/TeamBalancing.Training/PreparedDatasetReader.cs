using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.RegularExpressions;
using CyberManagement.Application.DTOs.Tournaments;
using CyberManagement.Application.Exceptions;
using CyberManagement.Application.Services;
using CyberManagement.Infrastructure.TeamBalancing;

namespace CyberManagement.TeamBalancing.Training;

public sealed record VerifiedPreparedDataset(string Directory, PreparationResult Manifest, byte[] SplitManifestBytes,
    IReadOnlyDictionary<string, PreparedMatch[]> Rows, JsonElement Baseline, AllocationRoster[] OptimizerRosters);

public static class PreparedDatasetReader
{
    public static VerifiedPreparedDataset Read(string directory)
    {
        var hashes = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllBytes(Path.Combine(directory, "artifact-hashes.json")), DatasetPreparation.JsonOptions)
            ?? throw new TeamBalanceException("DATASET_INVALID", "Thiếu bảng hash artifact.");
        var required = new[] { "train.jsonl", "validation.jsonl", "test.jsonl", "split-manifest.json", "split-manifest.sha256", "baseline.json" };
        Require(hashes.Keys.Order(StringComparer.Ordinal).SequenceEqual(required.Order(StringComparer.Ordinal)), "Bảng hash phải có đúng sáu file chuẩn bị dữ liệu.");
        var files = required.ToDictionary(name => name, name => File.ReadAllBytes(Path.Combine(directory, name)), StringComparer.Ordinal);
        foreach (var name in required) Require(TeamBalanceModelArtifact.Hash(files[name]) == hashes[name], "Hash artifact dữ liệu không khớp.");
        Require(System.Text.Encoding.UTF8.GetString(files["split-manifest.sha256"]).Trim() == hashes["split-manifest.json"], "Hash split manifest không khớp.");
        var manifest = JsonSerializer.Deserialize<PreparationResult>(files["split-manifest.json"], DatasetPreparation.JsonOptions);
        Require(manifest is not null && manifest.Source is not null && manifest.Source.FeatureVersion == TeamBalanceVersions.Feature
            && manifest.Source.DataKind is "observed-matches" or "generated-fixture", "Dataset phải được phân loại observed-matches hoặc generated-fixture; không nhận nguồn unverified.");
        Require(manifest.TrainingMedians is { Count: 4 } && manifest.TrainingMedians.All(x => double.IsFinite(x) && x >= 0)
            && manifest.TrainingMedians[0] <= 100 && manifest.TrainingMedians[1] <= 100
            && manifest.SupportedScopes is { Count: > 0 } && manifest.SupportedPatches is { Count: > 0 }, "Median/scope/patch của dataset không hợp lệ; chạy lại prepare.");
        Require(manifest.Folds is { Count: 3 } && manifest.Folds.Select(f => f.Name).Order(StringComparer.Ordinal)
            .SequenceEqual(new[] { "test", "train", "validation" }), "Cần đúng ba fold train/validation/test.");
        var rows = new Dictionary<string, PreparedMatch[]>(StringComparer.Ordinal);
        var seenMatches = new HashSet<string>(StringComparer.Ordinal);
        var seenGroups = new HashSet<string>(StringComparer.Ordinal);
        var seenAliases = new HashSet<string>(StringComparer.Ordinal);
        var players = new HashSet<string>(StringComparer.Ordinal);
        foreach (var fold in manifest.Folds)
        {
            Require(fold.Matches is not null && fold.Count == fold.Matches.Count && fold.Count >= (fold.Name == "train" ? 600 : 150), "Fold không đạt số trận gốc tối thiểu.");
            var metadata = new Dictionary<string, SplitMatch>(StringComparer.Ordinal);
            foreach (var match in fold.Matches)
            {
                Require(match is not null && !string.IsNullOrWhiteSpace(match.MatchId) && !string.IsNullOrWhiteSpace(match.GroupId)
                    && seenMatches.Add(match.MatchId) && match.PlayerKeys is { Length: 10 } && match.PlayerKeys.All(k => !string.IsNullOrWhiteSpace(k))
                    && match.PlayerKeys.Distinct(StringComparer.Ordinal).Count() == 10 && match.Aliases is not null
                    && match.ResultVerifiedAtUtc > match.StartedAtUtc && match.ResultVerifiedAtUtc.Offset == TimeSpan.Zero
                    && match.SnapshotHashes is { Length: 10 } && match.SnapshotHashes.All(IsHash), "Match/roster/hash thiếu hoặc trùng qua fold.");
                foreach (var alias in match.Aliases.Append(match.MatchId).Distinct(StringComparer.Ordinal))
                    Require(!string.IsNullOrWhiteSpace(alias) && seenAliases.Add(alias), "Alias trận trùng qua fold.");
                metadata.Add(match.MatchId, match);
                players.UnionWith(match.PlayerKeys);
            }
            foreach (var group in fold.Matches.Select(m => m.GroupId).Distinct(StringComparer.Ordinal))
                Require(seenGroups.Add(group), "Competition group trùng qua fold.");
            var parsed = new List<PreparedMatch>();
            using var reader = new StreamReader(new MemoryStream(files[fold.Name + ".jsonl"]), new System.Text.UTF8Encoding(false, true));
            while (reader.ReadLine() is { } line)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                var row = JsonSerializer.Deserialize<PreparedMatch>(line, DatasetPreparation.JsonOptions);
                Require(row is not null && metadata.TryGetValue(row.MatchId, out var match)
                    && row.GroupId == match.GroupId && row.StartedAtUtc == match.StartedAtUtc && row.StartedAtUtc.Offset == TimeSpan.Zero
                    && row.Features is { Length: 10 } && row.Features.All(x => double.IsFinite(x) && Math.Abs(x) <= float.MaxValue), "Feature row không khớp split manifest.");
                parsed.Add(row);
            }
            Require(parsed.Count == fold.Count * (fold.Name == "train" ? 2 : 1), "Số dòng augment/evaluation không hợp lệ.");
            var originals = new List<PreparedMatch>();
            foreach (var group in parsed.GroupBy(r => r.MatchId, StringComparer.Ordinal))
            {
                var pair = group.ToArray();
                Require(pair.Length == (fold.Name == "train" ? 2 : 1), "Một trận có số bản augment không hợp lệ.");
                if (pair.Length == 2) Require(pair[0].Label != pair[1].Label && pair[0].Features.Zip(pair[1].Features).All(p => p.First == -p.Second), "Augment phải đảo đúng vector và nhãn.");
                originals.Add(pair[0]);
            }
            Require(originals.Count == fold.Count && originals.Count(r => r.Label) == fold.PositiveLabels
                && fold.PositiveLabels >= 30 && fold.Count - fold.PositiveLabels >= 30, "Số nhãn/fold không khớp hoặc không đạt gate.");
            rows.Add(fold.Name, parsed.ToArray());
        }
        Require(seenMatches.Count >= 1000 && manifest.OriginalMatches == seenMatches.Count && players.Count >= 200
            && manifest.UniquePlayers == players.Count, "Data gate yêu cầu >=1.000 trận, >=200 người sau quarantine.");
        Require(rows["train"].Max(r => r.StartedAtUtc) < rows["validation"].Min(r => r.StartedAtUtc)
            && rows["validation"].Max(r => r.StartedAtUtc) < rows["test"].Min(r => r.StartedAtUtc)
            && rows["train"].All(r => r.StartedAtUtc < manifest.TrainBeforeUtc)
            && rows["validation"].All(r => r.StartedAtUtc >= manifest.TrainBeforeUtc && r.StartedAtUtc < manifest.TestFromUtc)
            && rows["test"].All(r => r.StartedAtUtc >= manifest.TestFromUtc), "Rò rỉ ranh giới thời gian train/evaluation.");
        Require(manifest.Folds.Single(f => f.Name == "train").Matches.Max(m => m.ResultVerifiedAtUtc) < manifest.TrainBeforeUtc
            && manifest.Folds.Single(f => f.Name == "validation").Matches.Max(m => m.ResultVerifiedAtUtc) < manifest.TestFromUtc,
            "Nhãn train/validation chưa có sẵn trước thời điểm bắt đầu evaluation.");
        using var baseline = JsonDocument.Parse(files["baseline.json"]);
        Require(baseline.RootElement.TryGetProperty("optimizerRosters", out var rosterJson)
            && rosterJson.ValueKind == JsonValueKind.Array, "Thiếu roster đánh giá optimizer; chạy lại prepare với tập đánh giá chưa sử dụng.");
        var rosters = rosterJson.Deserialize<AllocationRoster[]>(DatasetPreparation.JsonOptions);
        Require(rosters is not null && rosters.Length == rows["test"].Length
            && rosters.All(r => r is not null && r.Warnings is not null && r.Players is { Length: 10 }
                && r.Players.All(p => p is not null && TeamBalanceValidation.Roles.Contains(p.PreferredRole)
                    && p.Features is { Length: 10 } && p.Features.All(x => double.IsFinite(x))
                    && p.Features[0] is >= 1 and <= 27 && p.Features[0] == Math.Truncate(p.Features[0])))
            && rosters.Select(r => r.MatchId).ToHashSet(StringComparer.Ordinal).SetEquals(rows["test"].Select(r => r.MatchId)),
            "Roster đánh giá optimizer không khớp tập test hoặc có vai trò/feature không hợp lệ.");
        return new(Path.GetFullPath(directory), manifest, files["split-manifest.json"], rows, baseline.RootElement.Clone(), rosters);
    }

    private static bool IsHash(string? text) => text is not null && Regex.IsMatch(text, "^sha256:[0-9a-f]{64}$");
    private static void Require([DoesNotReturnIf(false)] bool condition, string message)
    {
        if (!condition) throw new TeamBalanceException("DATASET_INVALID", message);
    }
}
