using System.Globalization;
using CyberManagement.Application.Exceptions;
using CyberManagement.Application.Interfaces.Repositories;
using CyberManagement.Domain.Entities;
using CyberManagement.Infrastructure.Persistance;
using Microsoft.EntityFrameworkCore;

namespace CyberManagement.Infrastructure.Repositories;

public sealed class InvoiceRepository(ApplicationDbContext db) : IInvoiceRepository
{
    public Task<Employee?> GetEmployeeAsync(int employeeId, CancellationToken ct) => db.Employees.AsNoTracking()
        .Include(x => x.Role).ThenInclude(x => x.RolePermissions).ThenInclude(x => x.Permission)
        .SingleOrDefaultAsync(x => x.EmployeeId == employeeId, ct);

    public async Task<(int CustomerId, int? SessionId)> IdentifySourceAsync(IReadOnlyList<int> orderIds, int? sessionId, CancellationToken ct)
    {
        if (orderIds.Count > 0)
        {
            var rows = await db.Orders.AsNoTracking().Where(x => orderIds.Contains(x.OrderId)).ToListAsync(ct);
            if (rows.Count != orderIds.Count) throw new EntityNotFoundException("Không tìm thấy đủ đơn hàng đã chọn.");
            if (rows.Select(x => x.CustomerId).Distinct().Count() != 1
                || rows.Select(x => x.GamingSessionId).Distinct().Count() != 1)
                throw new BusinessRuleException("Các đơn phải cùng khách và cùng phiên (hoặc cùng không có phiên).");
            if (sessionId.HasValue && rows.Any(x => x.GamingSessionId != sessionId))
                throw new BusinessRuleException("Đơn không thuộc phiên đã chọn.");
            return (rows[0].CustomerId, sessionId ?? rows[0].GamingSessionId);
        }
        var session = await db.GamingSessions.AsNoTracking().SingleOrDefaultAsync(x => x.GamingSessionId == sessionId, ct)
            ?? throw new EntityNotFoundException("Không tìm thấy phiên chơi.");
        return (session.CustomerId, session.GamingSessionId);
    }

    // Khóa khách → phiên → đơn/dòng → phí → Payment; Invoice được khóa sau nguồn.
    public async Task<InvoiceSources> LockSourcesAsync(int customerId, int? sessionId, IReadOnlyList<int> orderIds, CancellationToken ct)
    {
        RequireTransaction();
        GuardPendingSources();
        var customer = await db.Customers.FromSqlInterpolated(
            $"SELECT * FROM [dbo].[Customers] WITH (UPDLOCK, HOLDLOCK) WHERE [CustomerId] = {customerId}")
            .AsNoTracking().SingleOrDefaultAsync(ct) ?? throw new EntityNotFoundException("Không tìm thấy khách hàng.");
        GamingSession? session = null;
        if (sessionId.HasValue)
        {
            session = await db.GamingSessions.FromSqlInterpolated(
                $"SELECT * FROM [dbo].[GamingSessions] WITH (UPDLOCK, HOLDLOCK) WHERE [GamingSessionId] = {sessionId.Value}")
                .AsNoTracking().SingleOrDefaultAsync(ct) ?? throw new EntityNotFoundException("Không tìm thấy phiên chơi.");
            if (session.CustomerId != customerId) throw new BusinessRuleException("Phiên không thuộc khách hàng của hóa đơn.");
        }
        var orders = new List<Order>();
        foreach (var id in orderIds.Order())
        {
            var order = await db.Orders.FromSqlInterpolated(
                $"SELECT * FROM [dbo].[Orders] WITH (UPDLOCK, HOLDLOCK) WHERE [OrderId] = {id}")
                .AsNoTracking().SingleOrDefaultAsync(ct) ?? throw new EntityNotFoundException($"Không tìm thấy đơn {id}.");
            if (order.CustomerId != customerId || order.GamingSessionId != sessionId)
                throw new BusinessRuleException("Nguồn đơn không khớp khách/phiên của hóa đơn.");
            order.Details = await db.OrderDetails.FromSqlInterpolated(
                $"SELECT * FROM [dbo].[OrderDetails] WITH (UPDLOCK, HOLDLOCK) WHERE [OrderId] = {id}")
                .AsNoTracking().OrderBy(x => x.OrderDetailId).ToListAsync(ct);
            orders.Add(order);
        }
        var charges = sessionId.HasValue ? await db.SessionCharges.FromSqlInterpolated(
                $"SELECT * FROM [dbo].[SessionCharges] WITH (UPDLOCK, HOLDLOCK) WHERE [GamingSessionId] = {sessionId.Value}")
            .AsNoTracking().OrderBy(x => x.ChargeId).ToListAsync(ct) : [];
        // Hóa đơn món không khóa/thu phí phiên kèm theo.
        if (orderIds.Count > 0) charges = [];
        var chargeIds = charges.Select(x => x.ChargeId).ToArray();
        var paymentIds = await db.Payments.AsNoTracking()
            .Where(x => (x.OrderId != null && orderIds.Contains(x.OrderId.Value))
                || (x.SessionChargeId != null && chargeIds.Contains(x.SessionChargeId.Value)))
            .OrderBy(x => x.PaymentId).Select(x => x.PaymentId).ToListAsync(ct);
        var payments = new List<Payment>();
        foreach (var id in paymentIds)
        {
            var snapshot = await db.Payments.FromSqlInterpolated(
                $"SELECT * FROM [dbo].[Payments] WITH (UPDLOCK, HOLDLOCK) WHERE [PaymentId] = {id}")
                .AsNoTracking().SingleAsync(ct);
            payments.Add(TrackFresh(snapshot, x => x.PaymentId == id));
        }
        var ledger = await ReadLedgersAsync(orderIds, chargeIds, payments, ct);
        var billedOrders = await db.InvoiceOrders.AsNoTracking().Where(x => orderIds.Contains(x.OrderId))
            .Select(x => x.OrderId).ToListAsync(ct);
        var billedCharges = await db.InvoiceDetails.AsNoTracking().Where(x => x.SessionChargeId != null
            && chargeIds.Contains(x.SessionChargeId.Value)).Select(x => x.SessionChargeId!.Value).ToListAsync(ct);
        return new(customer, session, orders, charges, payments, ledger, billedOrders, billedCharges);
    }

