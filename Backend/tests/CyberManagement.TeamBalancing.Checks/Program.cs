using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CyberManagement.Application.DTOs.Tournaments;
using CyberManagement.Application.Exceptions;
using CyberManagement.Application.Services;
using CyberManagement.TeamBalancing.Training;

// All data in this executable is generated test-only data, never a training dataset.
var checks = 0;
var now = DateTimeOffset.Parse("2026-10-01T12:00:00Z", CultureInfo.InvariantCulture);
var roles = new[] { "Duelist", "Duelist", "Initiator", "Initiator", "Controller", "Controller", "Sentinel", "Sentinel", "Flex", "Flex" };
var service = new TeamBalancingService();
var roster = Input();
var proposal = service.Propose(roster);
Check("snake exact membership, canonical A/B", proposal.Teams[0].Members.Select(m => m.RegistrationId).SequenceEqual(new[] { 1, 4, 5, 8, 9 })
    && proposal.Teams[1].Members.Select(m => m.RegistrationId).SequenceEqual(new[] { 2, 3, 6, 7, 10 }));
Check("two teams of five, no duplicate or missing player", proposal.Teams.Count == 2 && proposal.Teams.All(t => t.Members.Count == 5)
    && proposal.Teams.SelectMany(t => t.Members).Select(m => m.RegistrationId).Order().SequenceEqual(Enumerable.Range(1, 10)));
Check("baseline metadata does not claim ML", proposal.ModelVersion is null && proposal.ModelSha256 is null
    && proposal.DatasetVersion is null && proposal.BaselineVersion == TeamBalanceVersions.Baseline && !proposal.Persisted
    && proposal.Quality.PredictedWinProbabilityA is null && proposal.Quality.PredictedImbalance is null);
Check("canonical output hash is deterministic", proposal.ProposalId == service.Propose(roster).ProposalId);
Check("permuting roster preserves proposal", service.Propose(roster with { Participants = roster.Participants.Reverse().ToArray() }).ProposalId == proposal.ProposalId);
Check("input and snapshots are not mutated", TeamBalanceJson.Hash(roster) == TeamBalanceJson.Hash(Input()));
Check("missing source timestamp warns", proposal.Warnings.SequenceEqual(new[] { "SOURCE_FRESHNESS_UNKNOWN" }));

Reject("9 players", () => service.Propose(roster with { Participants = roster.Participants.Take(9).ToArray() }), "TeamSizeMismatch");
Reject("11 players", () => service.Propose(roster with { Participants = roster.Participants.Append(roster.Participants[0]).ToArray() }), "TeamSizeMismatch");
Reject("duplicate registration", () => service.Propose(Change(1, p => p with { RegistrationId = 1 })), "ValidationError");
Reject("duplicate customer", () => service.Propose(Change(1, p => p with { CustomerId = 1 })), "ValidationError");
Reject("duplicate Riot ID ignores case and whitespace", () => service.Propose(Change(1, p => p with { RiotId = " player1 #vn " })), "ValidationError");
Reject("bad Riot ID", () => service.Propose(Change(0, p => p with { RiotId = "player#two tags" })), "ValidationError");
Reject("null participant", () => service.Propose(Change(0, _ => null!)), "ValidationError");
Reject("inactive account is not silently removed", () => service.Propose(Change(0, p => p with { CustomerStatus = "Locked" })), "TeamPlanInvalid");
Reject("cancelled registration", () => service.Propose(Change(0, p => p with { RegistrationStatus = "Cancelled" })), "TeamPlanInvalid");
Reject("ineligible fee", () => service.Propose(Change(0, p => p with { FeeEligible = false })), "TeamPlanInvalid");
Reject("unpaid entry", () => service.Propose(roster with { FeePolicy = new(1000, null, 0) }), "TeamPlanInvalid");
Reject("open tournament", () => service.Propose(roster with { Status = "RegistrationOpen" }), "TournamentLocked");
Reject("confirmed teams", () => service.Propose(roster with { HasConfirmedTeams = true }), "TeamsAlreadyFormed");
Reject("unsupported game", () => service.Propose(roster with { GameKey = "lol" }), "UNSUPPORTED_GAME_OR_SIZE");
Reject("unsupported ML team count", () => service.Propose(roster with { Strategy = "ml", TeamCount = 3 }), "UNSUPPORTED_GAME_OR_SIZE");
Reject("ten players cannot form three baseline teams", () => service.Propose(roster with { TeamCount = 3 }), "TeamSizeMismatch");
var fifteen = Enumerable.Range(1, 15).Select(i => new TeamBalanceParticipant(i, i, "Active", "Registered",
    null, null, true, $"Player{i}#VN", "Flex", Snapshot(i, 28 - i))).ToArray();
