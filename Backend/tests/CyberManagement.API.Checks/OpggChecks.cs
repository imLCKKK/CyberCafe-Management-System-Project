using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using CyberManagement.Domain.Entities;
using CyberManagement.Infrastructure.Persistance;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

internal sealed class OpggCheckTransport : DelegatingHandler
{
    // A null status is the only mode that contacts the real local adapter.
    public int? FixtureStatus { get; set; } = 502;
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
    {
        if (FixtureStatus is not int status) return base.SendAsync(request, token);
        var body = status == 200
            ? """{"provenance":{"origin":"fixture","snapshotId":"00000000-0000-0000-0000-000000000001","capturedAtUtc":"2026-11-10T00:00:00.000Z"},"stats":{"source":"opgg","queue":"competitive"}}"""
            : """{"message":"OP.GG fixture error"}""";
        return Task.FromResult(new HttpResponseMessage((HttpStatusCode)status) {
            Content = new StringContent(body, Encoding.UTF8, status == 200 ? "application/vnd.opgg.capture+json" : "application/json")
        });
    }
}

internal static class OpggChecks
{
    internal static async Task RunAsync(IServiceProvider services, HttpClient client, Dictionary<string, string> staff,
        string customer, OpggCheckTransport transport, bool live)
    {
        var vectorPath = Path.Combine(AppContext.BaseDirectory, "fixtures", "snapshot-hash.json");
        using var vector = JsonDocument.Parse(await File.ReadAllTextAsync(vectorPath));
        var canonical = vector.RootElement.GetProperty("canonical").GetRawText();
        if (CyberManagement.Application.DTOs.Tournaments.OpggSnapshot.Fingerprint(canonical)
            != vector.RootElement.GetProperty("snapshotSha256").GetString()) throw new Exception("Node/.NET hash mismatch");
        var riotId = Environment.GetEnvironmentVariable("OPGG_RIOT_ID") ?? "Blue#00h";
        int tournamentId, registrationId, otherId;
        byte[] version;
        await using (var scope = services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var game = new Game { GameName = "VALORANT", TeamSize = 5 };
            var rank = new GameRank { Game = game, RankName = "Declared rank", RankOrder = 1 };
            var tournament = new Tournament { Game = game, TournamentName = "OP.GG integration check",
                CreatedBy = await db.Employees.Where(e => e.Username == "admin").Select(e => e.EmployeeId).SingleAsync(),
                Status = "RegistrationOpen", RegistrationStart = DateTime.UtcNow.AddDays(-1),
                RegistrationEnd = DateTime.UtcNow.AddDays(1), StartTime = DateTime.UtcNow.AddDays(2) };
            async Task<TournamentRegistration> Row(string phone) => new() {
                Tournament = tournament, Rank = rank, CustomerId = await db.Customers.Where(c => c.Phone == phone)
                    .Select(c => c.CustomerId).SingleAsync(), RiotId = riotId, PreferredRole = "Flex", FeeCharged = 0
            };
            var mine = await Row("0900000001");
            var other = await Row("0900000002");
            db.AddRange(mine, other);
            await db.SaveChangesAsync();
            tournamentId = tournament.TournamentId; version = tournament.Version; registrationId = mine.RegistrationId; otherId = other.RegistrationId;
        }

        string List(string audience) => $"/api/{audience}/tournaments/{tournamentId}/registrations";
        string Refresh(string audience, int id) => $"{List(audience)}/{id}/opgg/refresh";
        async Task<JsonElement> Call(string path, string token, int expected, object? command = null)
        {
            using var request = new HttpRequestMessage(command == null ? HttpMethod.Get : HttpMethod.Post, path);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            if (command != null) request.Content = JsonContent.Create(command);
            using var result = await client.SendAsync(request);
            var content = await result.Content.ReadAsStringAsync();
            if ((int)result.StatusCode != expected) throw new Exception($"{path}: expected {expected}, got {(int)result.StatusCode}: {content}");
            if (expected == 429 && result.Headers.RetryAfter == null) throw new Exception("Missing retry interval");
            return content.Length == 0 ? default : JsonSerializer.Deserialize<JsonElement>(content);
        }
        var own = Refresh("client", registrationId);
        var admin = Refresh("admin", registrationId);
        var empty = new { };
        await Call(own, customer, 400, new { stats = new { rank = 27 } });
        await Call(Refresh("client", otherId), customer, 404, empty);
        await Call(admin, staff["Waiter"], 403, empty);
        foreach (var status in new[] { 400, 403, 404, 409, 429, 502 })
        {
            transport.FixtureStatus = status;
            var result = await Call(own, customer, status, empty);
            if (result.GetProperty("message").GetString() != "OP.GG fixture error") throw new Exception("Lost provider error");
        }
        transport.FixtureStatus = 200;
        await Call(own, customer, 502, empty);
        var initial = await Call(List("client"), customer, 200);
        if (initial.EnumerateArray().Any(r => r.GetProperty("opggSnapshot").ValueKind != JsonValueKind.Null))
            throw new Exception("Fixture/error produced a persisted snapshot.");
        Console.WriteLine("PASS fixtures: provider status/message, Retry-After, permissions, injection and fixture provenance rejected.");
        if (!live) return;

        transport.FixtureStatus = null;
        var concurrent = await Task.WhenAll(Call(own, customer, 200, empty), Call(admin, staff["Admin"], 200, empty));
        var saved = concurrent[0];
        var proof = saved.GetProperty("provenance");
        var hash = proof.GetProperty("snapshotSha256").GetString();
        if (proof.GetProperty("origin").GetString() != "provider" || hash?.Length != 71
            || concurrent[1].GetProperty("provenance").GetProperty("snapshotSha256").GetString() != hash)
            throw new Exception("Concurrent refresh did not converge on one provider capture.");
        transport.FixtureStatus = 502;
        await Call(own, customer, 502, empty);
        foreach (var (audience, token) in new[] { ("admin", staff["Admin"]), ("client", customer) })
        {
            var rows = await Call(List(audience), token, 200);
            var row = rows.EnumerateArray().Single(r => r.GetProperty("id").GetInt32() == registrationId);
            if (!JsonElement.DeepEquals(saved, row.GetProperty("opggSnapshot"))) throw new Exception("Lost persisted capture");
        }
        await using var verify = services.CreateAsyncScope();
        var dbVerify = verify.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var record = await dbVerify.Set<RegistrationStatsSnapshot>().SingleAsync();
        var tournamentVersion = await dbVerify.Tournaments.Where(t => t.TournamentId == tournamentId).Select(t => t.Version).SingleAsync();
        if (tournamentVersion.SequenceEqual(version) || record.SnapshotId != Guid.Parse(proof.GetProperty("snapshotId").GetString()!))
            throw new Exception("Tournament version or snapshot ID is inconsistent.");
        Console.WriteLine($"PASS LIVE SQL: {saved.GetProperty("stats").GetRawText()}");
        Console.WriteLine("PASS live replay and SQL reread, tournament version advanced, failed refresh preserved the capture.");
    }
}
