using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;
using CyberManagement.Application.DTOs.Realtime;
using CyberManagement.Application.DTOs.Tournaments;
using CyberManagement.Application.Exceptions;
using CyberManagement.Application.Interfaces.Repositories;
using CyberManagement.Application.Interfaces.Services;

namespace CyberManagement.Application.Services;

public sealed class TournamentService(ITournamentRepository repository, IRealtimePublisher publisher) : ITournamentService
{
    private static readonly HashSet<string> PreferredRoles =
        ["Duelist", "Initiator", "Controller", "Sentinel", "Flex"];

    public Task RequireActiveEmployeeAsync(int employeeId, CancellationToken cancellationToken)
    {
        ValidateId(employeeId, "EmployeeId");
        return repository.RequireActiveEmployeeAsync(employeeId, cancellationToken);
    }

    public Task<IReadOnlyList<GameDto>> GetGamesAsync(bool includeInactive, CancellationToken cancellationToken) =>
        repository.GetGamesAsync(includeInactive, cancellationToken);

    public Task<IReadOnlyList<GameRankDto>> GetRanksAsync(int gameId, CancellationToken cancellationToken)
    {
        ValidateId(gameId, "GameId");
        return repository.GetRanksAsync(gameId, cancellationToken);
    }

    public Task<IReadOnlyList<TournamentDto>> GetTournamentsAsync(bool includeDrafts, CancellationToken cancellationToken) =>
        repository.GetTournamentsAsync(includeDrafts, cancellationToken);

    public Task<TournamentDto> GetTournamentAsync(int tournamentId, bool includeDraft, CancellationToken cancellationToken)
    {
        ValidateId(tournamentId, "TournamentId");
        return repository.GetTournamentAsync(tournamentId, includeDraft, cancellationToken);
    }

    public async Task<IReadOnlyList<TournamentRegistrationDto>> GetAdminRegistrationsAsync(
        int tournamentId, CancellationToken cancellationToken)
    {
        ValidateId(tournamentId, "TournamentId");
        _ = await repository.GetTournamentAsync(tournamentId, true, cancellationToken);
        return await repository.GetAdminRegistrationsAsync(tournamentId, cancellationToken);
    }

    public async Task<IReadOnlyList<ClientTournamentRegistrationDto>> GetClientRegistrationsAsync(
        int tournamentId, int customerId, CancellationToken cancellationToken)
    {
        ValidateId(tournamentId, "TournamentId");
        ValidateId(customerId, "CustomerId");
        _ = await repository.GetTournamentAsync(tournamentId, false, cancellationToken);
        return await repository.GetClientRegistrationsAsync(tournamentId, customerId, cancellationToken);
    }

    public async Task<CreateTournamentResult> CreateAsync(
        int employeeId, string key, CreateTournamentRequest request, CancellationToken cancellationToken)
    {
        ValidateId(employeeId, "EmployeeId");
        if (key is null || !Regex.IsMatch(key, @"\A[A-Za-z0-9._:-]{8,100}\z"))
            throw new ValidationException("Idempotency-Key phải có 8–100 ký tự ASCII hợp lệ.");
        ValidateTournament(request.GameId, request.Name, request.RegistrationStartUtc,
            request.RegistrationEndUtc, request.StartTimeUtc, request.EntryFee);
        var result = await repository.CreateAsync(employeeId, key, request, cancellationToken);
        if (!result.Replayed)
            await PublishAsync(RealtimeEvent.TournamentChanged, result.Tournament.Id);
        return result;
    }

    public async Task<TournamentDto> UpdateAsync(
        int employeeId, int tournamentId, UpdateTournamentRequest request, CancellationToken cancellationToken)
    {
        ValidateId(employeeId, "EmployeeId");
        ValidateId(tournamentId, "TournamentId");
        try
        {
            if (request.ExpectedVersion is null || Convert.FromBase64String(request.ExpectedVersion).Length != 8)
                throw new ValidationException("expectedVersion không hợp lệ.");
        }
        catch (FormatException)
        {
            throw new ValidationException("expectedVersion không hợp lệ.");
        }
        ValidateTournament(request.GameId, request.Name, request.RegistrationStartUtc,
            request.RegistrationEndUtc, request.StartTimeUtc, request.EntryFee);
        var tournament = await repository.UpdateAsync(employeeId, tournamentId, request, cancellationToken);
        await PublishAsync(RealtimeEvent.TournamentChanged, tournament.Id);
        return tournament;
    }