var three = service.Propose(Rehash(roster with { TeamCount = 3, Participants = fifteen }));
Check("Balanced supports three complete VALORANT teams", three.Teams.Count == 3
    && three.Teams.All(t => t.Members.Count == 5) && three.Teams.SelectMany(t => t.Members)
        .Select(m => m.RegistrationId).Order().SequenceEqual(Enumerable.Range(1, 15)));
Reject("unknown contract", () => service.Propose(roster with { ContractVersion = "v0" }), "ValidationError");
Reject("invalid version", () => service.Propose(roster with { ExpectedVersion = "not-base64" }), "ValidationError");
Reject("roster hash mismatch", () => service.Propose(roster with { RosterVersion = "sha256:invalid" }), "TeamPlanInvalid");
Reject("unknown strategy", () => service.Propose(roster with { Strategy = "Balanced" }), "ValidationError");
Reject("ML without artifact never falls back", () => service.Propose(roster with { Strategy = "ml" }), "MODEL_UNAVAILABLE");
Reject("missing role does not become Flex", () => service.Propose(Change(0, p => p with { PreferredRole = null! })), "TeamPlanInvalid");

var impossible = roster with { Participants = roster.Participants.Select(p => p with { PreferredRole = "Duelist" }).ToArray() };
Reject("impossible roles do not rewrite snake", () => service.Propose(Rehash(impossible)), "ROLE_CONSTRAINT_UNSATISFIABLE");
var flexRoster = Rehash(Change(0, p => p with { PreferredRole = "Flex" }));
Check("Flex fills missing role deterministically", service.Propose(flexRoster).Teams[0].Members[0].AssignedRole == "Duelist");
var tied = Rehash(roster with { Participants = roster.Participants.Select(p => p with { Snapshot = Snapshot(p.RegistrationId, 15) }).ToArray() });
Check("equal ranks tie by registration ID", service.Propose(tied).Teams[0].Members.Select(m => m.RegistrationId).SequenceEqual(new[] { 1, 4, 5, 8, 9 }));
Reject("missing snapshot", () => service.Propose(Change(0, p => p with { Snapshot = null! })), "STATS_UNAVAILABLE");
Reject("fixture origin", () => service.Propose(Change(0, p => p with { Snapshot = p.Snapshot with { Provenance = p.Snapshot.Provenance with { Origin = "fixture" } } })), "STATS_UNAVAILABLE");
Reject("modified snapshot hash", () => service.Propose(Change(0, p => p with { Snapshot = p.Snapshot with { Provenance = p.Snapshot.Provenance with { SnapshotSha256 = "sha256:wrong" } } })), "STATS_UNAVAILABLE");
Reject("stale stats", () => service.Propose(roster with { GeneratedAtUtc = now.AddHours(25) }), "STATS_STALE");
Reject("future stats", () => service.Propose(roster with { GeneratedAtUtc = now.AddMinutes(-10) }), "STATS_STALE");
Reject("scope mismatch", () => service.Propose(Change(0, p => p with { Snapshot = Snapshot(1, 27, scope: "Another act") })), "STATS_SCOPE_MISMATCH");
Reject("rank zero", () => service.Propose(Change(0, p => p with { Snapshot = Snapshot(1, 0) })), "STATS_UNAVAILABLE");
Reject("fewer than ten games", () => service.Propose(Change(0, p => p with { Snapshot = Snapshot(1, 27, matches: 9) })), "STATS_UNAVAILABLE");
Reject("out of range rate", () => service.Propose(Change(0, p => p with { Snapshot = Snapshot(1, 27, winRate: 101) })), "STATS_UNAVAILABLE");

