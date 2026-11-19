using System.Text.Json;
using CyberManagement.Application.Exceptions;
using CyberManagement.Infrastructure.TeamBalancing;
using CyberManagement.TeamBalancing.Training;

internal static class TeamBalanceMlChecks
{
    public static int Run(string preparedDirectory, string artifactDirectory)
    {
        var checks = 0;
        void Check(string name, bool result)
        {
            if (!result) throw new Exception("FAIL ML: " + name);
            checks++;
            Console.WriteLine("PASS ML: " + name);
        }
        void Reject(string name, Action action, string code)
        {
            try { action(); }
            catch (TeamBalanceException e) when (e.Code == code) { Check(name, true); return; }
            throw new Exception("FAIL ML: " + name);
        }
        var prepared = PreparedDatasetReader.Read(preparedDirectory);
        var manifestBytes = File.ReadAllBytes(Path.Combine(artifactDirectory, "model-manifest.json"));
        var manifestHash = TeamBalanceModelArtifact.Hash(manifestBytes);
        var manifest = JsonSerializer.Deserialize<TeamBalanceModelManifest>(manifestBytes, DatasetPreparation.JsonOptions)!;
        Check("fixture artifact cannot be marked ready", manifest.DataKind == "generated-fixture" && !manifest.ReadyForInference);
        Check("runtime/package/options recorded", manifest.PackageVersion == "5.0.1" && manifest.Seed == 42 && manifest.Iterations == 100
            && manifest.Threads == 1 && !manifest.Shuffle && manifest.L1 == 0 && manifest.TrainingMedians.Count == 4);
        Check("training cutoff includes label availability before validation", manifest.TrainingTimeCutoff < prepared.Manifest.TrainBeforeUtc
            && manifest.TrainingTimeCutoff == prepared.Manifest.Folds.Single(f => f.Name == "train").Matches.Max(m => m.ResultVerifiedAtUtc));
        Check("manifest binds model, split and metrics hashes", manifest.ModelSha256 == HashFile("model.zip")
            && manifest.RankOnlyModelSha256 == HashFile("rank-only-model.zip") && manifest.MetricsSha256 == HashFile("metrics.json")
            && manifest.SplitManifestHash == HashFile("split-manifest.json"));
        Check("manifest hash recorded exactly", File.ReadAllText(Path.Combine(artifactDirectory, "model-manifest.sha256")).Trim() == manifestHash);
        Reject("production loader rejects generated fixture", () => TeamBalanceModelArtifact.Load(artifactDirectory, manifestHash), "MODEL_UNAVAILABLE");
        Reject("production loader rejects wrong pinned manifest", () => TeamBalanceModelArtifact.Load(artifactDirectory, "sha256:wrong"), "MODEL_UNAVAILABLE");
        Reject("production loader rejects missing artifact", () => TeamBalanceModelArtifact.Load(Path.Combine(artifactDirectory, "absent"), manifestHash), "MODEL_UNAVAILABLE");
        using var modelStream = File.OpenRead(Path.Combine(artifactDirectory, "model.zip"));
        var model = TeamBalanceMlModel.Load(modelStream, rankOnly: false);
        using var rankStream = File.OpenRead(Path.Combine(artifactDirectory, "rank-only-model.zip"));
        var rank = TeamBalanceMlModel.Load(rankStream, rankOnly: true);
        var features = prepared.Rows["test"].Select(r => r.Features).ToArray();
        var probabilities = model.Predict(features);
        Check("saved model returns finite probabilities", probabilities.Length == 150 && probabilities.All(p => double.IsFinite(p) && p >= 0 && p <= 1));
        Check("A/B inversion gives complementary probability", probabilities.Zip(model.Predict(features.Select(d => d.Select(x => -x).ToArray()).ToArray()))
            .All(p => Math.Abs(p.First + p.Second - 1) <= 1e-6));
        var changedStats = features.Select(d => d.Select((x, i) => i == 0 ? x : x + 100).ToArray()).ToArray();
        Check("B2 ignores every non-rank feature", rank.Predict(features).SequenceEqual(rank.Predict(changedStats)));
        Check("concurrent batch inference is consistent", Task.WhenAll(Enumerable.Range(0, 8).Select(_ => Task.Run(() => model.Predict(features))))
            .GetAwaiter().GetResult().All(p => p.SequenceEqual(probabilities)));
        using var metrics = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(artifactDirectory, "metrics.json")));
        var report = metrics.RootElement.GetProperty("modelM1");
        Check("test metrics count originals, not augmented rows", report.GetProperty("metrics").GetProperty("count").GetInt32() == 150);
        Check("confidence intervals resample competition groups", report.GetProperty("confidence95").GetProperty("resamples").GetInt32() == 500
            && report.GetProperty("confidence95").GetProperty("groups").GetInt32() == 150);
        var baseline = metrics.RootElement.GetProperty("allocationBaselineB0");
        Check("allocation baseline covers invalid role rosters explicitly", baseline.GetProperty("validRosters").GetInt32()
            + baseline.GetProperty("invalidRoleRosters").GetArrayLength() == 150);
        Check("save/load and symmetry checks were measured", metrics.RootElement.GetProperty("symmetryGatePassed").GetBoolean()
            && metrics.RootElement.GetProperty("reloadError").GetDouble() <= 1e-6);
        using var selection = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(artifactDirectory, "selection.json")));
        Check("six hyperparameter candidates evaluated on validation", selection.RootElement.GetProperty("candidates").GetArrayLength() == 6);

        var known = PredictionEvaluation.Measure(new[] { false, true }, new[] { .1, .9 });
        Check("AUC/F1/Brier/log loss match independently known values", known.RocAuc == 1 && known.F1 == 1 && known.Accuracy == 1
            && Math.Abs(known.Brier - .01) < 1e-12 && Math.Abs(known.LogLoss + Math.Log(.9)) < 1e-12);
        var constant = PredictionEvaluation.Measure(new[] { false, true }, new[] { .5, .5 });
        Check("constant baseline ties get AUC 0.5", constant.RocAuc == .5 && constant.Brier == .25
            && Math.Abs(constant.LogLoss - Math.Log(2)) < 1e-12);
        Check("calibration bins cover all rows", known.Calibration.Sum(b => b.Count) == 2);
        Check("single-class AUC is undefined rather than invented", PredictionEvaluation.Measure(new[] { true, true }, new[] { .1, .9 }).RocAuc is null);
        Check("quality gate requires improvement over rank-only B2", !PredictionEvaluation.PassesGate(known, constant, known)
            && PredictionEvaluation.PassesGate(known, constant, constant));
        var rows = Enumerable.Range(0, 200).Select(i =>
        {
            var vector = new double[10]; vector[3] = (i % 2 == 0 ? -1 : 1) * (1 + i / 200d);
            return (Label: i % 2 != 0, Features: vector);
        }).ToArray();
        var learned = TeamBalanceMlModel.Fit(rows, .01f, rankOnly: false);
        var learnedRank = TeamBalanceMlModel.Fit(rows, .01f, rankOnly: true);
        var learnedProbabilities = learned.Predict(rows.Select(r => r.Features).ToArray());
        Check("ML learns a stats signal absent from rank", PredictionEvaluation.Measure(rows.Select(r => r.Label).ToArray(), learnedProbabilities).RocAuc == 1
            && learnedRank.Predict(rows.Select(r => r.Features).ToArray()).All(p => Math.Abs(p - .5) <= 1e-6));
        var retrained = TeamBalanceMlModel.Fit(rows, .01f, rankOnly: false);
        Check("fixed-seed training is repeatable", learnedProbabilities.Zip(retrained.Predict(rows.Select(r => r.Features).ToArray()))
            .All(p => Math.Abs(p.First - p.Second) <= 1e-6));
        var originalTrain = File.ReadAllBytes(Path.Combine(preparedDirectory, "train.jsonl"));
        File.AppendAllText(Path.Combine(preparedDirectory, "train.jsonl"), "{}\n");
        Reject("training reader rejects changed prepared bytes", () => PreparedDatasetReader.Read(preparedDirectory), "DATASET_INVALID");
        File.WriteAllBytes(Path.Combine(preparedDirectory, "train.jsonl"), originalTrain);
        return checks;

        string HashFile(string name) => TeamBalanceModelArtifact.Hash(File.ReadAllBytes(Path.Combine(artifactDirectory, name)));
    }
}
