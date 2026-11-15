using System.Data;
using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using System.Text.Json;
using CyberManagement.Application.DTOs.Tournaments;
using CyberManagement.Application.Exceptions;
using CyberManagement.Application.Interfaces.Repositories;
using CyberManagement.Application.Wallets;
using CyberManagement.Domain.Entities;
using CyberManagement.Infrastructure.Persistance;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace CyberManagement.Infrastructure.Repositories;

public sealed class TournamentRepository(
    ApplicationDbContext database, IWalletLedgerWriter wallet, ISessionSettlement settlement) : ITournamentRepository
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
        await ProjectTournaments(TournamentEntities(includeDraft)
            .Where(tournament => tournament.TournamentId == tournamentId))
            .SingleOrDefaultAsync(cancellationToken)
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
                AsUtc(registration.RegisteredAt), registration.FeeCharged))
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

    public async Task<CreateTournamentResult> CreateAsync(
        int employeeId, string key, CreateTournamentRequest request, CancellationToken cancellationToken)
    {
        await using var transaction = await BeginTransactionAsync(cancellationToken);
        await RequireActiveEmployeeAsync(employeeId, cancellationToken);
        var fingerprint = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new
        {
            request.GameId, Name = request.Name.Trim(),
            RegistrationStart = AsUtc(request.RegistrationStartUtc),
            RegistrationEnd = AsUtc(request.RegistrationEndUtc),
            StartTime = AsUtc(request.StartTimeUtc), EntryFee = decimal.ToInt64(request.EntryFee)
        })));
        var priorQuery = database.Database.IsSqlServer()
            ? database.Tournaments.FromSqlInterpolated(
                $"SELECT * FROM [Tournaments] WITH (UPDLOCK, HOLDLOCK) WHERE [CreatedBy] = {employeeId} AND [CreateRequestKey] = {key}")
            : database.Tournaments.Where(row => row.CreatedBy == employeeId && row.CreateRequestKey == key);
        var prior = await priorQuery.Include(row => row.Game).SingleOrDefaultAsync(cancellationToken);
        if (prior is not null)
        {
            if (prior.CreateRequestFingerprint != fingerprint)
                throw new BusinessRuleException("IdempotencyConflict: khóa đã được dùng với nội dung khác.");
            var count = await database.TournamentRegistrations.CountAsync(
                row => row.TournamentId == prior.TournamentId && row.Status != "Cancelled", cancellationToken);
            if (transaction is not null) await transaction.CommitAsync(cancellationToken);
            return new CreateTournamentResult(ToDto(prior, prior.Game, count), true);
        }
        if (request.StartTimeUtc <= DateTime.UtcNow)
            throw new ValidationException("Thời gian thi đấu phải ở tương lai.");
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
            Status = "Draft",
            CreateRequestKey = key,
            CreateRequestFingerprint = fingerprint
        };
        database.Tournaments.Add(tournament);
        await database.SaveChangesAsync(cancellationToken);
        var result = ToDto(tournament, game, 0);
        if (transaction is not null)
            await transaction.CommitAsync(cancellationToken);
        return new CreateTournamentResult(result, false);
    }

    public async Task<TournamentDto> UpdateAsync(
        int employeeId, int tournamentId, UpdateTournamentRequest request, CancellationToken cancellationToken)
    {
        await using var transaction = await BeginTransactionAsync(cancellationToken);
        await RequireActiveEmployeeAsync(employeeId, cancellationToken);
        var tournament = await FindTournamentForUpdateAsync(tournamentId, cancellationToken);
        if (!tournament.Version.SequenceEqual(Convert.FromBase64String(request.ExpectedVersion)))
            throw new BusinessRuleException("VersionConflict: giải đấu đã thay đổi. Vui lòng tải lại.");
        if (request.StartTimeUtc <= DateTime.UtcNow)
            throw new ValidationException("Thời gian thi đấu phải ở tương lai.");
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
        try
        {
            await database.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new BusinessRuleException("VersionConflict: giải đấu đã thay đổi. Vui lòng tải lại.");
        }
        var registrationCount = await database.TournamentRegistrations
            .CountAsync(row => row.TournamentId == tournamentId && row.Status != "Cancelled", cancellationToken);
        var result = ToDto(tournament, game, registrationCount);
        if (transaction is not null)
            await transaction.CommitAsync(cancellationToken);
        return result;
    }

    public async Task<TransitionTournamentResult> TransitionAsync(
        int employeeId, int tournamentId, string key, TransitionTournamentRequest request, CancellationToken cancellationToken)
    {
        await using var transaction = await BeginTransactionAsync(cancellationToken);
        await RequireActiveEmployeeAsync(employeeId, cancellationToken);
        var tournament = await FindTournamentForUpdateAsync(tournamentId, cancellationToken);
        var expectedVersion = Convert.FromBase64String(request.ExpectedVersion);
        var canonicalVersion = Convert.ToBase64String(expectedVersion);
        var prior = await database.TournamentTransitions.SingleOrDefaultAsync(
            row => row.TournamentId == tournamentId && row.EmployeeId == employeeId && row.RequestKey == key,
            cancellationToken);
        if (prior is not null)
        {
            if (prior.ToStatus != request.ToStatus || prior.ExpectedVersion != canonicalVersion)
                throw new BusinessRuleException("IdempotencyConflict: khóa đã được dùng với nội dung khác.");
            var replay = JsonSerializer.Deserialize<TournamentDto>(prior.ResponseJson)
                ?? throw new InvalidOperationException("Kết quả chuyển trạng thái đã lưu không hợp lệ.");
            if (transaction is not null) await transaction.CommitAsync(cancellationToken);
            return new TransitionTournamentResult(replay, true);
        }
        if (!tournament.Version.SequenceEqual(expectedVersion))
            throw new BusinessRuleException("VersionConflict: giải đấu đã thay đổi. Vui lòng tải lại.");
        if (request.ToStatus == "RegistrationOpen")
        {
            var canOpen = tournament.Status == "Draft"
                || tournament.Status == "RegistrationClosed" && tournament.RegistrationEnd > DateTime.UtcNow
                    && !await database.Teams.AnyAsync(team => team.TournamentId == tournamentId, cancellationToken);
            if (!canOpen || !tournament.Game.IsActive)
                throw new BusinessRuleException("Không thể mở đăng ký ở trạng thái hiện tại hoặc trò chơi đã ngừng hoạt động.");
            tournament.Status = "RegistrationOpen";
        }
        else if (request.ToStatus == "RegistrationClosed" && tournament.Status == "RegistrationOpen")
        {
            tournament.Status = "RegistrationClosed";
        }
        else
        {
            throw new BusinessRuleException("Trạng thái giải đấu không thể chuyển theo yêu cầu.");
        }

        try
        {
            await database.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new BusinessRuleException("VersionConflict: giải đấu đã thay đổi. Vui lòng tải lại.");
        }
        var result = ToDto(tournament, tournament.Game,
            await database.TournamentRegistrations.CountAsync(
                row => row.TournamentId == tournamentId && row.Status != "Cancelled", cancellationToken));
        database.TournamentTransitions.Add(new TournamentTransition
        {
            TournamentId = tournamentId, EmployeeId = employeeId, RequestKey = key,
            ToStatus = request.ToStatus, ExpectedVersion = canonicalVersion,
            ResponseJson = JsonSerializer.Serialize(result)
        });
        await database.SaveChangesAsync(cancellationToken);
        if (transaction is not null)
            await transaction.CommitAsync(cancellationToken);
        return new TransitionTournamentResult(result, false);
    }

    public async Task<TournamentRegistrationDto> RegisterAsync(
        int tournamentId, int customerId, TournamentRegistrationRequest request, CancellationToken cancellationToken)
    {
        var quotedFee = await database.Tournaments.AsNoTracking().Where(row => row.TournamentId == tournamentId)
            .Select(row => (decimal?)row.EntryFee).SingleOrDefaultAsync(cancellationToken)
            ?? throw new EntityNotFoundException("Không tìm thấy giải đấu.");
        await using var transaction = await BeginTransactionAsync(cancellationToken);
        var customer = database.Database.IsSqlServer()
            ? await wallet.LockCustomerAsync(customerId, cancellationToken)
            : await database.Customers.SingleOrDefaultAsync(item => item.CustomerId == customerId, cancellationToken)
                ?? throw new EntityNotFoundException("Không tìm thấy khách hàng.");
        var now = DateTime.UtcNow;
        if (customer.Status != "Active")
            throw new BusinessRuleException("Tài khoản khách hàng hiện không hoạt động.");
        if (quotedFee > 0)
        {
            if (!database.Database.IsSqlServer())
                throw new InvalidOperationException("Paid tournament registration requires a SQL Server transaction.");
            await settlement.BeforeWalletDebitAsync(customerId, now, cancellationToken);
        }
        var tournament = await FindTournamentForUpdateAsync(tournamentId, cancellationToken);
        if (tournament.EntryFee != quotedFee)
            throw new BusinessRuleException("Lệ phí giải đã thay đổi. Vui lòng tải lại và thử đăng ký.");
        if (tournament.EntryFee > 0 && request.ExpectedVersion is null
            || request.ExpectedVersion is not null
                && !tournament.Version.SequenceEqual(Convert.FromBase64String(request.ExpectedVersion)))
            throw new BusinessRuleException("Thông tin giải đã thay đổi. Vui lòng tải lại trước khi đăng ký.");
        if (tournament.Status != "RegistrationOpen"
            || now < tournament.RegistrationStart || now >= tournament.RegistrationEnd)
            throw new BusinessRuleException("Giải đấu hiện không nhận đăng ký.");
        if (!tournament.Game.IsActive)
            throw new BusinessRuleException("Trò chơi của giải đấu hiện không hoạt động.");
        if (tournament.EntryFee < 0 || tournament.EntryFee > 999999999
            || decimal.Truncate(tournament.EntryFee) != tournament.EntryFee)
            throw new BusinessRuleException("Lệ phí giải không hợp lệ; cần đối soát trước khi đăng ký.");
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
            RegisteredAt = now,
            FeeCharged = tournament.EntryFee
        };
        database.TournamentRegistrations.Add(registration);
        var result = new TournamentRegistrationDto(
            registration.RegistrationId, tournamentId, customerId, customer.FullName, registration.RiotId,
            rank.RankId, rank.RankName, registration.PreferredRole, registration.WinRate,
            registration.HoursPlayed, registration.SkillScore, registration.Status, AsUtc(registration.RegisteredAt),
            registration.FeeCharged);
        try
        {
            await database.SaveChangesAsync(cancellationToken);
            if (tournament.EntryFee > 0)
            {
                var source = registration.RegistrationId.ToString(System.Globalization.CultureInfo.InvariantCulture);
                var debit = await wallet.RecordAsync(new WalletMutation(customerId, WalletMutationKind.Debit,
                    tournament.EntryFee, "TournamentFee", "TournamentRegistration", source,
                    $"TournamentFee:{source}", new WalletActor("System"), now,
                    Description: $"Đăng ký giải đấu #{tournamentId}"), cancellationToken);
                registration.ChargeTransaction = debit.Entry;
                await database.SaveChangesAsync(cancellationToken);
            }
        }
        catch (DbUpdateException exception) when (exception.InnerException is SqlException { Number: 2601 or 2627 } sql
            && sql.Message.Contains("IX_TournamentRegistrations_TournamentId_CustomerId", StringComparison.Ordinal))
        {
            throw new BusinessRuleException("Khách hàng đã đăng ký giải đấu này.");
        }

        if (transaction is not null)
            await transaction.CommitAsync(cancellationToken);
        return result with { Id = registration.RegistrationId };
    }

    private IQueryable<TournamentDto> TournamentQuery(bool includeDrafts) =>
        ProjectTournaments(TournamentEntities(includeDrafts));

    private IQueryable<Tournament> TournamentEntities(bool includeDrafts) =>
        database.Tournaments.AsNoTracking()
            .Where(tournament => includeDrafts || tournament.Status != "Draft")
            .OrderBy(tournament => tournament.StartTime).ThenBy(tournament => tournament.TournamentId);

    private static IQueryable<TournamentDto> ProjectTournaments(IQueryable<Tournament> tournaments) =>
        tournaments
            .Select(tournament => new TournamentDto(
                tournament.TournamentId, tournament.GameId, tournament.Game.GameName, tournament.Game.TeamSize,
                tournament.TournamentName, AsUtc(tournament.RegistrationStart), AsUtc(tournament.RegistrationEnd),
                AsUtc(tournament.StartTime), tournament.EntryFee,
                tournament.Registrations.Count(registration => registration.Status != "Cancelled"),
                tournament.Status, tournament.CreatedBy, Convert.ToBase64String(tournament.Version)));

    public async Task RequireActiveEmployeeAsync(int employeeId, CancellationToken cancellationToken)
    {
        if (!await database.Employees.AnyAsync(
            employee => employee.EmployeeId == employeeId && employee.IsActive, cancellationToken))
            throw new UnauthorizedAccessException("Tài khoản nhân viên hiện không hoạt động.");
    }

    private async Task<Tournament> FindTournamentAsync(int tournamentId, CancellationToken cancellationToken) =>
        await database.Tournaments.Include(item => item.Game)
            .SingleOrDefaultAsync(item => item.TournamentId == tournamentId, cancellationToken)
            ?? throw new EntityNotFoundException("Không tìm thấy giải đấu.");

    private async Task<Tournament> FindTournamentForUpdateAsync(int tournamentId, CancellationToken cancellationToken)
    {
        var query = database.Database.IsSqlServer()
            ? database.Tournaments.FromSqlInterpolated(
                $"SELECT * FROM [Tournaments] WITH (UPDLOCK, HOLDLOCK) WHERE [TournamentId] = {tournamentId}")
            : database.Tournaments.Where(item => item.TournamentId == tournamentId);
        return await query.Include(item => item.Game).SingleOrDefaultAsync(cancellationToken)
            ?? throw new EntityNotFoundException("Không tìm thấy giải đấu.");
    }

    private async Task<IDbContextTransaction?> BeginTransactionAsync(CancellationToken cancellationToken) =>
        database.Database.IsRelational()
            ? await database.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken)
            : null;

    private static DateTime AsUtc(DateTime value) => DateTime.SpecifyKind(value, DateTimeKind.Utc);

    private static TournamentDto ToDto(Tournament tournament, Game game, int registeredCount) =>
        new(tournament.TournamentId, tournament.GameId, game.GameName, game.TeamSize,
            tournament.TournamentName, AsUtc(tournament.RegistrationStart), AsUtc(tournament.RegistrationEnd),
            AsUtc(tournament.StartTime), tournament.EntryFee, registeredCount,
            tournament.Status, tournament.CreatedBy, Convert.ToBase64String(tournament.Version));
}