var observed = TeamBalanceValidation.Snapshot(Snapshot(1, 27, winRate: 0), "Player1#VN", now);
var missing = TeamBalanceValidation.Snapshot(Snapshot(1, 27, winRate: null), "Player1#VN", now);
var medians = TeamBalanceFeatures.FitMedians(new[] { observed, observed with { WinRate = 60 } });
Check("zero remains observed and median fits only supplied train", medians[0] == 30 && TeamBalanceFeatures.Player(observed, medians)[1] == 0
    && TeamBalanceFeatures.Player(observed, medians)[6] == 0);
Check("null uses training median and missing flag", TeamBalanceFeatures.Player(missing, medians)[1] == .3
    && TeamBalanceFeatures.Player(missing, medians)[6] == 1);
Reject("all missing training column blocks preprocessing", () => TeamBalanceFeatures.FitMedians(new[] { missing }), "DATASET_INVALID");

using (var vector = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "fixtures", "snapshot-hash.json"))))
    Check("existing Node snapshot hash vector", TeamBalanceJson.Hash(vector.RootElement.GetProperty("canonical")) == vector.RootElement.GetProperty("snapshotSha256").GetString());
CanonicalChecks();

var source = new DatasetSource("fixture-tests-only", TeamBalanceVersions.Feature, "https://fixtures.invalid/dataset", "TEST ONLY; not a real source",
    new string('a', 40), now.AddDays(1), "sha256:" + new string('0', 64), "generated-fixture");
var matches = Enumerable.Range(0, 1000).Select(Match).ToArray();
var dataset = DatasetPreparation.Prepare(source, matches, "test-only", "test-only");
Check("70/15/15 split counts original matches", dataset.Manifest.Folds.Select(f => f.Count).SequenceEqual(new[] { 700, 150, 150 }));
Check("augment train only", dataset.Rows["train"].Length == 1400 && dataset.Rows["validation"].Length == 150 && dataset.Rows["test"].Length == 150);
Check("train augmentation keeps match/group and reverses labels/features", dataset.Rows["train"].Chunk(2).All(pair =>
    pair[0].MatchId == pair[1].MatchId && pair[0].GroupId == pair[1].GroupId && pair[0].Label != pair[1].Label
    && pair[0].Features.Zip(pair[1].Features).All(p => p.First == -p.Second)));
Check("leakage manifest includes exact original IDs/hashes", dataset.Manifest.Folds.SelectMany(f => f.Matches).Select(m => m.MatchId).Distinct().Count() == 1000
    && dataset.Manifest.Folds.SelectMany(f => f.Matches).All(m => m.SnapshotHashes.Length == 10));
Check("temporal player overlap explicitly reported", dataset.Manifest.TrainTestPlayerOverlap == 200
    && dataset.Manifest.GeneralizationLimit.Contains("unverified", StringComparison.Ordinal));
Check("baseline evaluated on all test rosters", JsonSerializer.SerializeToElement(dataset.EvaluationBaseline).GetProperty("totalRosters").GetInt32() == 150);

var alteredEvaluation = matches.Select((m, i) => i < 700 ? m : m with
{
    TeamA = m.TeamA.Select(p => p with { Snapshot = Snapshot(int.Parse(p.PlayerKey[1..], CultureInfo.InvariantCulture), 15, time: m.StartedAtUtc, winRate: 99) }).ToArray(),
    TeamB = m.TeamB.Select(p => p with { Snapshot = Snapshot(int.Parse(p.PlayerKey[1..], CultureInfo.InvariantCulture), 15, time: m.StartedAtUtc, winRate: 99) }).ToArray()
}).ToArray();
var changed = DatasetPreparation.Prepare(source, alteredEvaluation, "test-only", "test-only");
Check("evaluation values cannot alter fitted training medians", dataset.Manifest.TrainingMedians.SequenceEqual(changed.Manifest.TrainingMedians)
    && dataset.Rows["train"].SelectMany(r => r.Features).SequenceEqual(changed.Rows["train"].SelectMany(r => r.Features)));
