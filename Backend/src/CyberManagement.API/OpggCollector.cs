using System.Net.Http.Headers;
using System.Text.Json;
using CyberManagement.Application.DTOs.Tournaments;
using CyberManagement.Application.Exceptions;
using CyberManagement.Application.Interfaces.Services;

namespace CyberManagement.API;

public sealed class OpggCollector(HttpClient client) : IOpggCollector
{
    public async Task<string> CaptureAsync(string riotId, CancellationToken token)
    {
        const string captureType = "application/vnd.opgg.capture+json";
        using var request = new HttpRequestMessage(HttpMethod.Get,
            "api/valorant/opgg/stats?riotId=" + Uri.EscapeDataString(riotId));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue(captureType));
        try
        {
            using var response = await client.SendAsync(request, token);
            var content = await response.Content.ReadAsStringAsync(token);
            if (!response.IsSuccessStatusCode)
            {
                var status = (int)response.StatusCode;
                if (status is not (400 or 403 or 404 or 409 or 429 or 502)) throw new JsonException();
                using var error = JsonDocument.Parse(content);
                var message = error.RootElement.GetProperty("message").GetString();
                if (string.IsNullOrWhiteSpace(message) || message.Length > 300) throw new JsonException();
                throw new OpggProviderException(status, message);
            }
            if (response.Content.Headers.ContentType?.MediaType != captureType) throw new JsonException();
            var snapshot = OpggSnapshot.FromCanonical(content)!;
            if (!string.Equals(riotId, snapshot.Stats.GetProperty("riotId").GetString(), StringComparison.OrdinalIgnoreCase)
                || !ValidStats(snapshot.Stats)) throw new JsonException();
            return content;
        }
        catch (OperationCanceledException) when (!token.IsCancellationRequested)
        {
            throw new OpggProviderException(502, "OP.GG phản hồi quá thời gian chờ.");
        }
        catch (Exception error) when (error is HttpRequestException or JsonException or KeyNotFoundException or InvalidOperationException or FormatException)
        {
            throw new OpggProviderException(502, "Không đọc được thống kê từ adapter OP.GG. Vui lòng thử lại.");
        }
    }

    private static bool ValidStats(JsonElement stats)
    {
        var scope = stats.GetProperty("scope").GetString();
        if (string.IsNullOrWhiteSpace(scope) || scope.Length > 160
            || !stats.GetProperty("fetchedAt").TryGetDateTimeOffset(out _)) return false;
        var updated = stats.GetProperty("sourceUpdatedAt");
        if (updated.ValueKind != JsonValueKind.Null && !updated.TryGetDateTimeOffset(out _)) return false;
        var rank = stats.GetProperty("rank");
        if (rank.ValueKind != JsonValueKind.Null)
        {
            var name = rank.GetProperty("name").GetString();
            if (string.IsNullOrWhiteSpace(name) || name.Length > 50
                || !rank.GetProperty("tier").TryGetInt32(out var tier) || tier is < 0 or > 27
                || !rank.GetProperty("division").TryGetInt32(out var division) || division is < 0 or > 3
                || rank.GetProperty("label").GetString() != (division == 0 ? name : $"{name} {division}")) return false;
        }
        var matches = stats.GetProperty("matches");
        long count = 0;
        if (matches.ValueKind != JsonValueKind.Null && (!matches.TryGetInt64(out count) || count < 0)) return false;
        foreach (var key in new[] { "winRate", "headshotRate", "damagePerRound", "kda" })
        {
            var metric = stats.GetProperty(key);
            if (metric.ValueKind == JsonValueKind.Null) continue;
            if (count == 0 || !metric.TryGetDouble(out var number) || !double.IsFinite(number) || number < 0
                || (key is "winRate" or "headshotRate" && number > 100)) return false;
        }
        return true;
    }
}
