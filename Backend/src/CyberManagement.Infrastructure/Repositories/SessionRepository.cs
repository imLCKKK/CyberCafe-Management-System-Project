using System.Globalization;
using CyberManagement.Application.DTOs.Computers;
using CyberManagement.Application.DTOs.Invoices;
using CyberManagement.Application.Exceptions;
using CyberManagement.Application.Interfaces.Repositories;
using CyberManagement.Application.Interfaces.Services;
using CyberManagement.Application.Wallets;
using CyberManagement.Domain.Entities;
using CyberManagement.Infrastructure.Persistance;
using Microsoft.EntityFrameworkCore;

namespace CyberManagement.Infrastructure.Repositories;

public sealed class SessionRepository(ApplicationDbContext db, IWalletLedgerWriter wallet,
    IInvoiceService invoices, ISessionInvoiceIssuer systemInvoices, SessionCommitEvents events) : ISessionRepository
{
    private IQueryable<GamingSession> ReadQuery => db.GamingSessions.AsNoTracking()
        .Include(x => x.Computer).ThenInclude(x => x.ComputerType);

    public async Task<IReadOnlyList<SessionDto>> GetSessionsAsync(CancellationToken ct) =>
        (await ReadQuery.Where(x => x.Status == "Active" || x.EndTime == null)
            .OrderByDescending(x => x.GamingSessionId).ToListAsync(ct)).Select(ToDto).ToArray();

    public Task<SessionHomeDto> GetHomeAsync(int customerId, CancellationToken ct) => SqlWork.RunAsync(db, async () =>
    {
        var customer = db.Database.IsSqlServer() ? await wallet.LockCustomerAsync(customerId, ct)
            : await db.Customers.AsNoTracking().SingleOrDefaultAsync(x => x.CustomerId == customerId, ct)
                ?? throw new EntityNotFoundException("Không tìm thấy khách hàng.");
        var sessions = await ReadQuery.Where(x => x.CustomerId == customerId && (x.Status == "Active" || x.EndTime == null)).ToListAsync(ct);
        if (sessions.Count > 1) throw new BusinessRuleException("Khách có nhiều phiên chưa đóng, cần đối soát.");
        if (sessions.Count == 1 && (sessions[0].Status != "Active" || sessions[0].EndTime is not null
            || sessions[0].PricePerHourSnapshot is not > 0 || sessions[0].Computer.Status != "Occupied"))
            throw new BusinessRuleException("Phiên hoặc trạng thái máy không nhất quán, cần liên hệ nhân viên đối soát.");
        return new SessionHomeDto(customer.WalletBalance, sessions.Count == 0 ? null : ToDto(sessions[0]));
    }, ct);

    public Task<SessionDto> OpenAsync(OpenSessionCommand command, int employeeId, string key, CancellationToken ct) =>
        SqlWork.RunAsync(db, async () =>
        {
            await AuthorizeAsync(employeeId, "session.start", ct);
            var operation = $"Open:{employeeId}:{key}";
            await LockKeyAsync(operation, ct);
            var replay = await ReadQuery.SingleOrDefaultAsync(x => x.OpenOperationKey == operation, ct);
            if (replay is not null)
            {
                if (replay.CustomerId != command.CustomerId || replay.ComputerId != command.ComputerId)
                    throw new BusinessRuleException("Khóa mở phiên đã dùng cho nội dung khác.");
                return ToDto(replay);
            }
            var customer = await wallet.LockCustomerAsync(command.CustomerId, ct);
            var computer = await LockComputerAsync(command.ComputerId, ct);
            if (customer.Status != "Active" || computer.Status != "Available")
                throw new BusinessRuleException("Khách phải hoạt động và máy phải đang trống.");
            if (await db.GamingSessions.AnyAsync(x => (x.ComputerId == command.ComputerId || x.CustomerId == command.CustomerId)
                && (x.Status == "Active" || x.EndTime == null), ct))
                throw new BusinessRuleException("Máy hoặc khách đã có phiên chưa đóng.");
            if (await db.ComputerMaintenances.AnyAsync(x => x.ComputerId == command.ComputerId && x.EndTime == null, ct))
                throw new BusinessRuleException("Máy còn hồ sơ bảo trì chưa kết thúc.");
            var type = await db.ComputerTypes.AsNoTracking().SingleAsync(x => x.ComputerTypeId == computer.ComputerTypeId, ct);
            var price = type.PricePerHour;
            if (price is <= 0 or > 999999999 || price != decimal.Truncate(price))
                throw new BusinessRuleException("Giá giờ không hợp lệ.");
            if (decimal.Floor(customer.WalletBalance) < decimal.Ceiling(price / 3600m))
                throw new BusinessRuleException("Ví không đủ tài trợ một giây sử dụng.");
            var session = new GamingSession { ComputerId = command.ComputerId, CustomerId = command.CustomerId,
                StartTime = DateTime.UtcNow, PricePerHourSnapshot = price, HourlyRateSnapshot = price, OpenedByEmployeeId = employeeId,
                OpenOperationKey = operation, Status = "Active" };
            db.GamingSessions.Add(session);
            computer.Status = "Occupied";
            await db.SaveChangesAsync(ct);
            Queue(session);
            return ToDto(await ReadQuery.SingleAsync(x => x.GamingSessionId == session.GamingSessionId, ct));
        }, ct);

    public Task<SessionDto> CloseAsync(int sessionId, int employeeId, string key, CancellationToken ct) =>
        SqlWork.RunAsync(db, async () =>
        {
            await AuthorizeAsync(employeeId, "session.end", ct);
            await AuthorizeAsync(employeeId, "invoice.create", ct);
            var operation = $"Close:{employeeId}:{key}";
            await LockKeyAsync(operation, ct);
            if (await db.GamingSessions.AnyAsync(x => x.CloseOperationKey == operation && x.GamingSessionId != sessionId, ct))
                throw new BusinessRuleException("Khóa đóng phiên đã dùng cho phiên khác.");
            var identity = await db.GamingSessions.AsNoTracking().SingleOrDefaultAsync(x => x.GamingSessionId == sessionId, ct)
                ?? throw new EntityNotFoundException("Không tìm thấy phiên.");
            await wallet.LockCustomerAsync(identity.CustomerId, ct);
            var computer = await LockComputerAsync(identity.ComputerId, ct);
            var session = await LockSessionAsync(sessionId, ct);
            if (session.Status == "Ended" && session.EndTime is not null)
                return ToDto(await ReadQuery.SingleAsync(x => x.GamingSessionId == sessionId, ct));
            await SettleLockedAsync(session, computer, DateTime.UtcNow, true, employeeId, operation, ct);
            return ToDto(await ReadQuery.SingleAsync(x => x.GamingSessionId == sessionId, ct));
        }, ct);

    public async Task<IReadOnlyList<int>> GetActiveCustomersAsync(CancellationToken ct) =>
        await db.GamingSessions.AsNoTracking().Where(x => x.Status == "Active" || x.EndTime == null)
            .Select(x => x.CustomerId).Distinct().ToListAsync(ct);

    public Task BillAsync(int customerId, CancellationToken ct) => SqlWork.RunAsync(db, async () =>
    { await SettleInTransactionAsync(customerId, DateTime.UtcNow, ct); return true; }, ct);

    public async Task SettleInTransactionAsync(int customerId, DateTime nowUtc, CancellationToken ct)
    {
        if (nowUtc.Kind != DateTimeKind.Utc) throw new ArgumentException("Thời điểm quyết toán phải là UTC.");
        await wallet.LockCustomerAsync(customerId, ct);
        var candidates = await db.GamingSessions.AsNoTracking()
            .Where(x => x.CustomerId == customerId && (x.Status == "Active" || x.EndTime == null))
            .Select(x => new { x.GamingSessionId, x.ComputerId }).ToListAsync(ct);
        if (candidates.Count == 0) return;
        if (candidates.Count != 1) throw new BusinessRuleException("Khách có nhiều phiên chưa đóng, cần đối soát.");
        var candidate = candidates[0];
        var computer = await LockComputerAsync(candidate.ComputerId, ct);
        var session = await LockSessionAsync(candidate.GamingSessionId, ct);
        await SettleLockedAsync(session, computer, nowUtc, false, null, null, ct);
    }

    private async Task SettleLockedAsync(GamingSession session, Computer computer, DateTime nowUtc,
        bool close, int? employeeId, string? closeKey, CancellationToken ct)
    {
        var customer = await wallet.LockCustomerAsync(session.CustomerId, ct);
        var price = session.PricePerHourSnapshot;
        var collected = await db.SessionCharges.Where(x => x.GamingSessionId == session.GamingSessionId)
            .SumAsync(x => (decimal?)x.Amount, ct) ?? 0;
        if (customer.Status != "Active" || customer.WalletBalance < 0
            || customer.WalletBalance != decimal.Truncate(customer.WalletBalance)
            || session.Status != "Active" || session.EndTime is not null
            || computer.Status != "Occupied" || price is not > 0 or > 999999999
            || price != decimal.Truncate(price.Value) || session.StartTime > nowUtc
            || collected < 0 || collected != decimal.Truncate(collected) || collected != session.TotalCost
            || (collected > 0 && session.LastChargedAt is null)
            || session.LastChargedAt < session.StartTime || session.LastChargedAt > nowUtc)
            throw new BusinessRuleException("Dữ liệu phiên hoặc phí không nhất quán, cần đối soát.");

        var elapsed = (nowUtc.Ticks - session.StartTime.Ticks) / TimeSpan.TicksPerSecond;
        if (session.LastChargedAt is DateTime last
            && (last.Ticks - session.StartTime.Ticks) % TimeSpan.TicksPerSecond != 0)
            throw new BusinessRuleException("Mốc phí phiên không nằm ở giây hoàn tất.");
        var previouslySettledSeconds = ((session.LastChargedAt ?? session.StartTime).Ticks - session.StartTime.Ticks) / TimeSpan.TicksPerSecond;
        if (collected != decimal.Ceiling(price.Value * previouslySettledSeconds / 3600m))
            throw new BusinessRuleException("Tổng phí không khớp mốc đã quyết toán, cần đối soát.");
        var affordable = decimal.Floor((collected + decimal.Floor(customer.WalletBalance)) * 3600m / price.Value);
        var seconds = (long)Math.Min(elapsed, affordable);
        var through = session.StartTime.AddSeconds(seconds);
        var total = decimal.Ceiling(price.Value * seconds / 3600m);
        if (total < collected || session.LastChargedAt > through)
            throw new BusinessRuleException("Mốc thu phí vượt giới hạn hoặc không khớp sổ.");
        var exhausted = elapsed >= affordable;
        const decimal maximumCharge = 999_999_999m; // Wallet writer limit per debit.
        var chargeFrom = session.LastChargedAt ?? session.StartTime;
        while (collected < total)
        {
            // Find the last whole second that fits this debit's cumulative ceiling.
            var chargeSeconds = total - collected <= maximumCharge ? seconds
                : (long)decimal.Floor((collected + maximumCharge) * 3600m / price.Value);
            var chargeThrough = session.StartTime.AddSeconds(chargeSeconds);
            var chargeTotal = decimal.Ceiling(price.Value * chargeSeconds / 3600m);
            var amount = chargeTotal - collected;
            var charge = new SessionCharge { GamingSessionId = session.GamingSessionId,
                StartTime = chargeFrom, EndTime = chargeThrough, Amount = amount };
            db.SessionCharges.Add(charge);
            await db.SaveChangesAsync(ct);
            var operation = $"SessionCharge:{session.GamingSessionId}:{chargeThrough.Ticks}";
            var debit = await wallet.RecordAsync(new WalletMutation(session.CustomerId, WalletMutationKind.Debit,
                amount, "GamingCharge", "SessionCharge", charge.ChargeId.ToString(CultureInfo.InvariantCulture),
                operation, new WalletActor("System"), nowUtc), ct);
            charge.OperationKey = operation;
            charge.WalletTransaction = debit.Entry;
            db.Payments.Add(new Payment { SessionCharge = charge, WalletTransaction = debit.Entry,
                Amount = amount, Method = "Wallet", Status = "Succeeded", OperationKey = operation,
                CreatedAt = nowUtc, ConfirmedAt = nowUtc, PaymentTime = nowUtc, PaymentTimeBasis = "Confirmation" });
            collected = chargeTotal;
            chargeFrom = chargeThrough;
        }
        session.TotalCost = total;
        session.LastChargedAt = through;
        if (close || exhausted)
        {
            session.EndTime = exhausted ? through : nowUtc;
            session.Status = "Ended";
            session.ClosedByActorType = exhausted ? "System" : "Staff";
            session.ClosedByEmployeeId = exhausted ? null : employeeId;
            session.CloseReason = exhausted ? "InsufficientBalance" : "StaffClosed";
            session.CloseOperationKey = exhausted ? $"SessionAutoClose:{session.GamingSessionId}" : closeKey;
            computer.Status = "Available";
            await db.SaveChangesAsync(ct);
            if (exhausted)
                session.ClosingInvoiceId = (await systemInvoices.IssueForAutoClosedSessionAsync(session.GamingSessionId, ct))?.Id;
            else if (await db.SessionCharges.AnyAsync(x => x.GamingSessionId == session.GamingSessionId
                && !db.InvoiceDetails.Any(line => line.SessionChargeId == x.ChargeId), ct))
                session.ClosingInvoiceId = (await invoices.IssueAsync(new IssueInvoiceRequest { Kind = "Session",
                    GamingSessionId = session.GamingSessionId, OrderIds = [] }, employeeId!.Value,
                    $"SessionCloseInvoice:{session.GamingSessionId}", ct)).Id;
        }
        await db.SaveChangesAsync(ct);
        Queue(session);
    }

    private async Task AuthorizeAsync(int employeeId, string permission, CancellationToken ct)
    {
        if (!await db.Employees.AnyAsync(x => x.EmployeeId == employeeId && x.IsActive
            && x.Role.RolePermissions.Any(p => p.Permission.PermissionName == permission), ct))
            throw new UnauthorizedAccessException("Nhân viên hiện không có quyền thao tác phiên.");
    }

    private async Task LockKeyAsync(string key, CancellationToken ct) =>
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"DECLARE @r int; EXEC @r = sp_getapplock @Resource={key}, @LockMode='Exclusive', @LockOwner='Transaction', @LockTimeout=10000; IF @r < 0 THROW 51000, 'Session operation is busy.', 1;", ct);

    private async Task<Computer> LockComputerAsync(int id, CancellationToken ct) =>
        await db.Computers.FromSqlInterpolated($"SELECT * FROM [Computers] WITH (UPDLOCK, HOLDLOCK) WHERE [ComputerId] = {id}")
            .SingleOrDefaultAsync(ct) ?? throw new EntityNotFoundException("Không tìm thấy máy.");

    private Task<GamingSession> LockSessionAsync(int id, CancellationToken ct) =>
        db.GamingSessions.FromSqlInterpolated($"SELECT * FROM [GamingSessions] WITH (UPDLOCK, HOLDLOCK) WHERE [GamingSessionId] = {id}").SingleAsync(ct);

    private void Queue(GamingSession session) => events.Enqueue(db.Database.CurrentTransaction!.TransactionId,
        new SessionChange(session.CustomerId, session.ComputerId, session.GamingSessionId));

    private static DateTimeOffset Utc(DateTime value) => new(DateTime.SpecifyKind(value, DateTimeKind.Utc));
    private static SessionDto ToDto(GamingSession x) => new(x.GamingSessionId, x.ComputerId, x.CustomerId,
        x.Computer.ComputerName, x.Computer.ComputerType.TypeName, x.Computer.ComputerType.CPU,
        x.Computer.ComputerType.RAM, x.Computer.ComputerType.GPU, Utc(x.StartTime),
        x.EndTime is DateTime end ? Utc(end) : null, x.LastChargedAt is DateTime last ? Utc(last) : null,
        x.PricePerHourSnapshot, x.TotalCost, x.Status, x.OpenedByEmployeeId, x.ClosedByActorType, x.CloseReason, x.ClosingInvoiceId);
}