Check("identical duplicate removed before split", DatasetPreparation.Prepare(source, matches.Append(matches[0]).ToArray(), "test-only", "test-only").Manifest.DuplicateRowsRemoved == 1);
Reject("conflicting duplicate label", () => DatasetPreparation.Prepare(source, matches.Append(matches[0] with { Winner = "B" }).ToArray(), "test-only", "test-only"), "DATASET_INVALID");
Reject("alias overlap across chronological folds", () => PrepareChanged(999, m => m with { Aliases = new[] { "m0" } }), "DATASET_INVALID");
Reject("group crossing boundary quarantines rather than leaks", () => PrepareChanged(700, m => m with { GroupId = matches[699].GroupId }), "DATASET_INVALID");
Reject("late training label is quarantined rather than leaking", () => PrepareChanged(699, m => m with { ResultVerifiedAtUtc = matches[700].StartedAtUtc }), "DATASET_INVALID");
Reject("too few matches", () => DatasetPreparation.Prepare(source, matches.Take(999).ToArray(), "test-only", "test-only"), "DATASET_INVALID");
Reject("unverified source result", () => PrepareChanged(999, m => m with { ResultOrigin = "fixture" }), "DATASET_INVALID");
Reject("missing label", () => PrepareChanged(999, m => m with { Winner = null! }), "DATASET_INVALID");
Reject("post-start snapshot cannot train", () => PrepareChanged(0, m => m with { StartedAtUtc = m.StartedAtUtc.AddMinutes(-10) }), "STATS_STALE");
Reject("source rights required", () => DatasetPreparation.Prepare(source with { UsageTerms = "" }, matches, "test-only", "test-only"), "DATASET_INVALID");
CliChecks();
Console.WriteLine($"{checks} team balancing and dataset checks passed (generated fixtures only; no production model or real evaluation).");
return;

void Check(string name, bool passed)
{
    if (!passed) throw new Exception("FAIL " + name);
    checks++;
    Console.WriteLine("PASS " + name);
}

void Reject(string name, Action action, string code)
{
    try { action(); }
    catch (TeamBalanceException e) when (e.Code == code) { Check(name, true); return; }
    throw new Exception($"FAIL {name}: expected {code}");
}

TeamBalanceInput Input()
{
    var people = Enumerable.Range(1, 10).Select(i => new TeamBalanceParticipant(i, i, "Active", "Registered", null, null, true,
        $"Player{i}#VN", roles[i - 1], Snapshot(i, 28 - i))).ToArray();
    return Rehash(new(TeamBalanceVersions.Contract, TeamBalanceVersions.Feature, TeamBalanceVersions.Policy, TeamBalanceVersions.Baseline,
        7, 42, "valorant", "RegistrationClosed", 5, 2, false, "AAAAAAAAAAE=", "", now, new(0, null, 0), people));
}

TeamBalanceInput Change(int index, Func<TeamBalanceParticipant, TeamBalanceParticipant> change) => roster with
{ Participants = roster.Participants.Select((p, i) => i == index ? change(p) : p).ToArray() };
TeamBalanceInput Rehash(TeamBalanceInput input) => input with { RosterVersion = TeamBalanceValidation.RosterVersion(input) };

