using CyberManagement.Application.Exceptions;
using Microsoft.ML;
using Microsoft.ML.Data;
using Microsoft.ML.Trainers;

namespace CyberManagement.Infrastructure.TeamBalancing;

public sealed class TeamBalanceMlRow
{
    [VectorType(10)] public float[] Features { get; set; } = new float[10];
    public bool Label { get; set; }
}

public sealed class TeamBalanceMlPrediction
{
    public float Probability { get; set; }
}

// Each prediction creates its own IDataView/cursor. No shared mutable PredictionEngine.
public sealed class TeamBalanceMlModel
{
    public const string PackageVersion = "5.0.1";
    private readonly MLContext context;
    private readonly ITransformer model;
    private readonly DataViewSchema schema;
    private readonly bool rankOnly;

    private TeamBalanceMlModel(MLContext context, ITransformer model, DataViewSchema schema, bool rankOnly)
    { this.context = context; this.model = model; this.schema = schema; this.rankOnly = rankOnly; }

    public static TeamBalanceMlModel Fit(IEnumerable<(bool Label, double[] Features)> train, float l2, bool rankOnly)
    {
        if (l2 is not (.01f or .1f or 1f)) throw new ArgumentOutOfRangeException(nameof(l2));
        var context = new MLContext(seed: 42);
        var data = context.Data.LoadFromEnumerable(train.Select(r => Row(r.Features, rankOnly, r.Label)));
        var pipeline = context.Transforms.NormalizeMeanVariance("Features", fixZero: true)
            .Append(context.BinaryClassification.Trainers.SdcaLogisticRegression(new SdcaLogisticRegressionBinaryTrainer.Options
            {
                FeatureColumnName = "Features", LabelColumnName = "Label", L1Regularization = 0,
                L2Regularization = l2, MaximumNumberOfIterations = 100, NumberOfThreads = 1, Shuffle = false
            }));
        return new(context, pipeline.Fit(data), data.Schema, rankOnly);
    }

    public void Save(Stream destination) => context.Model.Save(model, schema, destination);

    public static TeamBalanceMlModel Load(Stream source, bool rankOnly)
    {
        var context = new MLContext(seed: 42);
        var model = context.Model.Load(source, out var schema);
        if (schema.GetColumnOrNull("Features")?.Type is not VectorDataViewType { Size: 10 } vector
            || vector.ItemType != NumberDataViewType.Single)
            throw new TeamBalanceException("MODEL_UNAVAILABLE", "Schema model phải có 10 feature float.");
        var output = model.GetOutputSchema(schema);
        if (output.GetColumnOrNull("Probability")?.Type != NumberDataViewType.Single)
            throw new TeamBalanceException("MODEL_UNAVAILABLE", "Model không có xác suất đầu ra tương thích.");
        return new(context, model, schema, rankOnly);
    }

    public double[] Predict(IReadOnlyList<double[]> differences)
    {
        if (differences.Count == 0) return [];
        var rows = differences.SelectMany(d => new[] { Row(d, rankOnly), Row(d.Select(x => -x).ToArray(), rankOnly) });
        var predictions = context.Data.CreateEnumerable<TeamBalanceMlPrediction>(model.Transform(context.Data.LoadFromEnumerable(rows)),
            reuseRowObject: false).Select(p => (double)p.Probability).ToArray();
        if (predictions.Length != differences.Count * 2 || predictions.Any(p => !double.IsFinite(p) || p < 0 || p > 1))
            throw new TeamBalanceException("MODEL_UNAVAILABLE", "Model trả xác suất không hợp lệ.");
        return Enumerable.Range(0, differences.Count).Select(i => (predictions[2 * i] + 1 - predictions[2 * i + 1]) / 2).ToArray();
    }

    private static TeamBalanceMlRow Row(double[] features, bool rankOnly, bool label = false)
    {
        if (features is null || features.Length != 10 || features.Any(x => !double.IsFinite(x) || Math.Abs(x) > float.MaxValue))
            throw new TeamBalanceException("DATASET_INVALID", "ML.NET cần 10 feature biểu diễn được bằng float hữu hạn.");
        return new() { Label = label, Features = features.Select((x, i) => rankOnly && i != 0 ? 0f : (float)x).ToArray() };
    }
}
