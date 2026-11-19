namespace CyberManagement.Application.Interfaces.Services;

public interface ITeamBalanceScorer
{
    string ModelVersion { get; }
    string ModelSha256 { get; }
    string DatasetVersion { get; }
    IReadOnlyList<double> TrainingMedians { get; }
    bool Supports(string scope);
    double[] Predict(IReadOnlyList<double[]> differences);
}
