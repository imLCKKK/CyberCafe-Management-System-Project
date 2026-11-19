using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace CyberManagement.Application.DTOs.Tournaments;

public sealed record OpggProvenance(string Origin, string SnapshotId, string CapturedAtUtc, string SnapshotSha256);
public sealed record OpggSnapshot(JsonElement Stats, OpggProvenance Provenance)
{
    // Hash the exact RFC 8785 UTF-8 bytes created by the Node collector.
    public static string Fingerprint(string canonical) =>
        "sha256:" + Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));

    public static OpggSnapshot? ForRegistration(string? canonical, string? riotId)
    {
        var snapshot = FromCanonical(canonical);
        return snapshot != null && string.Equals(snapshot.Stats.GetProperty("riotId").GetString(),
            riotId, StringComparison.OrdinalIgnoreCase) ? snapshot : null;
    }

    public static OpggSnapshot? FromCanonical(string? canonical)
    {
        if (canonical is null) return null;
        using var document = JsonDocument.Parse(canonical);
        var proof = document.RootElement.GetProperty("provenance");
        var stats = document.RootElement.GetProperty("stats");
        var origin = proof.GetProperty("origin").GetString();
        var id = proof.GetProperty("snapshotId").GetString();
        var captured = proof.GetProperty("capturedAtUtc").GetString();
        if (origin != "provider" || !Guid.TryParseExact(id, "D", out _)
            || !DateTimeOffset.TryParseExact(captured, "yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal, out _)
            || stats.GetProperty("source").GetString() != "opgg" || stats.GetProperty("queue").GetString() != "competitive")
            throw new JsonException("Nguồn snapshot OP.GG không hợp lệ.");
        return new(stats.Clone(), new(origin, id!, captured!, Fingerprint(canonical)));
    }
}
