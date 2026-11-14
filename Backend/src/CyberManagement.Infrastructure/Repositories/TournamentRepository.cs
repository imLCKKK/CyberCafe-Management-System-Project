using System.Data;
using CyberManagement.Application.DTOs.Tournaments;
using CyberManagement.Application.Exceptions;
using CyberManagement.Application.Interfaces.Repositories;
using CyberManagement.Domain.Entities;
using CyberManagement.Infrastructure.Persistance;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace CyberManagement.Infrastructure.Repositories;

public sealed class TournamentRepository(ApplicationDbContext database) : ITournamentRepository
{
    public async Task<IReadOnlyList<GameDto>> GetGamesAsync(bool includeInactive, CancellationToken cancellationToken) =>
        await database.Games.AsNoTracking()
            .Where(game => includeInactive || game.IsActive)
            .OrderBy(game => game.GameName).ThenBy(game => game.GameId)
            .Select(game => new GameDto(game.GameId, game.GameName, game.TeamSize, game.IsActive))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<GameRankDto>> GetRanksAsync(int gameId, CancellationToken cancellationToken)
    {
        if (!await database.Games.AnyAsync(game => game.GameId == gameId, cancellationToken))
            throw new EntityNotFoundException("Không tìm thấy trò chơi.");
        return await database.GameRanks.AsNoTracking()
            .Where(rank => rank.GameId == gameId)
            .OrderBy(rank => rank.RankOrder).ThenBy(rank => rank.RankId)
            .Select(rank => new GameRankDto(rank.RankId, rank.GameId, rank.RankName, rank.RankOrder))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<TournamentDto>> GetTournamentsAsync(
        bool includeDrafts, CancellationToken cancellationToken) =>
        await TournamentQuery(includeDrafts).ToListAsync(cancellationToken);

    public async Task<TournamentDto> GetTournamentAsync(
        int tournamentId, bool includeDraft, CancellationToken cancellationToken) =>
        await TournamentQuery(includeDraft)
            .SingleOrDefaultAsync(tournament => tournament.Id == tournamentId, cancellationToken)
            ?? throw new EntityNotFoundException("Không tìm thấy giải đấu.");

    public async Task<IReadOnlyList<TournamentRegistrationDto>> GetAdminRegistrationsAsync(
        int tournamentId, CancellationToken cancellationToken) =>
        await database.TournamentRegistrations.AsNoTracking()
            .Where(registration => registration.TournamentId == tournamentId && registration.Status != "Cancelled")
            .OrderBy(registration => registration.RegisteredAt).ThenBy(registration => registration.RegistrationId)
            .Select(registration => new TournamentRegistrationDto(
                registration.RegistrationId, registration.TournamentId, registration.CustomerId,
                registration.Customer.FullName, registration.RiotId, registration.RankId,
                registration.Rank.RankName, registration.PreferredRole, registration.WinRate,
                registration.HoursPlayed, registration.SkillScore, registration.Status,
                AsUtc(registration.RegisteredAt)))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<ClientTournamentRegistrationDto>> GetClientRegistrationsAsync(
        int tournamentId, int customerId, CancellationToken cancellationToken) =>
        await database.TournamentRegistrations.AsNoTracking()
            .Where(registration => registration.TournamentId == tournamentId && registration.Status != "Cancelled")
            .OrderBy(registration => registration.RegisteredAt).ThenBy(registration => registration.RegistrationId)
            .Select(registration => new ClientTournamentRegistrationDto(
                registration.RegistrationId, registration.Customer.FullName, registration.RiotId,
                registration.Rank.RankName, registration.PreferredRole, registration.WinRate,
                registration.HoursPlayed, registration.Status, AsUtc(registration.RegisteredAt),
                registration.CustomerId == customerId))
            .ToListAsync(cancellationToken);

    public async Task<TournamentDto> CreateAsync(
        int employeeId, CreateTournamentRequest request, CancellationToken cancellationToken)
    {
        await using var transaction = await BeginTransactionAsync(cancellationToken);
        var game = await database.Games.SingleOrDefaultAsync(
            item => item.GameId == request.GameId && item.IsActive, cancellationToken)
            ?? throw new BusinessRuleException("Chỉ có thể tạo giải cho trò chơi đang hoạt động.");
        var tournament = new Tournament
        {
            GameId = game.GameId,
            CreatedBy = employeeId,
            TournamentName = request.Name.Trim(),
            RegistrationStart = AsUtc(request.RegistrationStartUtc),
            RegistrationEnd = AsUtc(request.RegistrationEndUtc),
            StartTime = AsUtc(request.StartTimeUtc),
            EntryFee = request.EntryFee,
            Status = "Draft"
        };
        database.Tournaments.Add(tournament);
        await database.SaveChangesAsync(cancellationToken);
        var result = ToDto(tournament, game, 0);
        if (transaction is not null)
            await transaction.CommitAsync(cancellationToken);
        return result;
    }

    public async Task<TournamentDto> UpdateAsync(
        int tournamentId, UpdateTournamentRequest request, CancellationToken cancellationToken)
    {
        await using var transaction = await BeginTransactionAsync(cancellationToken);
        var tournament = await FindTournamentAsync(tournamentId, cancellationToken);
        if (tournament.Status is not ("Draft" or "RegistrationOpen" or "RegistrationClosed"))
            throw new BusinessRuleException("Giải đấu không còn cho phép chỉnh sửa.");
        var hasRegistrations = await database.TournamentRegistrations
            .AnyAsync(row => row.TournamentId == tournamentId, cancellationToken);
        if (hasRegistrations && (tournament.GameId != request.GameId || tournament.EntryFee != request.EntryFee))
            throw new BusinessRuleException("Không thể đổi trò chơi hoặc lệ phí sau khi đã có đăng ký.");
        var game = await database.Games.SingleOrDefaultAsync(
            item => item.GameId == request.GameId && item.IsActive, cancellationToken)
            ?? throw new BusinessRuleException("Chỉ có thể chọn trò chơi đang hoạt động.");

        tournament.GameId = game.GameId;
        tournament.TournamentName = request.Name.Trim();
        tournament.RegistrationStart = AsUtc(request.RegistrationStartUtc);
        tournament.RegistrationEnd = AsUtc(request.RegistrationEndUtc);
        tournament.StartTime = AsUtc(request.StartTimeUtc);
        tournament.EntryFee = request.EntryFee;
        await database.SaveChangesAsync(cancellationToken);
        var registrationCount = await database.TournamentRegistrations
            .CountAsync(row => row.TournamentId == tournamentId && row.Status != "Cancelled", cancellationToken);
        var result = ToDto(tournament, game, registrationCount);
        if (transaction is not null)
            await transaction.CommitAsync(cancellationToken);
        return result;
    }

    public async Task<TournamentDto> TransitionAsync(
        int tournamentId, string status, CancellationToken cancellationToken)
    {
        await using var transaction = await BeginTransactionAsync(cancellationToken);
        var tournament = await FindTournamentAsync(tournamentId, cancellationToken);
        if (status == "RegistrationOpen")
        {
            var canOpen = tournament.Status == "Draft"
                || tournament.Status == "RegistrationClosed" && tournament.RegistrationEnd > DateTime.UtcNow
                    && !await database.Teams.AnyAsync(team => team.TournamentId == tournamentId, cancellationToken);
            if (!canOpen || !tournament.Game.IsActive)
                throw new BusinessRuleException("Không thể mở đăng ký ở trạng thái hiện tại hoặc trò chơi đã ngừng hoạt động.");
            tournament.Status = "RegistrationOpen";
        }
        else if (status == "RegistrationClosed" && tournament.Status == "RegistrationOpen")
        {
            tournament.Status = "RegistrationClosed";
        }
        else
        {
            throw new BusinessRuleException("Trạng thái giải đấu không thể chuyển theo yêu cầu.");
        }

        await database.SaveChangesAsync(cancellationToken);
        var result = ToDto(tournament, tournament.Game,
            await database.TournamentRegistrations.CountAsync(
                row => row.TournamentId == tournamentId && row.Status != "Cancelled", cancellationToken));
        if (transaction is not null)
            await transaction.CommitAsync(cancellationToken);
        return result;
    }

    public async Task<TournamentRegistrationDto> RegisterAsync(
        int tournamentId, int customerId, TournamentRegistrationRequest request, CancellationToken cancellationToken)
    {
        await using var transaction = await BeginTransactionAsync(cancellationToken);
        var tournament = await FindTournamentAsync(tournamentId, cancellationToken);
        var now = DateTime.UtcNow;
        if (tournament.Status != "RegistrationOpen"
            || now < tournament.RegistrationStart || now >= tournament.RegistrationEnd)
            throw new BusinessRuleException("Giải đấu hiện không nhận đăng ký.");
        if (!tournament.Game.IsActive)
            throw new BusinessRuleException("Trò chơi của giải đấu hiện không hoạt động.");
        if (tournament.EntryFee != 0)
            throw new BusinessRuleException("Chưa thể đăng ký giải có phí; hiện chỉ hỗ trợ đăng ký miễn phí.");

        var customer = await database.Customers.SingleOrDefaultAsync(
            item => item.CustomerId == customerId, cancellationToken)
            ?? throw new EntityNotFoundException("Không tìm thấy khách hàng.");
        if (customer.Status != "Active")
            throw new BusinessRuleException("Tài khoản khách hàng hiện không hoạt động.");
        if (await database.TournamentRegistrations
            .AnyAsync(row => row.TournamentId == tournamentId && row.CustomerId == customerId, cancellationToken))
            throw new BusinessRuleException("Khách hàng đã đăng ký giải đấu này.");
        if (!await database.GameRanks.AnyAsync(
            rank => rank.RankId == request.RankId && rank.GameId == tournament.GameId, cancellationToken))
            throw new BusinessRuleException("Hạng đã chọn không thuộc trò chơi của giải đấu.");

        var rank = await database.GameRanks.SingleAsync(
            item => item.RankId == request.RankId, cancellationToken);
        var maxRankOrder = await database.GameRanks.Where(item => item.GameId == tournament.GameId)
            .Select(item => (int?)item.RankOrder).MaxAsync(cancellationToken) ?? 0;
        var winRate = request.WinRate ?? 0;
        var hoursPlayed = request.HoursPlayed ?? 0;
        var skillScore = maxRankOrder <= 0 ? 0 : decimal.Round(100m * (
            0.5m * rank.RankOrder / maxRankOrder
            + 0.3m * winRate / 100m
            + 0.2m * Math.Min(hoursPlayed, 2000) / 2000m), 2, MidpointRounding.AwayFromZero);
        var registration = new TournamentRegistration
        {
            TournamentId = tournamentId,
            CustomerId = customerId,
            RankId = rank.RankId,
            RiotId = request.RiotId,
            PreferredRole = request.PreferredRole,
            WinRate = winRate,
            HoursPlayed = hoursPlayed,
            SkillScore = skillScore,
            Status = "Registered",
            RegisteredAt = now
        };
        database.TournamentRegistrations.Add(registration);
        var result = new TournamentRegistrationDto(
            registration.RegistrationId, tournamentId, customerId, customer.FullName, registration.RiotId,
            rank.RankId, rank.RankName, registration.PreferredRole, registration.WinRate,
            registration.HoursPlayed, registration.SkillScore, registration.Status, AsUtc(registration.RegisteredAt));
        try
        {
            await database.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (exception.InnerException is SqlException { Number: 2601 or 2627 })
        {
            throw new BusinessRuleException("Khách hàng đã đăng ký giải đấu này.");
        }

        if (transaction is not null)
            await transaction.CommitAsync(cancellationToken);
        return result with { Id = registration.RegistrationId };
    }

    private IQueryable<TournamentDto> TournamentQuery(bool includeDrafts) =>
        database.Tournaments.AsNoTracking()
            .Where(tournament => includeDrafts || tournament.Status != "Draft")
            .OrderBy(tournament => tournament.StartTime).ThenBy(tournament => tournament.TournamentId)
            .Select(tournament => new TournamentDto(
                tournament.TournamentId, tournament.GameId, tournament.Game.GameName, tournament.Game.TeamSize,
                tournament.TournamentName, AsUtc(tournament.RegistrationStart), AsUtc(tournament.RegistrationEnd),
                AsUtc(tournament.StartTime), tournament.EntryFee,
                tournament.Registrations.Count(registration => registration.Status != "Cancelled"),
                tournament.Status, tournament.CreatedBy));

    private async Task<Tournament> FindTournamentAsync(int tournamentId, CancellationToken cancellationToken) =>
        await database.Tournaments.Include(item => item.Game)
            .SingleOrDefaultAsync(item => item.TournamentId == tournamentId, cancellationToken)
            ?? throw new EntityNotFoundException("Không tìm thấy giải đấu.");

    private async Task<IDbContextTransaction?> BeginTransactionAsync(CancellationToken cancellationToken) =>
        database.Database.IsRelational()
            ? await database.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken)
            : null;

    private static DateTime AsUtc(DateTime value) => DateTime.SpecifyKind(value, DateTimeKind.Utc);

    private static TournamentDto ToDto(Tournament tournament, Game game, int registeredCount) =>
        new(tournament.TournamentId, tournament.GameId, game.GameName, game.TeamSize,
            tournament.TournamentName, AsUtc(tournament.RegistrationStart), AsUtc(tournament.RegistrationEnd),
            AsUtc(tournament.StartTime), tournament.EntryFee, registeredCount,
            tournament.Status, tournament.CreatedBy);
}
