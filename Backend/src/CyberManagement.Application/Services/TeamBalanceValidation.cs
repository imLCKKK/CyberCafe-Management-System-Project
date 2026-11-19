using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using CyberManagement.Application.DTOs.Tournaments;
using CyberManagement.Application.Exceptions;

namespace CyberManagement.Application.Services;

public static class TeamBalanceValidation
{
    public static readonly IReadOnlyList<string> Roles = Array.AsReadOnly(
        new[] { "Duelist", "Initiator", "Controller", "Sentinel", "Flex" });

    public static string NormalizeRiotId(string? value)
    {
        if (value is null || value.Length > 128 || value.Any(c => c < 32 || c == 127))
            throw Error("ValidationError", "Riot ID không hợp lệ.");
        var parts = value.Split('#').Select(p => p.Trim()).ToArray();
        if (parts.Length != 2 || parts.Any(string.IsNullOrEmpty) || parts[1].Any(char.IsWhiteSpace))
            throw Error("ValidationError", "Nhập Riot ID theo dạng Tên#TAG.");
        return string.Join('#', parts);
    }

    public static string RosterVersion(TeamBalanceInput input) => TeamBalanceJson.Hash(new
    {
        input.TournamentId, input.GameId, input.Status, input.TeamSize,
        feePolicy = new { input.FeePolicy.EntryFee,
            fullRefundBeforeUtc = input.FeePolicy.FullRefundBeforeUtc?.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture),
            input.FeePolicy.RefundPercentAfter },
        participants = input.Participants.OrderBy(p => p.RegistrationId).Select(p => new
        {
            p.RegistrationId, p.CustomerId, p.CustomerStatus, riotId = NormalizeRiotId(p.RiotId),
            p.PreferredRole, p.RegistrationStatus, p.FeeCharged, p.FeeRefunded, p.FeeEligible,
            snapshotSha256 = p.Snapshot.Provenance.SnapshotSha256
        })
    });

    public static IReadOnlyDictionary<int, TeamBalancePlayerStats> Roster(TeamBalanceInput input)
    {
        if (input is null || input.ContractVersion != TeamBalanceVersions.Contract
            || input.FeatureVersion != TeamBalanceVersions.Feature || input.PolicyVersion != TeamBalanceVersions.Policy
            || input.TournamentId <= 0 || input.GameId <= 0 || input.GeneratedAtUtc == default
            || input.GeneratedAtUtc.Offset != TimeSpan.Zero || input.FeePolicy is null
            || input.FeePolicy.EntryFee < 0 || input.FeePolicy.RefundPercentAfter is < 0 or > 100)
            throw Error("ValidationError", "Contract, version hoặc cấu hình giải không hợp lệ.");
        if (input.Strategy is not ("ml" or TeamBalanceVersions.Baseline))
            throw Error("ValidationError", "Phải chọn ml hoặc rank-snake-v1.");
        try
        {
            if (Convert.FromBase64String(input.ExpectedVersion).Length != 8)
                throw new FormatException();
        }
        catch (Exception e) when (e is FormatException or ArgumentNullException)
        { throw Error("ValidationError", "ExpectedVersion phải là rowversion base64 8 byte."); }
        if (input.GameKey != "valorant" || input.TeamSize != 5 || input.Strategy == "ml" && input.TeamCount != 2)
            throw Error("UNSUPPORTED_GAME_OR_SIZE", "V1 chỉ hỗ trợ VALORANT, hai đội 5 người.");
        if (input.HasConfirmedTeams) throw Error("TeamsAlreadyFormed", "Giải đã có đội xác nhận.");
        if (input.Status != "RegistrationClosed") throw Error("TournamentLocked", "Giải phải đóng đăng ký trước khi chia đội.");
        if (input.TeamCount < 2 || input.Participants is null || input.Participants.Count != (long)input.TeamSize * input.TeamCount)
            throw Error("TeamSizeMismatch", "Cần đủ người cho ít nhất hai đội, mỗi đội 5 người.");
        var ids = new HashSet<int>();
        var customers = new HashSet<int>();
        var riotIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var stats = new Dictionary<int, TeamBalancePlayerStats>();
        foreach (var player in input.Participants)
        {
            if (player is null || player.RegistrationId <= 0 || player.CustomerId <= 0
                || !ids.Add(player.RegistrationId) || !customers.Add(player.CustomerId)
                || !riotIds.Add(NormalizeRiotId(player.RiotId)))
                throw Error("ValidationError", "ID hoặc Riot ID bị trùng/không hợp lệ.");
            if (player.CustomerStatus != "Active" || player.RegistrationStatus != "Registered" || !player.FeeEligible
                || player.FeeCharged < 0 || player.FeeRefunded < 0
                || input.FeePolicy.EntryFee > 0 && (player.FeeCharged != input.FeePolicy.EntryFee || player.FeeRefunded > 0))
                throw Error("TeamPlanInvalid", "Đăng ký, tài khoản hoặc phí không đủ điều kiện.");
            if (!Roles.Contains(player.PreferredRole, StringComparer.Ordinal))
                throw Error("TeamPlanInvalid", "Vai trò đăng ký thiếu hoặc không được hỗ trợ.");
            stats.Add(player.RegistrationId, Snapshot(player.Snapshot, player.RiotId, input.GeneratedAtUtc));
        }
        if (stats.Values.Select(s => s.Scope).Distinct(StringComparer.Ordinal).Count() != 1)
            throw Error("STATS_SCOPE_MISMATCH", "Snapshot phải thuộc cùng scope Competitive.");
        if (input.RosterVersion != RosterVersion(input))
            throw Error("TeamPlanInvalid", "Hash roster không khớp dữ liệu đầu vào.");
        return stats;
    }

    public static TeamBalancePlayerStats Snapshot(OpggSnapshot snapshot, string riotId, DateTimeOffset cutoff)
    {
        try
        {
            if (snapshot?.Provenance is not { Origin: "provider" } proof
                || !Guid.TryParseExact(proof.SnapshotId, "D", out _))
                throw Error("STATS_UNAVAILABLE", "Thiếu snapshot có provenance từ provider.");
            var captured = Timestamp(proof.CapturedAtUtc);
            if (proof.CapturedAtUtc != captured.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture))
                throw new FormatException();
            var stats = snapshot.Stats;
            if (stats.ValueKind != JsonValueKind.Object
                || stats.GetProperty("source").GetString() != "opgg"
                || stats.GetProperty("queue").GetString() != "competitive"
                || !string.Equals(NormalizeRiotId(stats.GetProperty("riotId").GetString()), NormalizeRiotId(riotId), StringComparison.OrdinalIgnoreCase))
                throw Error("STATS_UNAVAILABLE", "Snapshot không khớp người chơi/nguồn/queue.");
            var scope = stats.GetProperty("scope").GetString();
            if (string.IsNullOrWhiteSpace(scope) || scope.Length > 160) throw new FormatException();
            var fetched = Timestamp(stats.GetProperty("fetchedAt").GetString());
            var source = stats.GetProperty("sourceUpdatedAt");
            var updated = source.ValueKind == JsonValueKind.Null ? (DateTimeOffset?)null : Timestamp(source.GetString());
            if (fetched > captured || captured > cutoff || cutoff - fetched > TimeSpan.FromHours(24)
                || updated > fetched || updated.HasValue && cutoff - updated.Value > TimeSpan.FromDays(7))
                throw Error("STATS_STALE", "Snapshot ở tương lai, sau thời điểm cắt hoặc đã quá hạn.");
            var rank = stats.GetProperty("rank");
            if (rank.ValueKind != JsonValueKind.Object || !rank.GetProperty("tier").TryGetInt32(out var tier)
                || tier is < 1 or > 27 || !rank.GetProperty("division").TryGetInt32(out var division)
                || division is < 0 or > 3 || rank.GetProperty("name").GetString() is not { } name
                || string.IsNullOrWhiteSpace(name) || name.Length > 50
                || rank.GetProperty("label").GetString() != name + (division == 0 ? "" : " " + division.ToString(CultureInfo.InvariantCulture))
                || !stats.GetProperty("matches").TryGetInt32(out var matches) || matches < 10)
                throw Error("STATS_UNAVAILABLE", "Cần rank tier 1..27 và ít nhất 10 trận.");
            var values = new[] { "winRate", "headshotRate", "damagePerRound", "kda" }
                .Select(key => NullableNumber(stats.GetProperty(key), key.EndsWith("Rate", StringComparison.Ordinal) ? 100 : double.MaxValue)).ToArray();
            var actualHash = TeamBalanceJson.Hash(new { stats, provenance = new { proof.Origin, proof.SnapshotId, proof.CapturedAtUtc } });
            if (proof.SnapshotSha256 != actualHash)
                throw Error("STATS_UNAVAILABLE", "Hash snapshot không khớp nội dung.");
            return new(tier, matches, scope, values[0], values[1], values[2], values[3],
                updated.HasValue ? Array.Empty<string>() : new[] { "SOURCE_FRESHNESS_UNKNOWN" });
        }
        catch (Exception e) when (e is JsonException or InvalidOperationException or KeyNotFoundException or FormatException or OverflowException or ArgumentException)
        { throw Error("STATS_UNAVAILABLE", "Schema snapshot OP.GG không hợp lệ."); }
    }

    private static DateTimeOffset Timestamp(string? text)
    {
        if (text is null || !Regex.IsMatch(text, @"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(\.\d{1,7})?(Z|[+-]\d{2}:\d{2})$", RegexOptions.CultureInvariant)
            || !DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out var value))
            throw new FormatException();
        return value;
    }

    private static double? NullableNumber(JsonElement element, double maximum)
    {
        if (element.ValueKind == JsonValueKind.Null) return null;
        var value = element.GetDouble();
        if (!double.IsFinite(value) || value < 0 || value > maximum) throw new FormatException();
        return value;
    }

    private static TeamBalanceException Error(string code, string message) => new(code, message);
}