    public async Task<TournamentDto> TransitionAsync(
        int employeeId, int tournamentId, string status, CancellationToken cancellationToken)
    {
        ValidateId(employeeId, "EmployeeId");
        ValidateId(tournamentId, "TournamentId");
        if (status is not ("RegistrationOpen" or "RegistrationClosed"))
            throw new ValidationException("Trạng thái đích chỉ được là RegistrationOpen hoặc RegistrationClosed.");
        var tournament = await repository.TransitionAsync(employeeId, tournamentId, status, cancellationToken);
        await PublishAsync(RealtimeEvent.TournamentChanged, tournament.Id);
        return tournament;
    }

    public async Task<TournamentRegistrationDto> RegisterAsync(
        int tournamentId, int customerId, TournamentRegistrationRequest request, CancellationToken cancellationToken)
    {
        ValidateId(tournamentId, "TournamentId");
        ValidateId(customerId, "CustomerId");
        var riotId = NormalizeRiotId(request.RiotId);
        if (request.RankId <= 0)
            throw new ValidationException("Hạng trò chơi là bắt buộc.");
        if (request.PreferredRole is { Length: > 50 })
            throw new ValidationException("Vai trò sở trường không được vượt quá 50 ký tự.");
        if (!string.IsNullOrWhiteSpace(request.PreferredRole) && !PreferredRoles.Contains(request.PreferredRole))
            throw new ValidationException("Vai trò sở trường không hợp lệ.");
        if (request.WinRate is < 0 or > 100
            || request.WinRate is { } winRate && decimal.Round(winRate, 2) != winRate)
            throw new ValidationException("Tỷ lệ thắng phải từ 0 đến 100 và tối đa 2 chữ số thập phân.");
        if (request.HoursPlayed is < 0 or > 100000)
            throw new ValidationException("Số giờ chơi phải từ 0 đến 100000.");

        var result = await repository.RegisterAsync(tournamentId, customerId,
            request with { RiotId = riotId, PreferredRole = request.PreferredRole?.Trim() }, cancellationToken);
        await PublishAsync(RealtimeEvent.RegistrationChanged, tournamentId);
        return result;
    }

    private async Task PublishAsync(string eventType, int tournamentId) =>
        await publisher.PublishTournamentChangedAsync(
            new RealtimeEvent(Guid.NewGuid(), eventType, tournamentId, DateTimeOffset.UtcNow),
            CancellationToken.None);

    private static void ValidateTournament(
        int gameId, string? name, DateTime registrationStart, DateTime registrationEnd, DateTime startTime, decimal entryFee)
    {
        ValidateId(gameId, "GameId");
        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > 150)
            throw new ValidationException("Tên giải đấu là bắt buộc và tối đa 150 ký tự.");
        if (registrationStart >= registrationEnd || registrationEnd > startTime)
            throw new ValidationException("Thời gian phải thỏa mãn mở đăng ký < đóng đăng ký <= giờ thi đấu ở tương lai.");
        if (entryFee < 0 || entryFee > 999999999 || decimal.Truncate(entryFee) != entryFee)
            throw new ValidationException("Lệ phí phải là số nguyên VND từ 0 đến 999.999.999.");
    }

    private static string NormalizeRiotId(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > 128
            || value.Any(char.IsControl))
            throw new ValidationException("Riot ID phải có dạng Tên#TAG.");
        var parts = value.Split('#');
        if (parts.Length != 2)
            throw new ValidationException("Riot ID phải có đúng một dấu #.");
        var name = parts[0].Trim();
        var tag = parts[1].Trim();
        if (name.Length is < 1 or > 100 || tag.Length is < 1 or > 20 || tag.Any(char.IsWhiteSpace))
            throw new ValidationException("Tên Riot ID dài 1–100 ký tự, tag dài 1–20 ký tự và không chứa khoảng trắng.");
        return $"{name}#{tag}";
    }

    private static void ValidateId(int id, string name)
    {
        if (id <= 0)
            throw new ValidationException($"{name} phải là số nguyên dương.");
    }
}