    public Task<InvoiceIssueRequest?> GetIssueRequestAsync(string key, CancellationToken ct) =>
        db.InvoiceIssueRequests.AsNoTracking().SingleOrDefaultAsync(x => x.OperationKey == key, ct);

    // Khóa khách trước khi đọc nhiều bảng để thu/hoàn không chen giữa các truy vấn.
    public async Task<InvoiceReadSet> ReadAsync(int? customerId, int? invoiceId, CancellationToken ct)
    {
        // Chỉ kết thúc transaction do lần đọc này mở; giữ nguyên transaction của caller.
        await using var transaction = db.Database.CurrentTransaction is null
            ? await db.Database.BeginTransactionAsync(ct) : null;
        var selection = db.Invoices.AsNoTracking().AsQueryable();
        if (customerId.HasValue) selection = selection.Where(x => x.CustomerId == customerId);
        if (invoiceId.HasValue) selection = selection.Where(x => x.InvoiceId == invoiceId);
        // Chốt tập ID trước khi khóa, tránh kéo thêm hóa đơn của khách chưa được khóa.
        var selected = await selection.OrderByDescending(x => x.InvoiceDate).ThenByDescending(x => x.InvoiceId)
            .Take(invoiceId.HasValue ? 1 : customerId.HasValue ? 200 : 500)
            .Select(x => new { x.InvoiceId, x.CustomerId }).ToListAsync(ct);
        foreach (var id in selected.Select(x => x.CustomerId).Distinct().Order())
            await db.Customers.FromSqlInterpolated(
                $"SELECT * FROM [dbo].[Customers] WITH (UPDLOCK, HOLDLOCK) WHERE [CustomerId] = {id}")
                .AsNoTracking().SingleAsync(ct);

        var data = await ReadSelectedAsync(selected.Select(x => x.InvoiceId).ToArray(), customerId, ct);
        if (transaction is not null) await transaction.CommitAsync(ct);
        return data;
    }

    private async Task<InvoiceReadSet> ReadSelectedAsync(int[] selectedIds, int? customerId, CancellationToken ct)
    {
        if (selectedIds.Length == 0) return new([], [], [], []);
        var query = db.Invoices.AsNoTracking().AsSplitQuery()
            .Include(x => x.Customer).Include(x => x.Employee).Include(x => x.InvoiceOrders).Include(x => x.Details)
            .Where(x => selectedIds.Contains(x.InvoiceId));
        if (customerId.HasValue) query = query.Where(x => x.CustomerId == customerId);
        var invoices = await query.OrderByDescending(x => x.InvoiceDate).ThenByDescending(x => x.InvoiceId).ToListAsync(ct);
        var invoiceIds = invoices.Select(x => x.InvoiceId).ToArray();
        var orderIds = invoices.SelectMany(x => x.InvoiceOrders).Select(x => x.OrderId).Distinct().ToArray();
        var chargeIds = invoices.SelectMany(x => x.Details).Where(x => x.SessionChargeId != null)
            .Select(x => x.SessionChargeId!.Value).Distinct().ToArray();
        var payments = await db.Payments.AsNoTracking().Where(x => (x.InvoiceId != null && invoiceIds.Contains(x.InvoiceId.Value))
            || (x.OrderId != null && orderIds.Contains(x.OrderId.Value))
            || (x.SessionChargeId != null && chargeIds.Contains(x.SessionChargeId.Value))).ToListAsync(ct);
        var detailIds = invoices.SelectMany(x => x.Details).Where(x => x.OrderDetailId != null).Select(x => x.OrderDetailId!.Value).ToArray();
        var cancelledIds = await db.OrderDetails.AsNoTracking().Where(x => detailIds.Contains(x.OrderDetailId)
            && (x.Status == "Cancelled" || x.Order.Status == "Cancelled")).Select(x => x.OrderDetailId).ToListAsync(ct);
        return new(invoices, payments, await ReadLedgersAsync(orderIds, chargeIds, payments, ct), cancelledIds);
    }