OpggSnapshot Snapshot(int id, int tier, DateTimeOffset? time = null, string scope = "Act · Competitive", int matches = 20, double? winRate = 50)
{
    var reference = time ?? now;
    var stamp = (DateTimeOffset t) => t.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture);
    var stats = JsonSerializer.SerializeToElement(new
    {
        riotId = $"Player{id}#VN", source = "opgg", queue = "competitive", scope,
        fetchedAt = stamp(reference.AddMinutes(-2)), sourceUpdatedAt = (string?)null,
        rank = new { tier, name = "Gold", division = 2, label = "Gold 2" }, matches,
        winRate, headshotRate = 20d, damagePerRound = 140d, kda = 1.2d
    });
    var origin = "provider"; // Simulated collector proof for unit checks only.
    var capturedAtUtc = stamp(reference.AddMinutes(-1));
    var snapshotId = new Guid(SHA256.HashData(Encoding.UTF8.GetBytes($"{id}:{capturedAtUtc}")).AsSpan(0, 16)).ToString("D");
    return new(stats, new(origin, snapshotId, capturedAtUtc, TeamBalanceJson.Hash(new { stats, provenance = new { origin, snapshotId, capturedAtUtc } })));
}

MatchRecord Match(int index)
{
    var started = now.AddHours(index - 1100);
    var players = Enumerable.Range(1, 10).Select(i =>
    {
        var id = index % 20 * 10 + i;
        return new MatchPlayer($"p{id}", roles[i - 1], Snapshot(id, 28 - i, time: started));
    }).ToArray();
    return new($"m{index}", Array.Empty<string>(), $"g{index}", "Act · Competitive", "test-patch", started,
        started.AddMinutes(30), "provider", "https://fixtures.invalid/results", started.AddMinutes(31), "A", players[..5], players[5..]);
}

void PrepareChanged(int index, Func<MatchRecord, MatchRecord> change) => DatasetPreparation.Prepare(source,
    matches.Select((m, i) => i == index ? change(m) : m).ToArray(), "test-only", "test-only");

