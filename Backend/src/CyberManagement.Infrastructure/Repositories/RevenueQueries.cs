using System.Globalization;
using CyberManagement.Application.Interfaces.Repositories;
using CyberManagement.Domain.Entities;
using CyberManagement.Infrastructure.Persistance;
using Microsoft.EntityFrameworkCore;

namespace CyberManagement.Infrastructure.Repositories;

public sealed class RevenueQueries(ApplicationDbContext db) : IRevenueQueries
{
    // Đọc quyền hiện tại, không chỉ dựa vào thông tin trong JWT.
    public Task<Employee?> GetEmployeeAsync(int employeeId, CancellationToken cancellationToken = default) =>
        db.Employees.AsNoTracking()
            .Include(x => x.Role).ThenInclude(x => x.RolePermissions).ThenInclude(x => x.Permission)
            .SingleOrDefaultAsync(x => x.EmployeeId == employeeId, cancellationToken);

    // Giữ cùng một trạng thái dữ liệu trong suốt lần đọc báo cáo.
    public async Task<RevenueReadSet> ReadAsync(DateTimeOffset fromInclusive, DateTimeOffset toExclusive,
        CancellationToken cancellationToken = default)
    {
        if (fromInclusive >= toExclusive || toExclusive - fromInclusive > TimeSpan.FromDays(366))
            throw new ArgumentException("Khoảng thời gian báo cáo không hợp lệ.");
        // Báo cáo có transaction riêng; không lấy thêm khóa khách từ transaction đặt món.
        if (db.Database.CurrentTransaction is not null)
            throw new InvalidOperationException("Hãy đọc báo cáo ngoài transaction ghi nghiệp vụ.");

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var customerIds = await db.Customers.AsNoTracking().OrderBy(x => x.CustomerId)
            .Select(x => x.CustomerId).ToArrayAsync(cancellationToken);
        // Chốt tập khách và lấy khóa theo ID tăng dần trước khi đọc nguồn/tiền.
        foreach (var id in customerIds)
            await db.Customers.FromSqlInterpolated(
                $"SELECT * FROM [dbo].[Customers] WITH (UPDLOCK, HOLDLOCK) WHERE [CustomerId] = {id}")
                .AsNoTracking().SingleAsync(cancellationToken);

        var result = await ReadDataAsync(customerIds, fromInclusive, toExclusive, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return result;
    }

    private async Task<RevenueReadSet> ReadDataAsync(int[] customerIds, DateTimeOffset from,
        DateTimeOffset to, CancellationToken ct)
    {
        if (customerIds.Length == 0) return new([], [], [], [], [], [], [], []);
        var fromUtc = from.UtcDateTime;
        var toUtc = to.UtcDateTime;
        var ordersQuery = db.Orders.AsNoTracking().Where(x => customerIds.Contains(x.CustomerId));
        var chargesQuery = db.SessionCharges.AsNoTracking()
            .Where(x => customerIds.Contains(x.GamingSession.CustomerId));
        var paymentsQuery = db.Payments.AsNoTracking().Where(x =>
            (x.Order != null && customerIds.Contains(x.Order.CustomerId))
            || (x.SessionCharge != null && customerIds.Contains(x.SessionCharge.GamingSession.CustomerId))
            || (x.Invoice != null && customerIds.Contains(x.Invoice.CustomerId)));
        var walletsQuery = db.WalletTransactions.AsNoTracking().Where(x => customerIds.Contains(x.CustomerId));
        var topUpsQuery = db.CounterTopUpRequests.AsNoTracking().Where(x => customerIds.Contains(x.CustomerId));
        var invoicesQuery = db.Invoices.AsNoTracking().AsSplitQuery()
            .Include(x => x.InvoiceOrders).Include(x => x.Details)
            .Where(x => customerIds.Contains(x.CustomerId));

        // Lấy các khoản nạp và ledger phát sinh trong kỳ trước.
        var topUpSeed = await topUpsQuery.Where(x =>
            (x.CreatedAt >= fromUtc && x.CreatedAt < toUtc)
            || (x.ApprovedAt != null && x.ApprovedAt >= fromUtc && x.ApprovedAt < toUtc)).ToListAsync(ct);
        var topUpWalletIds = topUpSeed.Where(x => x.WalletTransactionId != null)
            .Select(x => x.WalletTransactionId!.Value).ToArray();
        var walletSeed = await walletsQuery.Where(x => (x.CreatedAt >= fromUtc && x.CreatedAt < toUtc)
            || topUpWalletIds.Contains(x.TransactionId)).ToListAsync(ct);
        walletSeed = await WithOriginalDebitsAsync(walletsQuery, walletSeed, ct);
        var seedWalletIds = walletSeed.Select(x => x.TransactionId).ToArray();

        // Pending/Failed vẫn được đọc để đối soát; service quyết định khoản nào được tính.
        var paymentSeed = await paymentsQuery.Where(x =>
            (x.PaymentTime != null && x.PaymentTime >= from && x.PaymentTime < to)
            || (x.ConfirmedAt != null && x.ConfirmedAt >= from && x.ConfirmedAt < to)
            || (x.CreatedAt >= from && x.CreatedAt < to)
            || (x.WalletTransactionId != null && seedWalletIds.Contains(x.WalletTransactionId.Value))
            || (x.Status == "Succeeded" && x.PaymentTime == null)).ToListAsync(ct);
        paymentSeed = await WithOriginalPaymentsAsync(paymentsQuery, paymentSeed, ct);
        var seedPaymentIds = paymentSeed.Select(x => x.PaymentId).ToArray();
        var seedInvoiceIds = paymentSeed.Where(x => x.InvoiceId != null).Select(x => x.InvoiceId!.Value).ToArray();
        var invoices = await invoicesQuery.Where(x => (x.InvoiceDate >= fromUtc && x.InvoiceDate < toUtc)
            || seedInvoiceIds.Contains(x.InvoiceId)).ToListAsync(ct);

        // Chọn nguồn theo ngày và các giao dịch liên quan, kể cả nguồn nằm ngoài kỳ.
        var orderIds = (await ordersQuery.Where(x => x.OrderTime >= fromUtc && x.OrderTime < toUtc)
            .Select(x => x.OrderId).ToListAsync(ct)).ToHashSet();
        var chargeIds = (await chargesQuery.Where(x => x.EndTime >= fromUtc && x.EndTime < toUtc)
            .Select(x => x.ChargeId).ToListAsync(ct)).ToHashSet();
        orderIds.UnionWith(paymentSeed.Where(x => x.OrderId != null).Select(x => x.OrderId!.Value));
        chargeIds.UnionWith(paymentSeed.Where(x => x.SessionChargeId != null).Select(x => x.SessionChargeId!.Value));
        orderIds.UnionWith(SourceIds(walletSeed, "Order"));
        chargeIds.UnionWith(SourceIds(walletSeed, "SessionCharge"));
        // Hoàn ví lưu SourceId dạng OrderId:OrderDetailId; hai ID phải khớp cùng một dòng.
        var refundSources = OrderDetailSources(walletSeed);
        var refundDetailIds = refundSources.Select(x => x.OrderDetailId).Distinct().ToArray();
        var refundDetails = await db.OrderDetails.AsNoTracking().Where(x => refundDetailIds.Contains(x.OrderDetailId)
            && customerIds.Contains(x.Order.CustomerId))
            .Select(x => new { x.OrderId, x.OrderDetailId }).ToListAsync(ct);
        orderIds.UnionWith(refundDetails.Where(x => refundSources.Contains((x.OrderId, x.OrderDetailId)))
            .Select(x => x.OrderId));
        orderIds.UnionWith(await db.InventoryTransactions.AsNoTracking().Where(x => x.OrderId != null
            && customerIds.Contains(x.Order!.CustomerId) && x.CreatedAt >= from && x.CreatedAt < to)
            .Select(x => x.OrderId!.Value).ToListAsync(ct));
        orderIds.UnionWith(await db.PromotionUsages.AsNoTracking().Where(x => customerIds.Contains(x.Order.CustomerId)
            && ((x.UsedAt >= from && x.UsedAt < to)
                || (x.ReleasedAt != null && x.ReleasedAt >= from && x.ReleasedAt < to)))
            .Select(x => x.OrderId).ToListAsync(ct));
        AddInvoiceSources(invoices, orderIds, chargeIds);

        // Hóa đơn đã phát hành trước kỳ vẫn cần để đối chiếu thu/hoàn trong kỳ.
        var relatedInvoices = await invoicesQuery.Where(x =>
            x.InvoiceOrders.Any(y => orderIds.Contains(y.OrderId))
            || x.Details.Any(y => y.SessionChargeId != null && chargeIds.Contains(y.SessionChargeId.Value)))
            .ToListAsync(ct);
        invoices = invoices.Concat(relatedInvoices).DistinctBy(x => x.InvoiceId).ToList();
        AddInvoiceSources(invoices, orderIds, chargeIds);
        var invoiceIds = invoices.Select(x => x.InvoiceId).ToArray();

        var orders = await ordersQuery.Where(x => orderIds.Contains(x.OrderId))
            .Include(x => x.Details).OrderBy(x => x.OrderId).ToListAsync(ct);
        var charges = await chargesQuery.Where(x => chargeIds.Contains(x.ChargeId))
            .Include(x => x.GamingSession).OrderBy(x => x.ChargeId).ToListAsync(ct);
        var detailIds = orders.SelectMany(x => x.Details).Select(x => x.OrderDetailId).ToArray();

        // Lấy đầy đủ lịch sử thu/hoàn của các nguồn, không chỉ dòng nằm trong kỳ.
        var payments = await paymentsQuery.Where(x => seedPaymentIds.Contains(x.PaymentId)
            || (x.OrderId != null && orderIds.Contains(x.OrderId.Value))
            || (x.SessionChargeId != null && chargeIds.Contains(x.SessionChargeId.Value))
            || (x.InvoiceId != null && invoiceIds.Contains(x.InvoiceId.Value))).ToListAsync(ct);
        payments = await WithOriginalPaymentsAsync(paymentsQuery, payments, ct);
        var paymentWalletIds = payments.Where(x => x.WalletTransactionId != null)
            .Select(x => x.WalletTransactionId!.Value).Concat(seedWalletIds).Distinct().ToArray();
        var orderSources = orderIds.Select(x => x.ToString(CultureInfo.InvariantCulture)).ToArray();
        var chargeSources = chargeIds.Select(x => x.ToString(CultureInfo.InvariantCulture)).ToArray();
        var detailSources = orders.SelectMany(x => x.Details).Select(x =>
            x.OrderId.ToString(CultureInfo.InvariantCulture) + ":" + x.OrderDetailId.ToString(CultureInfo.InvariantCulture))
            .Distinct().ToArray();
        var wallets = await walletsQuery.Where(x => paymentWalletIds.Contains(x.TransactionId)
            || (x.SourceType == "Order" && orderSources.Contains(x.SourceId!))
            || (x.SourceType == "SessionCharge" && chargeSources.Contains(x.SourceId!))
            || (x.SourceType == "OrderDetail" && detailSources.Contains(x.SourceId!))
            || (x.OriginalDebitId != null && paymentWalletIds.Contains(x.OriginalDebitId.Value))).ToListAsync(ct);
        wallets = await WithOriginalDebitsAsync(walletsQuery, wallets, ct);
        // Giữ khoản hoàn liên kết debit đã đọc, kể cả SourceId lịch sử cần đối soát thêm.
        var originalIds = wallets.Where(x => x.Amount < 0).Select(x => x.TransactionId).ToArray();
        var linkedRefunds = await walletsQuery.Where(x => x.OriginalDebitId != null
            && originalIds.Contains(x.OriginalDebitId.Value)).ToListAsync(ct);
        wallets = wallets.Concat(linkedRefunds).DistinctBy(x => x.TransactionId).ToList();
        var walletIds = wallets.Select(x => x.TransactionId).ToArray();
        var counterIds = SourceIds(wallets, "CounterTopUp");
        var seedTopUpIds = topUpSeed.Select(x => x.RequestId).ToArray();
        var topUps = await topUpsQuery.Where(x => seedTopUpIds.Contains(x.RequestId) || counterIds.Contains(x.RequestId)
            || (x.WalletTransactionId != null && walletIds.Contains(x.WalletTransactionId.Value))).ToListAsync(ct);
        var movements = await db.InventoryTransactions.AsNoTracking().Where(x =>
            (x.OrderId != null && orderIds.Contains(x.OrderId.Value))
            || (x.OrderDetailId != null && detailIds.Contains(x.OrderDetailId.Value)))
            .OrderBy(x => x.InvTransId).ToListAsync(ct);
        var usages = await db.PromotionUsages.AsNoTracking().Include(x => x.Promotion)
            .Where(x => orderIds.Contains(x.OrderId))
            .OrderBy(x => x.PromotionUsageId).ToListAsync(ct);

        return new(payments.OrderBy(x => x.PaymentId).ToArray(), wallets.OrderBy(x => x.TransactionId).ToArray(),
            topUps.OrderBy(x => x.RequestId).ToArray(), orders, charges, movements, usages,
            invoices.OrderBy(x => x.InvoiceId).ToArray());
    }

    // Hoàn trong kỳ có thể tham chiếu khoản thu trước kỳ.
    private static async Task<List<Payment>> WithOriginalPaymentsAsync(IQueryable<Payment> query,
        List<Payment> rows, CancellationToken ct)
    {
        var ids = rows.Where(x => x.RefundOfPaymentId != null).Select(x => x.RefundOfPaymentId!.Value)
            .Except(rows.Select(x => x.PaymentId)).Distinct().ToArray();
        if (ids.Length == 0) return rows;
        var originals = await query.Where(x => ids.Contains(x.PaymentId)).ToListAsync(ct);
        return rows.Concat(originals).DistinctBy(x => x.PaymentId).ToList();
    }

    // Lấy debit gốc để kiểm tra đúng nguồn và khách của khoản hoàn ví.
    private static async Task<List<WalletTransaction>> WithOriginalDebitsAsync(IQueryable<WalletTransaction> query,
        List<WalletTransaction> rows, CancellationToken ct)
    {
        var ids = rows.Where(x => x.OriginalDebitId != null).Select(x => x.OriginalDebitId!.Value)
            .Except(rows.Select(x => x.TransactionId)).Distinct().ToArray();
        if (ids.Length == 0) return rows;
        var originals = await query.Where(x => ids.Contains(x.TransactionId)).ToListAsync(ct);
        return rows.Concat(originals).DistinctBy(x => x.TransactionId).ToList();
    }

    private static HashSet<(int OrderId, int OrderDetailId)> OrderDetailSources(IEnumerable<WalletTransaction> rows)
    {
        var sources = new HashSet<(int OrderId, int OrderDetailId)>();
        foreach (var row in rows.Where(x => x.SourceType == "OrderDetail"))
        {
            var parts = row.SourceId?.Split(':');
            if (parts is { Length: 2 }
                && int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out var orderId)
                && int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var detailId)
                && orderId > 0 && detailId > 0)
                sources.Add((orderId, detailId));
        }
        // Nguồn không hợp lệ vẫn còn trong ledger đã đọc để service báo sai lệch.
        return sources;
    }

    private static int[] SourceIds(IEnumerable<WalletTransaction> rows, string sourceType) => rows
        .Where(x => x.SourceType == sourceType)
        .Select(x => int.TryParse(x.SourceId, NumberStyles.None, CultureInfo.InvariantCulture, out var id) ? id : 0)
        .Where(x => x > 0).Distinct().ToArray();

    private static void AddInvoiceSources(IEnumerable<Invoice> invoices, HashSet<int> orderIds, HashSet<int> chargeIds)
    {
        orderIds.UnionWith(invoices.SelectMany(x => x.InvoiceOrders).Select(x => x.OrderId));
        chargeIds.UnionWith(invoices.SelectMany(x => x.Details).Where(x => x.SessionChargeId != null)
            .Select(x => x.SessionChargeId!.Value));
    }
}
