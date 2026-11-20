using CyberManagement.Application.DTOs.Orders;
using CyberManagement.Application.Exceptions;
using CyberManagement.Application.Interfaces.Repositories;
using CyberManagement.Domain.Entities;
using CyberManagement.Infrastructure.Persistance;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace CyberManagement.Infrastructure.Repositories;

public sealed class OrderRepository(ApplicationDbContext db, TimeProvider clock) : IOrderRepository
{
    public async Task<T> ExecuteAsync<T>(Func<Task<T>> action, CancellationToken ct)
    {
        if (!db.Database.IsSqlServer() || db.Database.CurrentTransaction is not null)
            throw new InvalidOperationException("Order use cases own one SQL Server transaction.");
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        try
        {
            var result = await action();
            await transaction.CommitAsync(ct);
            return result;
        }
        catch (Exception error)
        {
            await transaction.RollbackAsync(CancellationToken.None);
            db.ChangeTracker.Clear();
            if (error is DbUpdateConcurrencyException || error is SqlException { Number: 1205 or 1222 }
                || error is DbUpdateException { InnerException: SqlException { Number: 2601 or 2627 or 1205 } })
                throw new BusinessRuleException("ConcurrencyConflict: dữ liệu đã thay đổi, đọc lại đơn và retry cùng khóa.");
            throw;
        }
    }

    public async Task AuthorizeAsync(OrderActor actor, string? permission, CancellationToken ct)
    {
        if (actor.Id <= 0) throw new UnauthorizedAccessException();
        if (actor.Type == "Customer" && permission is null)
        {
            if (!await db.Customers.AnyAsync(x => x.CustomerId == actor.Id, ct))
                throw new UnauthorizedAccessException("Tài khoản khách không tồn tại.");
            return; // Locked customers may still read/replay their committed orders.
        }
        if (actor.Type != "Staff" || permission is null
            || !await db.Employees.AnyAsync(x => x.EmployeeId == actor.Id && x.IsActive
                && x.Role.RolePermissions.Any(p => p.Permission.PermissionName == permission), ct))
            throw new UnauthorizedAccessException("Nhân viên không còn quyền thực hiện thao tác này.");
    }

    public async Task<int> FindCustomerIdAsync(int orderId, int? ownerId, CancellationToken ct) =>
        await db.Orders.Where(x => x.OrderId == orderId && (ownerId == null || x.CustomerId == ownerId))
            .Select(x => (int?)x.CustomerId).SingleOrDefaultAsync(ct)
        ?? throw new EntityNotFoundException("Không tìm thấy đơn hàng.");

    public async Task ValidateSessionAsync(int customerId, int? sessionId, CancellationToken ct)
    {
        if (sessionId is null) return;
        var candidate = await db.GamingSessions.AsNoTracking().SingleOrDefaultAsync(x => x.GamingSessionId == sessionId, ct);
        if (candidate is null || candidate.CustomerId != customerId)
            throw new BusinessRuleException("SessionNotActive: phiên không thuộc khách hàng.");
        await db.Computers.FromSqlInterpolated(
            $"SELECT * FROM [Computers] WITH (UPDLOCK, HOLDLOCK) WHERE [ComputerId] = {candidate.ComputerId}")
            .AsNoTracking().SingleAsync(ct);
        var session = await db.GamingSessions.FromSqlInterpolated(
            $"SELECT * FROM [GamingSessions] WITH (UPDLOCK, HOLDLOCK) WHERE [GamingSessionId] = {sessionId}")
            .AsNoTracking().SingleAsync(ct);
        if (session.CustomerId != customerId || session.ComputerId != candidate.ComputerId
            || session.Status != "Active" || session.EndTime is not null)
            throw new BusinessRuleException("SessionNotActive: phiên đã kết thúc hoặc không hoạt động.");
    }

    public async Task<bool> CategoryActiveAsync(int categoryId, CancellationToken ct) =>
        (await db.Categories.FromSqlInterpolated(
            $"SELECT * FROM [Categories] WITH (HOLDLOCK) WHERE [CategoryId] = {categoryId}")
            .AsNoTracking().SingleAsync(ct)).IsActive;

