using System.Security.Cryptography;
using System.Text.Json;
using CyberManagement.Application.DTOs.Tournaments;
using CyberManagement.Application.Exceptions;
using CyberManagement.Application.Services;

namespace CyberManagement.Infrastructure.TeamBalancing;

public sealed record TeamBalanceModelManifest(
    string ModelVersion, string ContractVersion, string FeatureVersion, string PolicyVersion,
    string Algorithm, string PackageVersion, string DatasetVersion, string DataKind,
    string Game, string Platform, string Queue, IReadOnlyList<string> SupportedScopes, IReadOnlyList<string> SupportedPatches,
    DateTimeOffset TrainingTimeCutoff, DateTimeOffset TrainedAtUtc, string CodeCommit,
    string Runtime, string OperatingSystem, string Architecture, string TrainingAssemblySha256, string ScoringAssemblySha256,
    int Seed, int Iterations, int Threads, bool Shuffle, float L1, float ModelL2, float RankOnlyL2,
    IReadOnlyList<string> FeatureNames, IReadOnlyList<double> TrainingMedians,
    string ModelSha256, string RankOnlyModelSha256, string SplitManifestHash, string MetricsSha256,
    bool PredictionGatePassed, bool SymmetryGatePassed, bool ReadyForInference,
    IReadOnlyList<string> Limitations);

public sealed class TeamBalanceModelArtifact : CyberManagement.Application.Interfaces.Services.ITeamBalanceScorer
{
    public TeamBalanceModelManifest Manifest { get; }
    public TeamBalanceMlModel Model { get; }
    public IReadOnlyList<double> TrainingMedians { get; }
    public string ModelVersion => Manifest.ModelVersion;
    public string ModelSha256 => Manifest.ModelSha256;
    public string DatasetVersion => Manifest.DatasetVersion;
    public bool Supports(string scope) => Manifest.SupportedScopes.Contains(scope, StringComparer.Ordinal);
    public double[] Predict(IReadOnlyList<double[]> differences) => Model.Predict(differences);

    private TeamBalanceModelArtifact(TeamBalanceModelManifest manifest, TeamBalanceMlModel model)
    { Manifest = manifest; Model = model; TrainingMedians = Array.AsReadOnly(manifest.TrainingMedians.ToArray()); }

    // expectedManifestHash must be pinned in trusted server configuration, never supplied by a browser.
    public static TeamBalanceModelArtifact Load(string directory, string expectedManifestHash)
    {
        try
        {
            var bytes = File.ReadAllBytes(Path.Combine(directory, "model-manifest.json"));
            if (Hash(bytes) != expectedManifestHash) throw new InvalidDataException();
            var manifest = JsonSerializer.Deserialize<TeamBalanceModelManifest>(bytes, new JsonSerializerOptions(JsonSerializerDefaults.Web)
            { UnmappedMemberHandling = System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow }) ?? throw new InvalidDataException();
            if (manifest.ContractVersion != TeamBalanceVersions.Contract || manifest.FeatureVersion != TeamBalanceVersions.Feature
                || manifest.PolicyVersion != TeamBalanceVersions.Policy || manifest.Algorithm != "sdca-logistic-regression"
                || manifest.PackageVersion != TeamBalanceMlModel.PackageVersion || manifest.DataKind != "observed-matches"
                || !manifest.ReadyForInference || !manifest.PredictionGatePassed || !manifest.SymmetryGatePassed
                || manifest.Game != "valorant" || manifest.Platform != "pc" || manifest.Queue != "competitive"
                || manifest.SupportedScopes is not { Count: > 0 } || manifest.SupportedPatches is not { Count: > 0 }
                || manifest.FeatureNames is null || !manifest.FeatureNames.SequenceEqual(TeamBalanceFeatures.Names)
                || manifest.TrainingMedians is not { Count: 4 } || manifest.TrainingMedians.Any(x => !double.IsFinite(x) || x < 0)
                || manifest.TrainingMedians[0] > 100 || manifest.TrainingMedians[1] > 100)
                throw new InvalidDataException();
            var modelBytes = File.ReadAllBytes(Path.Combine(directory, "model.zip"));
            if (Hash(modelBytes) != manifest.ModelSha256
                || Hash(File.ReadAllBytes(Path.Combine(directory, "rank-only-model.zip"))) != manifest.RankOnlyModelSha256
                || Hash(File.ReadAllBytes(Path.Combine(directory, "metrics.json"))) != manifest.MetricsSha256
                || Hash(File.ReadAllBytes(Path.Combine(directory, "split-manifest.json"))) != manifest.SplitManifestHash)
                throw new InvalidDataException();
            return new(manifest, TeamBalanceMlModel.Load(new MemoryStream(modelBytes, writable: false), rankOnly: false));
        }
        catch (Exception error) when (error is IOException or InvalidDataException or UnauthorizedAccessException or JsonException
            or InvalidOperationException or ArgumentException or FormatException or TeamBalanceException)
        { throw new TeamBalanceException("MODEL_UNAVAILABLE", "Artifact thiếu, hỏng, sai version hoặc chưa đạt gate suy luận."); }
    }

    public static string Hash(byte[] bytes) => "sha256:" + Convert.ToHexStringLower(SHA256.HashData(bytes));
}
