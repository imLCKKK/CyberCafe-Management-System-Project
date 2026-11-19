using System.Data;
using System.Globalization;
using System.Text.Json;
using CyberManagement.Application.DTOs.Tournaments;
using CyberManagement.Application.Exceptions;
using CyberManagement.Application.Interfaces.Services;
using CyberManagement.Application.Services;
using CyberManagement.Infrastructure.Persistance;
using Microsoft.EntityFrameworkCore;

namespace CyberManagement.Infrastructure.TeamBalancing;

public sealed class TeamPreviewRosterReader(ApplicationDbContext database) : ITeamPreviewRosterReader
{
    public async Task<TeamPreviewRoster> ReadAsync(int tournamentId, string strategy, string expectedVersion, CancellationToken token)
    {
        await using var transaction = database.Database.IsRelational()
            ? await database.Database.BeginTransactionAsync(IsolationLevel.Serializable, token) : null;
        var t = await database.Tournaments.AsNoTracking().Where(t => t.TournamentId == tournamentId)
            .Select(t => new { t.GameId, t.Game.GameName, t.Game.TeamSize, t.Status, t.EntryFee, t.RegistrationEnd,
                t.Version, HasTeams = t.Teams.Any() }).SingleOrDefaultAsync(token)
            ?? throw new TeamBalanceException("TournamentNotFound", "Không tìm thấy giải đấu.");
        // Current schema has no confirmed PlanSnapshot/source metadata to replay.
        if (t.HasTeams) throw new TeamBalanceException("TeamsAlreadyFormed", "Giải đã có đội. Chưa có snapshot đã chốt để trả lại.");
        if (Convert.ToBase64String(t.Version) != expectedVersion)
            throw new TeamBalanceException("VersionConflict", "Phiên bản giải đã đổi. Hãy tải lại giải.");
        if (t.Status != "RegistrationClosed") throw new TeamBalanceException("TournamentLocked", "Cần đóng đăng ký trước khi chia đội.");
        if (!string.Equals(t.GameName.Trim(), "valorant", StringComparison.OrdinalIgnoreCase) || t.TeamSize != 5)
            throw new TeamBalanceException("UNSUPPORTED_GAME_OR_SIZE", "Cần giải VALORANT với đội 5 người.");
        var rows = await database.TournamentRegistrations.AsNoTracking()
            .Where(r => r.TournamentId == tournamentId && r.Status == "Registered").OrderBy(r => r.RegistrationId)
            .Select(r => new { r.RegistrationId, r.CustomerId, r.Customer.FullName, r.Customer.Status, r.RiotId, r.PreferredRole,
                r.FeeCharged, r.ChargeTransactionId, Debit = r.ChargeTransaction,
                Refunded = r.ChargeTransactionId != null && database.WalletTransactions.Any(w => w.OriginalDebitId == r.ChargeTransactionId),
                Canonical = r.StatsSnapshots.Where(s => s.RiotId == r.RiotId).OrderByDescending(s => s.FetchedAt)
                    .ThenByDescending(s => s.SnapshotId).Select(s => s.CanonicalJson).FirstOrDefault() }).ToListAsync(token);
        if (rows.Count < 10 || rows.Count % 5 != 0) throw new TeamBalanceException("TeamSizeMismatch", "Không đủ người để chia đều thành các đội 5 người.");
        if (strategy == "ml" && rows.Count != 10) throw new TeamBalanceException("UNSUPPORTED_GAME_OR_SIZE", "Model v1 yêu cầu đúng 10 người.");
        var participants = rows.Select(r =>
        {
            var id = r.RegistrationId.ToString(CultureInfo.InvariantCulture);
            var eligible = t.EntryFee == 0 ? r.FeeCharged.GetValueOrDefault() == 0 && r.ChargeTransactionId == null
                : r.FeeCharged == t.EntryFee && r.Debit is { Kind: "Debit", Type: "TournamentFee", SourceType: "TournamentRegistration" }
                    && r.Debit.CustomerId == r.CustomerId && r.Debit.SourceId == id && r.Debit.Amount == -t.EntryFee && !r.Refunded;
            if (r.Status != "Active" || !eligible) throw new TeamBalanceException("TeamPlanInvalid", "Đăng ký có tài khoản hoặc phí không hợp lệ.");
            OpggSnapshot? snapshot;
            try { snapshot = OpggSnapshot.ForRegistration(r.Canonical, r.RiotId); }
            catch (Exception e) when (e is JsonException or KeyNotFoundException or InvalidOperationException or FormatException)
            { throw new TeamBalanceException("STATS_UNAVAILABLE", "Snapshot OP.GG không hợp lệ."); }
            if (snapshot == null) throw new TeamBalanceException("STATS_UNAVAILABLE", "Thiếu snapshot OP.GG của người đăng ký.");
            return new TeamBalanceParticipant(r.RegistrationId, r.CustomerId, r.Status, "Registered", r.FeeCharged,
                null, eligible, r.RiotId!, r.PreferredRole!, snapshot);
        }).ToArray();
        var input = new TeamBalanceInput(TeamBalanceVersions.Contract, TeamBalanceVersions.Feature, TeamBalanceVersions.Policy,
            strategy, tournamentId, t.GameId, "valorant", t.Status, t.TeamSize, rows.Count / t.TeamSize, false,
            expectedVersion, "", DateTimeOffset.UtcNow, new(t.EntryFee, DateTime.SpecifyKind(t.RegistrationEnd, DateTimeKind.Utc), 0), participants);
        if (transaction != null) await transaction.CommitAsync(token);
        return new(input with { RosterVersion = TeamBalanceValidation.RosterVersion(input) }, rows.ToDictionary(r => r.RegistrationId, r => r.FullName));
    }
}
