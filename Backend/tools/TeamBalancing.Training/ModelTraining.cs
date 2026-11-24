using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.RegularExpressions;
using CyberManagement.Application.DTOs.Tournaments;
using CyberManagement.Application.Exceptions;
using CyberManagement.Application.Services;
using CyberManagement.Infrastructure.TeamBalancing;

namespace CyberManagement.TeamBalancing.Training;

public sealed record ValidationCandidate(bool RankOnly, float L2, PredictionMetrics Metrics);
public sealed record ModelTrainingResult(TeamBalanceModelManifest Manifest, string ManifestSha256);

public static class ModelTraining
{
    public static ModelTrainingResult Run(string preparedDirectory, string modelVersion, string outputDirectory, string codeCommit)
    {
        if (string.IsNullOrWhiteSpace(modelVersion) || !Regex.IsMatch(modelVersion, @"^[a-zA-Z0-9][a-zA-Z0-9._-]{0,99}$"))
            throw new TeamBalanceException("ValidationError", "Model version cần 1..100 ký tự chữ, số, chấm, gạch nối hoặc gạch dưới.");
        if (Directory.Exists(outputDirectory) || File.Exists(outputDirectory))
            throw new TeamBalanceException("OUTPUT_EXISTS", "Không ghi đè artifact model có sẵn.");
        var dataset = PreparedDatasetReader.Read(preparedDirectory);
        var splitHash = TeamBalanceModelArtifact.Hash(dataset.SplitManifestBytes);
        var ledger = Path.Combine(dataset.Directory, ".evaluation-" + splitHash[7..] + ".json");
        if (File.Exists(ledger)) throw new TeamBalanceException("EVALUATION_ALREADY_USED", "Holdout này đã được đánh giá; không tái dùng để sửa/chọn model. Cần evaluation mới.");
        var train = dataset.Rows["train"];
        var validation = dataset.Rows["validation"];
        var candidates = new List<ValidationCandidate>();
        (TeamBalanceMlModel Model, float L2) Select(bool rankOnly)
        {
            var trained = new List<(TeamBalanceMlModel Model, float L2, PredictionMetrics Metrics)>();
            foreach (var l2 in new[] { .01f, .1f, 1f })
            {
                var model = TeamBalanceMlModel.Fit(train.Select(r => (r.Label, r.Features)), l2, rankOnly);
                var metrics = PredictionEvaluation.Measure(validation.Select(r => r.Label).ToArray(), model.Predict(validation.Select(r => r.Features).ToArray()));
                candidates.Add(new(rankOnly, l2, metrics));
                trained.Add((model, l2, metrics));
            }
            var selected = trained.OrderBy(x => x.Metrics.Brier).ThenBy(x => x.Metrics.LogLoss).ThenBy(x => x.L2).First();
            return (selected.Model, selected.L2);
        }
        var baseline = Select(rankOnly: true);
        var selected = Select(rankOnly: false);
        Directory.CreateDirectory(outputDirectory);
        void Write(string name, object value) => File.WriteAllText(Path.Combine(outputDirectory, name), JsonSerializer.Serialize(value, DatasetPreparation.JsonOptions));
        // Freeze the selected models/options before looking at test predictions or metrics.
        Write("selection.json", new { modelVersion, modelL2 = selected.L2, rankOnlyL2 = baseline.L2, splitManifestHash = splitHash,
            selectionMetric = "validation Brier; tie: log loss, L2", candidates });
        using (var file = File.Create(Path.Combine(outputDirectory, "model.zip"))) selected.Model.Save(file);
        using (var file = File.Create(Path.Combine(outputDirectory, "rank-only-model.zip"))) baseline.Model.Save(file);
        File.WriteAllBytes(Path.Combine(outputDirectory, "split-manifest.json"), dataset.SplitManifestBytes);
        using (var file = new FileStream(ledger, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            JsonSerializer.Serialize(file, new { modelVersion, splitHash, evaluatedAtUtc = DateTimeOffset.UtcNow,
                selectionSha256 = HashFile(Path.Combine(outputDirectory, "selection.json")), dataKind = dataset.Manifest.Source.DataKind }, DatasetPreparation.JsonOptions);

        var test = dataset.Rows["test"];
        var vectors = test.Select(r => r.Features).ToArray();
        var probabilities = selected.Model.Predict(vectors);
        var rankProbabilities = baseline.Model.Predict(vectors);
        var constantReport = PredictionEvaluation.Report(test, Enumerable.Repeat(.5, test.Length).ToArray());
        var rankReport = PredictionEvaluation.Report(test, rankProbabilities);
        var modelReport = PredictionEvaluation.Report(test, probabilities);
        var opposite = selected.Model.Predict(vectors.Select(d => d.Select(x => -x).ToArray()).ToArray());
        var symmetryError = probabilities.Zip(opposite).Max(p => Math.Abs(p.First + p.Second - 1));
        using var reloadedFile = File.OpenRead(Path.Combine(outputDirectory, "model.zip"));
        var reloaded = TeamBalanceMlModel.Load(reloadedFile, rankOnly: false);
        var reloadError = probabilities.Zip(reloaded.Predict(vectors)).Max(p => Math.Abs(p.First - p.Second));
        var predictionPassed = PredictionEvaluation.PassesGate(modelReport.Metrics, constantReport.Metrics, rankReport.Metrics);
        var symmetryPassed = symmetryError <= 1e-6 && reloadError <= 1e-6;
        var fixture = dataset.Manifest.Source.DataKind == "generated-fixture";
        var baselineAllocation = AllocationReport(dataset, selected.Model);
        var optimizedAllocation = OptimizerReport(dataset, selected.Model);
        Write("metrics.json", new
        {
            dataKind = dataset.Manifest.Source.DataKind, evaluation = "held-out test; symmetric probability p(d)=(q(d)+1-q(-d))/2",
            constantBaselineB1 = constantReport, rankOnlyBaselineB2 = rankReport, modelM1 = modelReport,
            predictionGatePassed = predictionPassed, symmetryGatePassed = symmetryPassed, symmetryError, reloadError,
            allocationBaselineB0 = baselineAllocation,
            allocationOptimizedM1 = optimizedAllocation,
            limits = new[] { "No cold-player holdout, observed outcomes for proposed teams, or end-to-end API latency measurement.",
                fixture ? "GENERATED FIXTURES: technical tests only, never eligible for production inference." : "Source authenticity/usage terms require independent verification of raw captures/results." }
        });
        var metadata = dataset.Manifest;
        var manifest = new TeamBalanceModelManifest(modelVersion, TeamBalanceVersions.Contract, TeamBalanceVersions.Feature, TeamBalanceVersions.Policy,
            "sdca-logistic-regression", TeamBalanceMlModel.PackageVersion, metadata.Source.DatasetVersion, metadata.Source.DataKind,
            "valorant", "pc", "competitive", metadata.SupportedScopes, metadata.SupportedPatches,
            metadata.Folds.Single(f => f.Name == "train").Matches.Max(r => r.ResultVerifiedAtUtc), DateTimeOffset.UtcNow, codeCommit, RuntimeInformation.FrameworkDescription,
            RuntimeInformation.OSDescription, RuntimeInformation.ProcessArchitecture.ToString(), HashFile(typeof(ModelTraining).Assembly.Location),
            HashFile(typeof(TeamBalanceMlModel).Assembly.Location),
            42, 100, 1, false, 0, selected.L2, baseline.L2, TeamBalanceFeatures.Names, metadata.TrainingMedians,
            HashFile(Path.Combine(outputDirectory, "model.zip")), HashFile(Path.Combine(outputDirectory, "rank-only-model.zip")), splitHash,
            HashFile(Path.Combine(outputDirectory, "metrics.json")), predictionPassed, symmetryPassed, !fixture && predictionPassed && symmetryPassed,
            new[] { "No verified cold-player generalization, map/agent/party effects or observed post-proposal balance.",
                "Allocation evaluation uses the service optimizer offline; end-to-end SQL/API latency is unverified.",
                fixture ? "Generated fixtures: blocked for production inference." : "Limited to the verified source population and training scopes/patches." });
        Write("model-manifest.json", manifest);
        var manifestHash = HashFile(Path.Combine(outputDirectory, "model-manifest.json"));
        File.WriteAllText(Path.Combine(outputDirectory, "model-manifest.sha256"), manifestHash + "\n");
        Write("artifact-hashes.json", Directory.GetFiles(outputDirectory).Order(StringComparer.Ordinal)
            .ToDictionary(path => Path.GetFileName(path)!, HashFile));
        return new(manifest, manifestHash);
    }

    private static object AllocationReport(VerifiedPreparedDataset dataset, TeamBalanceMlModel model)
    {
        var baseline = dataset.Baseline;
        var rows = JsonSerializer.Deserialize<AllocationBaselineRow[]>(baseline.GetProperty("validRosterDetails"), DatasetPreparation.JsonOptions)
            ?? throw new TeamBalanceException("DATASET_INVALID", "Thiếu feature baseline phân đội.");
        var invalid = baseline.GetProperty("invalidRoleRosters").EnumerateArray().Select(x => x.GetString()).ToArray();
        var testIds = dataset.Rows["test"].Select(r => r.MatchId).ToHashSet(StringComparer.Ordinal);
        if (rows.Length != baseline.GetProperty("validRosters").GetInt32() || rows.Length + invalid.Length != testIds.Count
            || !rows.Select(r => r.MatchId).Concat(invalid.Select(x => x!)).ToHashSet(StringComparer.Ordinal).SetEquals(testIds))
            throw new TeamBalanceException("DATASET_INVALID", "Baseline không bao phủ đúng mọi roster test.");
        var probabilities = model.Predict(rows.Select(r => r.Features).ToArray());
        return new
        {
            method = TeamBalanceVersions.Baseline, totalRosters = testIds.Count, validRosters = rows.Length, invalidRoleRosters = invalid,
            validityRate = (double)rows.Length / testIds.Count,
            rosters = rows.Select((r, i) => new { r.MatchId, r.RankMeanGap, r.Warnings,
                surrogateProbabilityA = probabilities[i], surrogateImbalance = Math.Abs(probabilities[i] - .5) }).ToArray(),
            warning = "M1 probabilities on proposed B0 teams are surrogate only; these teams have no observed labels."
        };
    }

    private static object OptimizerReport(VerifiedPreparedDataset dataset, TeamBalanceMlModel model)
    {
        var valid = 0;
        var rosters = dataset.OptimizerRosters.Select(roster =>
        {
            var members = roster.Players.Select((player, i) => (RegistrationId: i + 1, player.PreferredRole)).ToArray();
            var features = roster.Players.Select((player, i) => (Id: i + 1, player.Features))
                .ToDictionary(player => player.Id, player => player.Features);
            double? probability = null;
            double? imbalance = null;
            double? rankMeanGap = null;
            var warnings = roster.Warnings.ToList();
            try
            {
                var (teams, p) = TeamBalancingService.Optimize(members, features, model.Predict);
                probability = p;
                imbalance = Math.Abs(p - .5);
                rankMeanGap = Math.Abs(teams[0].Members.Average(m => features[m.RegistrationId][0])
                    - teams[1].Members.Average(m => features[m.RegistrationId][0]));
                if (imbalance > .10) warnings.Add("BALANCE_TARGET_UNMET");
                valid++;
            }
            catch (TeamBalanceException error) when (error.Code == "ROLE_CONSTRAINT_UNSATISFIABLE")
            {
                warnings.Add(error.Code);
            }
            return new { roster.MatchId, roleCoverageValid = probability.HasValue, rankMeanGap,
                surrogateProbabilityA = probability, surrogateImbalance = imbalance, warnings };
        }).ToArray();
        return new { method = "ml", totalRosters = rosters.Length, validRosters = valid,
            validityRate = (double)valid / rosters.Length, rosters };
    }

    private static string HashFile(string path) => TeamBalanceModelArtifact.Hash(File.ReadAllBytes(path));
}
