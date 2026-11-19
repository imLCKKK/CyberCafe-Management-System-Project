using CyberManagement.Application.DTOs.Tournaments;
using CyberManagement.Application.Exceptions;
using CyberManagement.Application.Interfaces.Services;

namespace CyberManagement.Application.Services;

public sealed class TeamBalancingService(ITeamBalanceScorer? scorer = null) : ITeamBalancingService
{
    public TeamBalanceProposal Propose(TeamBalanceInput input)
    {
        var stats = TeamBalanceValidation.Roster(input);
        var warnings = stats.Values.SelectMany(s => s.Warnings).Distinct().Order(StringComparer.Ordinal).ToList();
        var ml = input.Strategy == "ml";
        TeamBalanceTeam[] teams;
        double? probability = null;
        if (ml)
        {
            if (scorer is null) throw new TeamBalanceException("MODEL_UNAVAILABLE", "Chưa có model đủ điều kiện suy luận.");
            if (!scorer.Supports(stats.Values.First().Scope))
                throw new TeamBalanceException("STATS_SCOPE_MISMATCH", "Model không hỗ trợ scope của giải.");
            (teams, probability) = Optimize(input, stats, scorer);
            if (Math.Abs(probability.Value - .5) > .10) warnings.Add("BALANCE_TARGET_UNMET");
        }
        else teams = Snake(input, stats);
        var gap = teams.Max(t => t.Members.Average(m => stats[m.RegistrationId].RankTier))
            - teams.Min(t => t.Members.Average(m => stats[m.RegistrationId].RankTier));
        var proposal = new TeamBalanceProposal(TeamBalanceVersions.Contract, input.TournamentId, input.RosterVersion,
            "", input.Strategy, ml ? scorer!.ModelVersion : null, ml ? scorer!.ModelSha256 : null,
            TeamBalanceVersions.Feature, TeamBalanceVersions.Policy, ml ? scorer!.DatasetVersion : null,
            ml ? null : TeamBalanceVersions.Baseline, Array.AsReadOnly(teams),
            new(probability, probability.HasValue ? Math.Abs(probability.Value - .5) : null, gap, true), warnings.AsReadOnly(), false);
        return WithId(proposal);
    }

    private static TeamBalanceTeam[] Snake(TeamBalanceInput input, IReadOnlyDictionary<int, TeamBalancePlayerStats> stats)
    {
        var ordered = input.Participants.OrderByDescending(p => stats[p.RegistrationId].RankTier)
            .ThenBy(p => p.RegistrationId).ToArray();
        var groups = Enumerable.Range(0, input.TeamCount).Select(_ => new List<TeamBalanceParticipant>(input.TeamSize)).ToArray();
        for (var i = 0; i < ordered.Length; i++)
            groups[(i / groups.Length) % 2 == 0 ? i % groups.Length : groups.Length - 1 - i % groups.Length].Add(ordered[i]);
        return groups.OrderBy(g => g.Min(p => p.RegistrationId)).Select((g, i) =>
            AssignRoles(g, i < 26 ? ((char)('A' + i)).ToString() : $"Team{i + 1}")).ToArray();
    }

    private static (TeamBalanceTeam[], double) Optimize(TeamBalanceInput input,
        IReadOnlyDictionary<int, TeamBalancePlayerStats> stats, ITeamBalanceScorer model)
    {
        var players = input.Participants.OrderBy(p => p.RegistrationId).ToArray();
        var features = stats.ToDictionary(p => p.Key, p => TeamBalanceFeatures.Player(p.Value, model.TrainingMedians));
        var candidates = new List<(TeamBalanceTeam[] Teams, double[] Difference)>(126);
        // Enumerate in lexicographic order, fixing player 0 in A. Stable ordering resolves exact ties.
        foreach (var indices in TeamASelections())
        {
            var a = indices.Select(index => players[index]).ToArray();
            var other = players.ExceptBy(a.Select(p => p.RegistrationId), p => p.RegistrationId).ToArray();
            if (!CanCover(a) || !CanCover(other)) continue;
            candidates.Add(([AssignRoles(a, "A"), AssignRoles(other, "B")], TeamBalanceFeatures.Difference(
                a.Select(p => features[p.RegistrationId]).ToArray(), other.Select(p => features[p.RegistrationId]).ToArray())));
        }
        if (candidates.Count == 0) throw new TeamBalanceException("ROLE_CONSTRAINT_UNSATISFIABLE", "Không có hai đội phủ đủ bốn vai trò.");
        var probabilities = model.Predict(candidates.Select(c => c.Difference).ToArray());
        if (probabilities.Length != candidates.Count || probabilities.Any(p => !double.IsFinite(p) || p < 0 || p > 1))
            throw new TeamBalanceException("MODEL_UNAVAILABLE", "Model trả xác suất không hợp lệ.");
        var best = Enumerable.Range(0, candidates.Count).OrderBy(i => Math.Abs(probabilities[i] - .5))
            .ThenBy(i => Math.Abs(candidates[i].Teams[0].Members.Sum(m => stats[m.RegistrationId].RankTier)
                - candidates[i].Teams[1].Members.Sum(m => stats[m.RegistrationId].RankTier))).First();
        return (candidates[best].Teams, probabilities[best]);
    }

    private static IEnumerable<int[]> TeamASelections()
    {
        for (var b = 1; b <= 6; b++)
        for (var c = b + 1; c <= 7; c++)
        for (var d = c + 1; d <= 8; d++)
        for (var e = d + 1; e <= 9; e++)
            yield return [0, b, c, d, e];
    }

    private static bool CanCover(TeamBalanceParticipant[] players) =>
        players.Where(p => p.PreferredRole != "Flex").Select(p => p.PreferredRole).Distinct().Count()
        + players.Count(p => p.PreferredRole == "Flex") >= 4;

    private static TeamBalanceProposal WithId(TeamBalanceProposal proposal)
    {
        return proposal with { ProposalId = TeamBalanceJson.Hash(new
        {
            proposal.ContractVersion, proposal.TournamentId, proposal.RosterVersion, proposal.Method,
            proposal.ModelVersion, proposal.ModelSha256, proposal.FeatureVersion, proposal.PolicyVersion,
            proposal.DatasetVersion, proposal.BaselineVersion, proposal.Teams
        }) };
    }

    public static TeamBalanceTeam AssignRoles(IReadOnlyList<TeamBalanceParticipant> players, string teamKey)
    {
        var missing = new Queue<string>(TeamBalanceValidation.Roles.Take(4)
            .Where(role => !players.Any(p => p.PreferredRole == role)));
        var members = players.OrderBy(p => p.RegistrationId).Select(p => new TeamBalanceMember(p.RegistrationId,
            p.PreferredRole == "Flex" && missing.Count > 0 ? missing.Dequeue() : p.PreferredRole)).ToArray();
        if (players.Count != 5 || missing.Count != 0)
            throw new TeamBalanceException("ROLE_CONSTRAINT_UNSATISFIABLE", "Phân đội không phủ đủ Duelist/Initiator/Controller/Sentinel; baseline không tự đổi người.");
        return new(teamKey, Array.AsReadOnly(members));
    }
}
