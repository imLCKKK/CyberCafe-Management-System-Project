using CyberManagement.Application.DTOs.Tournaments;
using CyberManagement.Application.Exceptions;

namespace CyberManagement.Application.Services;

public static class TeamBalanceFeatures
{
    public static readonly IReadOnlyList<string> Names = Array.AsReadOnly(new[]
    {
        "rankTier", "winRateFraction", "headshotRateFraction", "damagePerRound", "kda", "log1pMatches",
        "winRateMissing", "headshotRateMissing", "damagePerRoundMissing", "kdaMissing"
    });

    // Call only with the training fold. Evaluation and inference reuse these four medians.
    public static double[] FitMedians(IEnumerable<TeamBalancePlayerStats> trainingPlayers)
    {
        var columns = Enumerable.Range(0, 4).Select(_ => new List<double>()).ToArray();
        foreach (var player in trainingPlayers)
        {
            var values = NullableValues(player);
            for (var i = 0; i < columns.Length; i++)
                if (values[i].HasValue) columns[i].Add(values[i]!.Value);
        }
        return columns.Select(column =>
        {
            if (column.Count == 0) throw new TeamBalanceException("DATASET_INVALID", "Cột feature không có giá trị trong train.");
            column.Sort();
            var middle = column.Count / 2;
            return column.Count % 2 == 0 ? column[middle - 1] / 2 + column[middle] / 2 : column[middle];
        }).ToArray();
    }

    public static double[] Player(TeamBalancePlayerStats player, IReadOnlyList<double> medians)
    {
        if (medians.Count != 4 || medians.Any(m => !double.IsFinite(m) || m < 0)
            || medians[0] > 100 || medians[1] > 100)
            throw new TeamBalanceException("DATASET_INVALID", "Median feature không hợp lệ.");
        var nullable = NullableValues(player);
        var values = nullable.Select((v, i) => v ?? medians[i]).ToArray();
        return [player.RankTier, values[0] / 100, values[1] / 100, values[2], values[3], Math.Log(1d + player.Matches),
            nullable[0].HasValue ? 0 : 1, nullable[1].HasValue ? 0 : 1, nullable[2].HasValue ? 0 : 1, nullable[3].HasValue ? 0 : 1];
    }

    public static double[] Difference(IReadOnlyList<double[]> a, IReadOnlyList<double[]> b)
    {
        if (a.Count != 5 || b.Count != 5 || a.Concat(b).Any(v => v.Length != 10 || v.Any(x => !double.IsFinite(x))))
            throw new TeamBalanceException("DATASET_INVALID", "Feature trận cần hai đội 5 người, mỗi vector 10 chiều hữu hạn.");
        var result = Enumerable.Range(0, 10).Select(i => a.Sum(v => v[i] / 5) - b.Sum(v => v[i] / 5)).ToArray();
        if (result.Any(x => !double.IsFinite(x)))
            throw new TeamBalanceException("DATASET_INVALID", "Feature trận vượt giới hạn số hữu hạn.");
        return result;
    }

    private static double?[] NullableValues(TeamBalancePlayerStats player) =>
        [player.WinRate, player.HeadshotRate, player.DamagePerRound, player.Kda];
}