void CanonicalChecks()
{
    var values = new List<JsonElement>();
    var random = new Random(42);
    for (var i = 0; i < 400; i++)
    {
        var number = BitConverter.Int64BitsToDouble(random.NextInt64());
        if (double.IsFinite(number)) values.Add(JsonSerializer.SerializeToElement(new { number, unicode = "Tím\u2028😀<>&\n", z = -number }));
    }
    values.Add(JsonSerializer.SerializeToElement(new { numbers = new[] { -0d, 1e-7, 1e-6, 1e20, 1e21, 1e23, 333333333.33333329, double.Epsilon } }));
    values.Add(JsonSerializer.SerializeToElement(proposal));
    values.Add(JsonSerializer.SerializeToElement(roster));
    var start = new ProcessStartInfo("node") { RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
    start.ArgumentList.Add(Path.Combine(AppContext.BaseDirectory, "canonical-check.mjs"));
    using var node = Process.Start(start)!;
    var oracleOutput = node.StandardOutput.ReadToEndAsync();
    foreach (var value in values) node.StandardInput.WriteLine(value.GetRawText());
    node.StandardInput.Close();
    var hashes = oracleOutput.GetAwaiter().GetResult().Split('\n', StringSplitOptions.RemoveEmptyEntries);
    node.WaitForExit();
    Check("Node/.NET canonical hashes agree on numbers, Unicode, roster and proposal", node.ExitCode == 0
        && hashes.Length == values.Count && hashes.Zip(values).All(pair => pair.First == TeamBalanceJson.Hash(pair.Second)));
    try { using var duplicate = JsonDocument.Parse("{\"x\":1,\"x\":2}"); TeamBalanceJson.Hash(duplicate.RootElement); }
    catch (JsonException) { Check("duplicate JSON key rejected", true); return; }
    throw new Exception("Duplicate JSON key was accepted");
}

void CliChecks()
{
    // bin/ is ignored. Keep generated files for diagnosing a failed CLI check.
    var folder = Path.Combine(AppContext.BaseDirectory, "cli-fixtures", Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(folder);
    var sourcePath = Path.Combine(folder, "source.json");
    var matchesPath = Path.Combine(folder, "matches.jsonl");
    var output = Path.Combine(folder, "prepared");
    var compact = new JsonSerializerOptions(DatasetPreparation.JsonOptions) { WriteIndented = false };
    File.WriteAllLines(matchesPath, matches.Select(m => JsonSerializer.Serialize(m, compact)));
    string HashFile(string path) => "sha256:" + Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(path)));
    var matchingSource = source with { MatchesSha256 = HashFile(matchesPath) };
    File.WriteAllText(sourcePath, JsonSerializer.Serialize(matchingSource, compact));
    var invocation = Run("prepare", sourcePath, matchesPath, output);
    Check("CLI prepares full fixture dataset", invocation.Exit == 0 && File.Exists(Path.Combine(output, "artifact-hashes.json")));
    using var hashes = JsonDocument.Parse(File.ReadAllText(Path.Combine(output, "artifact-hashes.json")));
    Check("CLI output file hashes verify", hashes.RootElement.EnumerateObject().Count() == 6
        && hashes.RootElement.EnumerateObject().All(p => p.Value.GetString() == HashFile(Path.Combine(output, p.Name))));
    var trainedOutput = Path.Combine(folder, "model-fixture");
    invocation = Run("train", output, "fixture-check.v1", trainedOutput);
    Check("CLI trains actual ML.NET model on generated fixtures", invocation.Exit == 0 && File.Exists(Path.Combine(trainedOutput, "model.zip")));
    checks += TeamBalanceMlChecks.Run(output, trainedOutput);
    invocation = Run("train", output, "fixture-check.v2", Path.Combine(folder, "repeat-model"));
    Check("CLI rejects repeated holdout evaluation", invocation.Exit == 1 && invocation.Error.Contains("EVALUATION_ALREADY_USED", StringComparison.Ordinal));
    invocation = Run("train", output, "bad/version", Path.Combine(folder, "bad-model-version"));
    Check("CLI validates model version", invocation.Exit == 1 && invocation.Error.Contains("ValidationError", StringComparison.Ordinal));
    Check("CLI preserves train/evaluation row counts", File.ReadLines(Path.Combine(output, "train.jsonl")).Count() == 1400
        && File.ReadLines(Path.Combine(output, "validation.jsonl")).Count() == 150 && File.ReadLines(Path.Combine(output, "test.jsonl")).Count() == 150);
    var before = HashFile(Path.Combine(output, "split-manifest.json"));
    invocation = Run("prepare", sourcePath, matchesPath, output);
    Check("CLI refuses to overwrite existing artifacts", invocation.Exit == 1 && invocation.Error.Contains("OUTPUT_EXISTS", StringComparison.Ordinal)
        && HashFile(Path.Combine(output, "split-manifest.json")) == before);
    File.WriteAllText(sourcePath, JsonSerializer.Serialize(source, compact));
    var rejected = Path.Combine(folder, "rejected");
    invocation = Run("prepare", sourcePath, matchesPath, rejected);
    Check("CLI verifies raw dataset hash before creating output", invocation.Exit == 1
        && invocation.Error.Contains("DATASET_INVALID", StringComparison.Ordinal) && !Directory.Exists(rejected));
    File.WriteAllText(sourcePath, JsonSerializer.Serialize(matchingSource, compact)[..^1] + ",\"unexpected\":true}");
    invocation = Run("prepare", sourcePath, matchesPath, rejected);
    Check("CLI rejects unknown source fields", invocation.Exit == 1 && !Directory.Exists(rejected));
    Check("CLI reports usage errors", Run().Exit == 2);

    (int Exit, string Error) Run(params string[] arguments)
    {
        var command = new ProcessStartInfo("dotnet") { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        command.ArgumentList.Add(typeof(DatasetPreparation).Assembly.Location);
        foreach (var argument in arguments) command.ArgumentList.Add(argument);
        using var process = Process.Start(command)!;
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        process.WaitForExit();
        _ = stdout.GetAwaiter().GetResult();
        return (process.ExitCode, stderr.GetAwaiter().GetResult());
    }
}
