using CyberManagement.Application.DTOs.Tournaments;

namespace CyberManagement.Application.Interfaces.Services;

public sealed record TeamPreviewRoster(TeamBalanceInput Input, IReadOnlyDictionary<int, string> CustomerNames);
public interface ITeamPreviewRosterReader
{
    Task<TeamPreviewRoster> ReadAsync(int tournamentId, string strategy, string expectedVersion, CancellationToken token);
}
