using System.Globalization;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using CyberManagement.API;
using CyberManagement.Application.Interfaces.Services;
using CyberManagement.Application.DTOs.Tournaments;
using CyberManagement.Application.Services;
using CyberManagement.Domain.Entities;
using CyberManagement.Infrastructure.Persistance;
using CyberManagement.Infrastructure.TeamBalancing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

// Test host only. Exercises the real ML.NET fit/predict path without enabling fixture artifacts in production.
internal sealed class PreviewFixtureScorer : ITeamBalanceScorer
{
    private readonly TeamBalanceMlModel model = TeamBalanceMlModel.Fit(Enumerable.Range(0, 100).Select(i =>
    {
        var x = new double[10]; x[0] = i - 50;
        return (i >= 50, x);
    }), .1f, false);
    public string ModelVersion => "preview-test-only";
    public string ModelSha256 => "test-only";
    public string DatasetVersion => "generated-fixture";
    public IReadOnlyList<double> TrainingMedians => new double[] { 50, 20, 120, 1 };
    public bool Supports(string scope) => scope == "Fixture scope";
    public int LastBatchSize { get; private set; }
    public double[] Predict(IReadOnlyList<double[]> differences)
    {
        LastBatchSize = differences.Count;
        return model.Predict(differences);
    }
}