    public async Task LockWalletSourcesAsync(int customerId, CancellationToken ct)
    {
        // Acquire the session lock group before Order/Product/Promotion, even when the
        // amount is only known after applying a voucher. Zero-value orders do not settle.
        var sessions = await db.GamingSessions.AsNoTracking().Where(x => x.CustomerId == customerId
            && (x.Status == "Active" || x.EndTime == null)).OrderBy(x => x.ComputerId).ToListAsync(ct);
        if (sessions.Count > 1) throw new BusinessRuleException("Khách có nhiều phiên chưa đóng; cần đối soát.");
        foreach (var session in sessions)
        {
            await db.Computers.FromSqlInterpolated(
                $"SELECT * FROM [Computers] WITH (UPDLOCK, HOLDLOCK) WHERE [ComputerId] = {session.ComputerId}")
                .AsNoTracking().SingleAsync(ct);
            await db.GamingSessions.FromSqlInterpolated(
                $"SELECT * FROM [GamingSessions] WITH (UPDLOCK, HOLDLOCK) WHERE [GamingSessionId] = {session.GamingSessionId}")
                .AsNoTracking().SingleAsync(ct);
            await db.SessionCharges.FromSqlInterpolated(
                $"SELECT * FROM [SessionCharges] WITH (UPDLOCK, HOLDLOCK) WHERE [GamingSessionId] = {session.GamingSessionId}")
                .AsNoTracking().ToListAsync(ct);
        }
    }

    public async Task LockRelatedSessionAsync(int orderId, CancellationToken ct)
    {
        var sessionId = await db.Orders.Where(x => x.OrderId == orderId).Select(x => x.GamingSessionId).SingleAsync(ct);
        if (sessionId is null) return;
        var computerId = await db.GamingSessions.Where(x => x.GamingSessionId == sessionId).Select(x => x.ComputerId).SingleAsync(ct);
        await db.Computers.FromSqlInterpolated(
            $"SELECT * FROM [Computers] WITH (UPDLOCK, HOLDLOCK) WHERE [ComputerId] = {computerId}")
            .AsNoTracking().SingleAsync(ct);
        await db.GamingSessions.FromSqlInterpolated(
            $"SELECT * FROM [GamingSessions] WITH (UPDLOCK, HOLDLOCK) WHERE [GamingSessionId] = {sessionId}")
            .AsNoTracking().SingleAsync(ct);
        await db.SessionCharges.FromSqlInterpolated(
            $"SELECT * FROM [SessionCharges] WITH (UPDLOCK, HOLDLOCK) WHERE [GamingSessionId] = {sessionId}")
            .AsNoTracking().ToListAsync(ct);
    }

    public async Task<Order?> LockAsync(int orderId, CancellationToken ct)
    {
        var snapshot = await db.Orders.FromSqlInterpolated(
            $"SELECT * FROM [Orders] WITH (UPDLOCK, HOLDLOCK) WHERE [OrderId] = {orderId}")
            .AsNoTracking().SingleOrDefaultAsync(ct);
        if (snapshot is null) return null;
        var order = db.Orders.Local.SingleOrDefault(x => x.OrderId == orderId);
        if (order is null) { order = snapshot; db.Attach(order); }
        else
        {
            db.Entry(order).CurrentValues.SetValues(snapshot);
            db.Entry(order).OriginalValues.SetValues(snapshot);
        }
        await db.Entry(order).Collection(x => x.Details).LoadAsync(ct);
        return order;
    }

    public Task<OrderOperation?> FindOperationAsync(OrderActor actor, string route, string key, CancellationToken ct) =>
        db.Set<OrderOperation>().AsNoTracking().SingleOrDefaultAsync(x => x.ActorType == actor.Type
            && x.ActorId == actor.Id && x.Route == route && x.Key == key, ct);