    public async Task<Invoice?> LockInvoiceAsync(int invoiceId, CancellationToken ct)
    {
        RequireTransaction();
        var snapshot = await db.Invoices.FromSqlInterpolated(
            $"SELECT * FROM [dbo].[Invoices] WITH (UPDLOCK, HOLDLOCK) WHERE [InvoiceId] = {invoiceId}")
            .AsNoTracking().SingleOrDefaultAsync(ct);
        if (snapshot is null) return null;
        var invoice = TrackFresh(snapshot, x => x.InvoiceId == invoiceId);
        var details = await db.InvoiceDetails.FromSqlInterpolated(
            $"SELECT * FROM [dbo].[InvoiceDetails] WITH (UPDLOCK, HOLDLOCK) WHERE [InvoiceId] = {invoiceId}")
            .AsNoTracking().OrderBy(x => x.InvoiceDetailId).ToListAsync(ct);
        invoice.Details = details.Select(x => TrackFresh(x, y => y.InvoiceDetailId == x.InvoiceDetailId)).ToList();
        return invoice;
    }

    public async Task AddAsync(Invoice invoice, InvoiceIssueRequest request, AuditLog audit, CancellationToken ct)
    {
        RequireTransaction();
        await db.Invoices.AddAsync(invoice, ct);
        await db.InvoiceIssueRequests.AddAsync(request, ct);
        await db.AuditLogs.AddAsync(audit, ct);
    }

    public Task<int?> FindInvoiceForSourceAsync(int? orderId, int? chargeId, CancellationToken ct) => orderId.HasValue
        ? db.InvoiceOrders.Where(x => x.OrderId == orderId.Value).Select(x => (int?)x.InvoiceId).SingleOrDefaultAsync(ct)
        : db.InvoiceDetails.Where(x => x.SessionChargeId == chargeId).Select(x => (int?)x.InvoiceId).SingleOrDefaultAsync(ct);

    private async Task<IReadOnlyList<WalletTransaction>> ReadLedgersAsync(IReadOnlyList<int> orderIds,
        IReadOnlyList<int> chargeIds, IReadOnlyList<Payment> payments, CancellationToken ct)
    {
        var ids = payments.Where(x => x.WalletTransactionId != null).Select(x => x.WalletTransactionId!.Value).ToArray();
        var orders = orderIds.Select(x => x.ToString(CultureInfo.InvariantCulture)).ToArray();
        var charges = chargeIds.Select(x => x.ToString(CultureInfo.InvariantCulture)).ToArray();
        return await db.WalletTransactions.AsNoTracking().Where(x => ids.Contains(x.TransactionId)
            || (x.OriginalDebitId != null && ids.Contains(x.OriginalDebitId.Value))
            || (x.SourceType == "Order" && orders.Contains(x.SourceId!))
            || (x.SourceType == "SessionCharge" && charges.Contains(x.SourceId!))).ToListAsync(ct);
    }

    // Caller phải lưu các thay đổi nguồn trước khi chụp snapshot.
    private void GuardPendingSources()
    {
        db.ChangeTracker.DetectChanges();
        if (db.ChangeTracker.Entries().Any(x => x.State is EntityState.Added or EntityState.Modified or EntityState.Deleted
            && (x.Entity is Customer or GamingSession or Order or OrderDetail or Payment or WalletTransaction)))
            throw new BusinessRuleException("Hãy lưu thay đổi nguồn trong transaction chung trước khi phát hành/cập nhật hóa đơn.");
    }

    private T TrackFresh<T>(T snapshot, Func<T, bool> match) where T : class
    {
        db.ChangeTracker.DetectChanges();
        var entry = db.ChangeTracker.Entries<T>().SingleOrDefault(x => match(x.Entity));
        if (entry is null) { db.Attach(snapshot); return snapshot; }
        if (entry.State != EntityState.Unchanged)
            throw new BusinessRuleException("Dữ liệu hóa đơn/thu đang có thay đổi chưa lưu trước khi lấy khóa.");
        entry.CurrentValues.SetValues(snapshot);
        entry.OriginalValues.SetValues(snapshot);
        return entry.Entity;
    }

    private void RequireTransaction()
    {
        if (db.Database.CurrentTransaction is null) throw new InvalidOperationException("Phải mở transaction trước khi ghi hóa đơn.");
    }
}
