namespace CyberManagement.TeamBalancing.Training;

public sealed record CalibrationBin(int Bin, int Count, double? MeanProbability, double? ObservedWinRate);
public sealed record PredictionMetrics(int Count, int PositiveLabels, int NegativeLabels, double Accuracy,
    double F1, double? RocAuc, double Brier, double LogLoss, IReadOnlyList<CalibrationBin> Calibration);
public sealed record MetricInterval(double? Lower, double? Upper);
public sealed record GroupBootstrap(int Resamples, int Groups, int AucValidResamples, MetricInterval RocAuc, MetricInterval Brier, MetricInterval LogLoss);
public sealed record PredictionReport(PredictionMetrics Metrics, GroupBootstrap Confidence95);

public static class PredictionEvaluation
{
    public static PredictionMetrics Measure(IReadOnlyList<bool> labels, IReadOnlyList<double> probabilities)
    {
        if (labels.Count == 0 || labels.Count != probabilities.Count || probabilities.Any(p => !double.IsFinite(p) || p < 0 || p > 1))
            throw new ArgumentException("Cần nhãn và xác suất hữu hạn cùng số lượng, không rỗng.");
        var tp = 0; var fp = 0; var fn = 0; var correct = 0;
        var brier = 0d; var logLoss = 0d;
        for (var i = 0; i < labels.Count; i++)
        {
            var predicted = probabilities[i] >= .5;
            if (predicted == labels[i]) correct++;
            if (predicted && labels[i]) tp++;
            if (predicted && !labels[i]) fp++;
            if (!predicted && labels[i]) fn++;
            var y = labels[i] ? 1 : 0;
            brier += Math.Pow(probabilities[i] - y, 2);
            var p = Math.Clamp(probabilities[i], 1e-15, 1 - 1e-15);
            logLoss -= y * Math.Log(p) + (1 - y) * Math.Log(1 - p);
        }
        var positives = labels.Count(y => y);
        var negatives = labels.Count - positives;
        var precedingNegatives = 0; var concordance = 0d;
        foreach (var tied in Enumerable.Range(0, labels.Count).OrderBy(i => probabilities[i]).GroupBy(i => probabilities[i]))
        {
            var positive = tied.Count(i => labels[i]);
            var negative = tied.Count() - positive;
            concordance += positive * (precedingNegatives + negative / 2d);
            precedingNegatives += negative;
        }
        var bins = Enumerable.Range(0, 10).Select(bin =>
        {
            var rows = Enumerable.Range(0, labels.Count).Where(i => Math.Min(9, (int)(probabilities[i] * 10)) == bin).ToArray();
            return new CalibrationBin(bin, rows.Length, rows.Length == 0 ? null : rows.Average(i => probabilities[i]),
                rows.Length == 0 ? null : rows.Average(i => labels[i] ? 1d : 0));
        }).ToArray();
        return new(labels.Count, positives, negatives, (double)correct / labels.Count,
            2 * tp + fp + fn == 0 ? 0 : 2d * tp / (2 * tp + fp + fn),
            positives == 0 || negatives == 0 ? null : concordance / positives / negatives,
            brier / labels.Count, logLoss / labels.Count, bins);
    }

    public static PredictionReport Report(PreparedMatch[] rows, double[] probabilities)
    {
        var metrics = Measure(rows.Select(r => r.Label).ToArray(), probabilities);
        var groups = rows.Select((row, index) => (row.GroupId, index)).GroupBy(x => x.GroupId, StringComparer.Ordinal)
            .OrderBy(g => g.Key, StringComparer.Ordinal).Select(g => g.Select(x => x.index).ToArray()).ToArray();
        var random = new Random(42);
        var samples = new List<PredictionMetrics>(500);
        for (var sample = 0; sample < 500; sample++)
        {
            var indices = Enumerable.Range(0, groups.Length).SelectMany(_ => groups[random.Next(groups.Length)]).ToArray();
            samples.Add(Measure(indices.Select(i => rows[i].Label).ToArray(), indices.Select(i => probabilities[i]).ToArray()));
        }
        return new(metrics, new(500, groups.Length, samples.Count(m => m.RocAuc.HasValue), Interval(samples.Select(m => m.RocAuc)),
            Interval(samples.Select(m => (double?)m.Brier)), Interval(samples.Select(m => (double?)m.LogLoss))));
    }

    public static bool PassesGate(PredictionMetrics model, PredictionMetrics constant, PredictionMetrics rankOnly) =>
        model.RocAuc >= .6 && model.Brier <= .24 && model.Brier <= .95 * constant.Brier
        && model.Brier <= .95 * rankOnly.Brier && model.LogLoss <= Math.Min(constant.LogLoss, rankOnly.LogLoss);

    private static MetricInterval Interval(IEnumerable<double?> values)
    {
        var sorted = values.Where(x => x.HasValue).Select(x => x!.Value).Order().ToArray();
        if (sorted.Length == 0) return new(null, null);
        double Quantile(double p)
        {
            var index = p * (sorted.Length - 1); var lower = (int)index;
            return sorted[lower] + (sorted[Math.Min(lower + 1, sorted.Length - 1)] - sorted[lower]) * (index - lower);
        }
        return new(Quantile(.025), Quantile(.975));
    }
}