    // Multi-query DTO reads take the same customer lock as every writer.
    public async Task<OrderDto> ReadAsync(int orderId, int? customerId, CancellationToken ct)
    {
        await using var transaction = db.Database.CurrentTransaction is null
            ? await db.Database.BeginTransactionAsync(ct) : null;
        var owner = await FindCustomerIdAsync(orderId, customerId, ct);
        await LockReadCustomerAsync(owner, ct);
        var result = await ReadSelectedAsync(orderId, ct);
        if (transaction is not null) await transaction.CommitAsync(ct);
        return result;
    }

    public async Task<OrderPageDto> ReadPageAsync(int? customerId, string? status, int page, int pageSize, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var query = db.Orders.AsNoTracking().Where(x => (customerId == null || x.CustomerId == customerId)
            && (status == null || x.Status == status));
        var selected = await query.OrderByDescending(x => x.OrderTime).ThenByDescending(x => x.OrderId)
            .Skip((page - 1) * pageSize).Take(pageSize).Select(x => new { x.OrderId, x.CustomerId }).ToListAsync(ct);
        foreach (var id in selected.Select(x => x.CustomerId).Distinct().Order()) await LockReadCustomerAsync(id, ct);
        var items = new List<OrderDto>();
        foreach (var source in selected) items.Add(await ReadSelectedAsync(source.OrderId, ct));
        var result = new OrderPageDto(items, page, pageSize, await query.CountAsync(ct));
        await transaction.CommitAsync(ct);
        return result;
    }

    private Task<Customer> LockReadCustomerAsync(int id, CancellationToken ct) => db.Customers.FromSqlInterpolated(
        $"SELECT * FROM [Customers] WITH (UPDLOCK, HOLDLOCK) WHERE [CustomerId] = {id}").AsNoTracking().SingleAsync(ct);

    private async Task<OrderDto> ReadSelectedAsync(int orderId, CancellationToken ct)
    {
        var order = await db.Orders.AsNoTracking().Include(x => x.Details).SingleAsync(x => x.OrderId == orderId, ct);
        var payments = await PaymentsAsync(orderId, ct);
        var subtotal = order.Details.Sum(x => x.UnitPrice * x.Quantity);
        var cancelled = order.Details.Where(x => x.Status == "Cancelled" || order.Status is "Cancelled" or "Expired")
            .Sum(x => x.UnitPrice * x.Quantity - x.DiscountAmount);
        var collected = payments.Where(x => x.Type == "Payment" && x.Status == "Succeeded").Sum(x => x.Amount);
        var refunded = payments.Where(x => x.Type == "Refund" && x.Status == "Succeeded").Sum(x => x.Amount);
        var pending = payments.Where(x => x.Type == "Refund" && x.Status == "Pending").Sum(x => x.Amount);
        var intent = await GatewayIntentAsync(orderId, ct);
        var waitingGateway = order.PaymentMethod == "ZaloPay" && order.TotalAmount > 0 && collected == 0;
        var payable = order.TotalAmount - cancelled;
        var outstanding = Math.Max(0, payable - collected + refunded);
        var unresolved = Math.Max(0, Math.Min(cancelled, collected) - refunded);
        var status = unresolved > 0 ? "RefundPending" : refunded > 0
            ? (refunded == collected ? "Refunded" : "PartiallyRefunded")
            : collected > 0 && outstanding == 0 ? "Paid"
            : waitingGateway ? (payments.Any(x => x.Type == "Payment" && x.Status == "Failed") ? "Failed"
                : order.Status == "Expired" ? "Expired" : "Pending")
            : payable == 0 && collected == 0 ? "NotRequired"
            : order.PaymentMethod == "Cash" ? "PendingCollection" : "Pending";
        VoucherSnapshotDto? voucher = null;
        if (order.VoucherCodeSnapshot is not null)
        {
            var promotionId = await db.PromotionUsages.Where(x => x.OrderId == orderId).Select(x => x.PromotionId).SingleAsync(ct);
            voucher = new(promotionId, order.VoucherCodeSnapshot, order.VoucherDiscountTypeSnapshot!,
                order.VoucherDiscountValueSnapshot!.Value, order.DiscountAmount);
        }
        return new(orderId, order.CustomerId, order.GamingSessionId, order.EmployeeId,
            DateTime.SpecifyKind(order.OrderTime, DateTimeKind.Utc), order.Status, Convert.ToBase64String(order.Version),
            subtotal, order.DiscountAmount, order.TotalAmount, cancelled, payable, voucher,
            order.Details.OrderBy(x => x.LineNumber).ThenBy(x => x.OrderDetailId).Select(x => new OrderItemDto(
                x.OrderDetailId, x.ProductId, x.ProductNameSnapshot, x.Quantity, x.UnitPrice, x.DiscountAmount,
                x.UnitPrice * x.Quantity - x.DiscountAmount, x.Status, null)).ToArray(),
            new(order.PaymentMethod, status, collected, refunded, pending, unresolved, outstanding,
                payments.SingleOrDefault(x => x.Type == "Payment")?.PaymentId,
                order.Status == "AwaitingPayment" && order.ReservedUntilUtc > clock.GetUtcNow()
                    && intent?.Status == "Pending" ? intent.PaymentUrl : null,
                order.ReservedUntilUtc,
                intent?.RequiresReconciliation == true || waitingGateway && (intent is null || intent.PaymentUrl is null
                    || order.ReservedUntilUtc <= clock.GetUtcNow())
                    || unresolved > 0));
    }

