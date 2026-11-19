using System.Text.Json.Serialization;
using CyberManagement.Application.DTOs.Tournaments;
using Microsoft.Extensions.Caching.Memory;

namespace CyberManagement.API;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record TeamPreviewRequest(string Strategy, string ExpectedVersion);

public sealed record TeamPreviewMember(int RegistrationId, string CustomerName, int SkillScore, string RankName, string AssignedRole);
public sealed record TeamPreviewTeam(string TeamName, double AvgSkillScore, TeamPreviewMember[] Members);
public sealed record TeamPlanDto(string Version, string RosterVersion, string ProposalId, Guid PlanToken,
    DateTimeOffset ExpiresAtUtc, string ModelSource, string? ModelVersion, string? BaselineVersion,
    TeamPreviewTeam[] Teams, int[] Unassigned, double ScoreSpread, TeamBalanceQuality Quality, IReadOnlyList<string> Warnings);

public sealed record TeamPlanBinding(int EmployeeId, TeamBalanceInput Input, TeamBalanceProposal Proposal, DateTimeOffset ExpiresAtUtc);

public sealed class TeamPreviewPlans : IDisposable
{
    private readonly MemoryCache cache = new(new MemoryCacheOptions { SizeLimit = 100_000 });

    public Guid Issue(TeamPlanBinding binding)
    {
        var key = Guid.NewGuid();
        cache.Set(key, binding, new MemoryCacheEntryOptions
        {
            AbsoluteExpiration = binding.ExpiresAtUtc, Size = binding.Input.Participants.Count
        });
        if (!cache.TryGetValue(key, out _))
            throw new CyberManagement.Application.Exceptions.TeamBalanceException("PreviewUnavailable", "Bộ nhớ đề xuất đang đầy. Vui lòng thử lại.");
        return key;
    }

    public TeamPlanBinding? Find(Guid token, int employeeId, int tournamentId) =>
        cache.TryGetValue<TeamPlanBinding>(token, out var binding) && binding?.EmployeeId == employeeId
            && binding.Input.TournamentId == tournamentId ? binding : null;

    public void Dispose() => cache.Dispose();
}
