using CyberManagement.Application.DTOs.Tournaments;

namespace CyberManagement.Application.Interfaces.Services;

public interface ITournamentService
{
    Task<OpggSnapshot> RefreshStatsAsync(int tournamentId, int registrationId, int? ownerId, CancellationToken token);
    Task RequireActiveEmployeeAsync(int employeeId, CancellationToken cancellationToken);
    Task<IReadOnlyList<GameDto>> GetGamesAsync(bool includeInactive, CancellationToken cancellationToken);
    Task<IReadOnlyList<GameRankDto>> GetRanksAsync(int gameId, CancellationToken cancellationToken);
    Task<IReadOnlyList<TournamentDto>> GetTournamentsAsync(bool includeDrafts, CancellationToken cancellationToken);
    Task<TournamentDto> GetTournamentAsync(int tournamentId, bool includeDraft, CancellationToken cancellationToken);
    Task<IReadOnlyList<TournamentRegistrationDto>> GetAdminRegistrationsAsync(int tournamentId, CancellationToken cancellationToken);
    Task<IReadOnlyList<ClientTournamentRegistrationDto>> GetClientRegistrationsAsync(
        int tournamentId, int customerId, CancellationToken cancellationToken);
    Task<CreateTournamentResult> CreateAsync(int employeeId, string key, CreateTournamentRequest request, CancellationToken cancellationToken);
    Task<TournamentDto> UpdateAsync(int employeeId, int tournamentId, UpdateTournamentRequest request, CancellationToken cancellationToken);
    Task<TransitionTournamentResult> TransitionAsync(int employeeId, int tournamentId, string key, TransitionTournamentRequest request, CancellationToken cancellationToken);
    Task<TournamentRegistrationDto> RegisterAsync(
        int tournamentId, int customerId, TournamentRegistrationRequest request, CancellationToken cancellationToken);
}