    public Task<Payment> LockZaloPayPaymentAsync(int orderId, CancellationToken ct) => db.Payments.FromSqlInterpolated(
        $"SELECT * FROM [Payments] WITH (UPDLOCK, HOLDLOCK) WHERE [OrderId] = {orderId} AND [Type] = 'Payment' AND [Method] = 'ZaloPay'")
        .SingleAsync(ct);

    public Task<ZaloPayIntent?> GatewayIntentAsync(int orderId, CancellationToken ct) => db.Set<ZaloPayIntent>()
        .AsNoTracking().SingleOrDefaultAsync(x => x.OrderId == orderId, ct);

    public async Task<IReadOnlyList<int>> CheckoutWorkAsync(CancellationToken ct) =>
        await db.Orders.AsNoTracking().Where(x => x.PaymentMethod == "ZaloPay" && x.TotalAmount > 0 && x.ReservedUntilUtc != null
            && (x.Status == "AwaitingPayment" || db.Set<ZaloPayIntent>().Any(i => i.OrderId == x.OrderId && i.Status == "Succeeded"))
            && db.Payments.Any(p => p.OrderId == x.OrderId && p.Type == "Payment" && p.Status != "Succeeded"))
            .OrderBy(x => x.OrderId).Select(x => x.OrderId).ToListAsync(ct);

    public void AddAudit(AuditLog audit) => db.AuditLogs.Add(audit);

    public async Task<IReadOnlyList<Payment>> PaymentsAsync(int orderId, CancellationToken ct) =>
        await db.Payments.AsNoTracking().Where(x => x.OrderId == orderId).ToListAsync(ct);
    public async Task<IReadOnlyList<WalletTransaction>> LedgersAsync(int orderId, CancellationToken ct)
    {
        var source = orderId.ToString(System.Globalization.CultureInfo.InvariantCulture);
        var linked = db.Payments.Where(x => x.OrderId == orderId && x.WalletTransactionId != null)
            .Select(x => x.WalletTransactionId!.Value);
        return await db.WalletTransactions.AsNoTracking().Where(x => linked.Contains(x.TransactionId)
            || (x.SourceType == "Order" && x.SourceId == source)).ToListAsync(ct);
    }
    public void Add(Order order) => db.Orders.Add(order);
    public void AddPayment(Payment payment) => db.Payments.Add(payment);
    public void AddOperation(OrderOperation operation) => db.Set<OrderOperation>().Add(operation);
    public async Task SaveAsync(CancellationToken ct) => await db.SaveChangesAsync(ct);
}