internal static class TeamPreviewChecks
{
    public static async Task RunAsync(IServiceProvider services, HttpClient client, Dictionary<string,string> staff,
        string customerToken, bool hasModel, string provider)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var admin = await db.Employees.SingleAsync(e => e.Username == "admin");
        var t = new Tournament { Game = new Game { GameName = "VALORANT", TeamSize = 5, IsActive = true }, CreatedBy = admin.EmployeeId,
            TournamentName = "Preview integration", Status = "RegistrationClosed", RegistrationStart = DateTime.UtcNow.AddDays(-1),
            RegistrationEnd = DateTime.UtcNow, StartTime = DateTime.UtcNow.AddHours(1) };
        var rank = new GameRank { Game = t.Game, RankName = "Declared", RankOrder = 1 };
        var rows = new List<TournamentRegistration>();
        for (var i = 0; i < 10; i++)
        {
            var riot = $"Preview{i}#VN";
            var r = new TournamentRegistration { Tournament = t, Rank = rank, PreferredRole = "Flex", RiotId = riot, FeeCharged = 0,
                Customer = new Customer { FullName = $"Preview {i}", Phone = $"098780{i:0000}" } };
            var snapshotId = Guid.NewGuid();
            var time = DateTime.UtcNow.AddMinutes(-1);
            string Stamp(DateTime value) => value.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture);
            var canonical = TeamBalanceJson.Canonical(new {
                provenance = new { origin = "provider", snapshotId = snapshotId.ToString(), capturedAtUtc = Stamp(time) },
                stats = new { riotId = riot, source = "opgg", queue = "competitive", scope = "Fixture scope", fetchedAt = Stamp(time),
                    sourceUpdatedAt = (string?)null, matches = 20, winRate = 50, headshotRate = 20, damagePerRound = 120, kda = 1,
                    rank = new { tier = 15 + i, division = 2, name = "Gold", label = "Gold 2" } } });
            r.StatsSnapshots.Add(new RegistrationStatsSnapshot { SnapshotId = snapshotId, RiotId = riot, FetchedAt = time, CanonicalJson = canonical });
            db.Add(r); rows.Add(r);
        }
        await db.SaveChangesAsync();
        var version = Convert.ToBase64String(t.Version);
        string Route() => $"/api/admin/tournaments/{t.TournamentId}/teams/preview";
        var checks = 0;
        void Check(bool result, string name)
        {
            if (!result) throw new Exception("Preview failed: " + name);
            checks++;
        }
        async Task<JsonElement> Post(object body, int status, string? code = null, string? bearer = null)
        {
            using var req = new HttpRequestMessage(HttpMethod.Post, Route()) { Content = JsonContent.Create(body) };
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearer ?? staff["Admin"]);
            using var response = await client.SendAsync(req);
            var content = await response.Content.ReadAsStringAsync();
            Check((int)response.StatusCode == status, $"{response.StatusCode}, expected {status}, {content}");
            if (content.Length == 0) return default;
            var json = JsonSerializer.Deserialize<JsonElement>(content);
            if (code != null) Check(json.GetProperty("code").GetString() == code, code);
            return json;
        }
        var balanced = new { strategy = "Balanced", expectedVersion = version };
        await Post(balanced, 403, bearer: customerToken);
        await Post(balanced, 403, bearer: staff["Waiter"]);
        await Post(new { strategy = "ml", expectedVersion = version }, 400, "ValidationError");
        await Post(new { strategy = "ML", expectedVersion = version, participants = new[] { 1 } }, 400, "ValidationError");
        await Post(new { strategy = "Balanced", expectedVersion = Convert.ToBase64String(Enumerable.Repeat((byte)255, 8).ToArray()) }, 409, "VersionConflict");
        var first = await Post(balanced, 200);
        var repeat = await Post(balanced, 200, bearer: staff["Manager"]);
        var plans = scope.ServiceProvider.GetRequiredService<TeamPreviewPlans>();
        var token = first.GetProperty("planToken").GetGuid();
        Check(plans.Find(token, admin.EmployeeId, t.TournamentId)?.Proposal.ProposalId == first.GetProperty("proposalId").GetString(), "server binding");
        Check(plans.Find(token, admin.EmployeeId + 1, t.TournamentId) == null, "binding owner");
        Check(repeat.GetProperty("planToken").GetGuid() != token && repeat.GetProperty("proposalId").GetString() == first.GetProperty("proposalId").GetString(), "stable proposal and separate tokens");
        var strategy = new { strategy = "ML", expectedVersion = version };
        var output = hasModel ? await Post(strategy, 200) : first;
        if (hasModel)
        {
            Check(output.GetProperty("modelVersion").GetString() == "preview-test-only", "model metadata");
            Check(((PreviewFixtureScorer)scope.ServiceProvider.GetRequiredService<ITeamBalanceScorer>()).LastBatchSize == 126, "all 126 partitions in one batch");
            Check(output.GetProperty("quality").GetProperty("predictedImbalance").GetDouble() < .1, "balanced ML output");
            var input = plans.Find(output.GetProperty("planToken").GetGuid(), admin.EmployeeId, t.TournamentId)!.Input;
            var solver = scope.ServiceProvider.GetRequiredService<ITeamBalancingService>();
            Check(solver.Propose(input with { Participants = input.Participants.Reverse().ToArray() }).ProposalId ==
                output.GetProperty("proposalId").GetString(), "ML reorder stable");
            var times = new double[100];
            for (var i = 0; i < times.Length; i++)
            {
                var timer = System.Diagnostics.Stopwatch.StartNew();
                solver.Propose(input);
                times[i] = timer.Elapsed.TotalMilliseconds;
            }
            var p95 = times.Order().ElementAt(94);
            Console.WriteLine($"Fixture warm inference p95 {p95:F2} ms, 100 runs, {Environment.Version}, {Environment.GetEnvironmentVariable("PROCESSOR_IDENTIFIER")}, {Environment.OSVersion}, {GC.GetGCMemoryInfo().TotalAvailableMemoryBytes} available bytes, {input.Participants.Count} players.");
            Console.WriteLine("Inference samples (ms): " + JsonSerializer.Serialize(times));
            Check(p95 <= 2000, "warm model benchmark");
        }
        else await Post(strategy, 503, "MODEL_UNAVAILABLE");
        var teams = output.GetProperty("teams").EnumerateArray().ToArray();
        var ids = teams.SelectMany(team => team.GetProperty("members").EnumerateArray()).Select(m => m.GetProperty("registrationId").GetInt32()).ToArray();
        Check(teams.Length == 2 && teams.All(team => team.GetProperty("members").GetArrayLength() == 5)
            && ids.Order().SequenceEqual(rows.Select(r => r.RegistrationId).Order()), "exact SQL roster");
        Check(!await db.Teams.AnyAsync(team => team.TournamentId == t.TournamentId)
            && (await db.Tournaments.AsNoTracking().SingleAsync(x => x.TournamentId == t.TournamentId)).Version.SequenceEqual(t.Version), "no persisted changes");
        scope.ServiceProvider.GetRequiredService<PreviewReadInterleaving>().BeforeSecondRead = async () =>
        {
            rows[0].PreferredRole = "Sentinel";
            await db.SaveChangesAsync();
        };
        await Post(balanced, 409, "TeamPlanInvalid");
        rows[0].Customer.Status = "Locked"; await db.SaveChangesAsync();
        await Post(balanced, 409, "TeamPlanInvalid");
        rows[0].Customer.Status = "Active"; rows[0].Status = "Cancelled"; await db.SaveChangesAsync();
        await Post(balanced, 409, "TeamSizeMismatch");
        rows[0].Status = "Registered"; rows[0].FeeCharged = 5000; await db.SaveChangesAsync();
        await Post(balanced, 409, "TeamPlanInvalid");
        rows[0].FeeCharged = 0;
        rows[0].Status = "Registered"; rows[0].RiotId = "Changed#VN"; await db.SaveChangesAsync();
        await Post(balanced, 422, "STATS_UNAVAILABLE");
        db.Teams.Add(new Team { TournamentId = t.TournamentId, TeamName = "Existing team" });
        await db.SaveChangesAsync();
        await Post(balanced, 409, "TeamsAlreadyFormed");
        Console.WriteLine($"{checks} preview checks passed ({provider}, generated roster, ML fixture enabled: {hasModel}).");
    }
}

internal sealed class PreviewReadInterleaving
{
    public Func<Task>? BeforeSecondRead { get; set; }
}

internal sealed class PreviewInterleavedReader(TeamPreviewRosterReader reader, PreviewReadInterleaving change) : ITeamPreviewRosterReader
{
    private int reads;
    public async Task<TeamPreviewRoster> ReadAsync(int id, string strategy, string version, CancellationToken token)
    {
        if (++reads == 2 && change.BeforeSecondRead is { } mutate)
        {
            change.BeforeSecondRead = null;
            await mutate();
        }
        return await reader.ReadAsync(id, strategy, version, token);
    }
}
