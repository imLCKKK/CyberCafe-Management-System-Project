using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using CyberManagement.Application.DTOs.Invoices;
using CyberManagement.Application.Exceptions;
using CyberManagement.Application.Interfaces.Repositories;
using CyberManagement.Application.Interfaces.Services;
using CyberManagement.Domain.Entities;

namespace CyberManagement.Application.Services;

public sealed class InvoiceService(IInvoiceRepository repository, IInvoiceUnitOfWork unitOfWork)
    : IInvoiceService, ISessionInvoiceIssuer
{
    private const decimal MaxMoney = 999999999999999999m;
    private const string AutoIssueReason = "InsufficientBalance";

    public async Task<IReadOnlyList<InvoiceDto>> GetAllAsync(int employeeId, CancellationToken ct = default)
    {
        await AuthorizeEmployeeAsync(employeeId, "invoice.view", ct);
        var data = await repository.ReadAsync(null, null, ct);
        return data.Invoices.Select(x => InvoiceAccounting.ToDto(x, data.Payments, data.Ledgers, data.CancelledDetailIds)).ToArray();
    }

    public async Task<InvoiceDto> GetByIdAsync(int invoiceId, int employeeId, CancellationToken ct = default)
    {
        Positive(invoiceId);
        await AuthorizeEmployeeAsync(employeeId, "invoice.view", ct);
        return await ReadOneAsync(invoiceId, null, ct);
    }

    public async Task<IReadOnlyList<InvoiceDto>> GetOwnAsync(int customerId, CancellationToken ct = default)
    {
        Positive(customerId);
        var data = await repository.ReadAsync(customerId, null, ct);
        return data.Invoices.Select(x => InvoiceAccounting.ToDto(x, data.Payments, data.Ledgers, data.CancelledDetailIds)).ToArray();
    }

    public Task<InvoiceDto> GetOwnByIdAsync(int invoiceId, int customerId, CancellationToken ct = default)
    {
        Positive(invoiceId); Positive(customerId);
        return ReadOneAsync(invoiceId, customerId, ct);
    }

    public async Task<InvoiceDto> IssueAsync(IssueInvoiceRequest request, int employeeId, string operationKey,
        CancellationToken ct = default)
    {
        await AuthorizeEmployeeAsync(employeeId, "invoice.create", ct);
        ValidateKey(operationKey, staff: true);
        var normalized = Normalize(request);
        var identity = await repository.IdentifySourceAsync(normalized.OrderIds, normalized.GamingSessionId, ct);
        var hash = Hash(new { version = 1, kind = normalized.Kind, customerId = identity.CustomerId, employeeId,
            gamingSessionId = normalized.GamingSessionId, orderIds = normalized.OrderIds });
        var replay = await ReplayAsync(operationKey, hash, ct);
        if (replay is not null) return replay;
        // Replay lịch sử được tra trước khi cấm tạo mới hóa đơn trộn hai loại nguồn.
        if ((normalized.Kind == "Orders" && normalized.OrderIds.Count == 0)
            || (normalized.Kind == "Session" && (normalized.OrderIds.Count != 0 || normalized.GamingSessionId is null)))
            throw new ArgumentException("Chọn đơn món hoặc phiên đã kết thúc; không trộn hai loại nguồn.");
        var callerOwnsTransaction = unitOfWork.HasActiveTransaction;
        try
        {
            return (await unitOfWork.ExecuteAsync(async () =>
            {
                var sources = await repository.LockSourcesAsync(identity.CustomerId, identity.SessionId, normalized.OrderIds, ct);
                var existing = await ReplayAsync(operationKey, hash, ct);
                if (existing is not null) return existing;
                return await CreateAsync(normalized.Kind, sources, employeeId, operationKey, hash, system: false, ct)
                    ?? throw new BusinessRuleException("Phiên không còn phí chưa phát hành.");
            }, ct));
        }
        catch (InvoiceWriteConflictException) when (!callerOwnsTransaction)
        {
            // UnitOfWork đã rollback toàn bộ trước khi đọc khóa thắng cuộc.
            return await ReplayAsync(operationKey, hash, ct) ?? throw new InvoiceWriteConflictException();
        }
    }

    // Worker server tự tạo khóa/hash; API không nhận actor hoặc khóa System từ khách.
    public async Task<InvoiceDto?> IssueForAutoClosedSessionAsync(int sessionId, CancellationToken ct = default)
    {
        Positive(sessionId);
        RequireTransaction();
        var identity = await repository.IdentifySourceAsync([], sessionId, ct);
        var key = $"SessionAutoInvoice:{sessionId}";
        var hash = Hash(new { version = 2, kind = "Session", customerId = identity.CustomerId,
            actorType = "System", actorKey = "SessionBilling", employeeId = (int?)null,
            reason = AutoIssueReason, gamingSessionId = sessionId, orderIds = Array.Empty<int>() });
        var sources = await repository.LockSourcesAsync(identity.CustomerId, sessionId, [], ct);
        var replay = await ReplayAsync(key, hash, ct);
        if (replay is not null) return replay;
        var session = sources.Session;
        // Insufficient funds means unable to fund the next second, even with a positive residual balance.
        if (session?.ClosedByActorType != "System" || session.CloseReason != AutoIssueReason
            || session.Status != "Ended" || session.EndTime is null || session.PricePerHourSnapshot is not > 0
            || decimal.Floor((session.TotalCost + decimal.Floor(sources.Customer.WalletBalance)) * 3600m
                / session.PricePerHourSnapshot.Value) > decimal.Floor((decimal)(session.EndTime.Value - session.StartTime).TotalSeconds))
            throw new BusinessRuleException("Chỉ tự phát hành theo ngữ cảnh hết số dư đã được worker xác minh.");
        return await CreateAsync("Session", sources, null, key, hash, system: true, ct);
    }

    // Callback/cash/refund gọi sau khi lưu nguồn; cùng khóa khách bảo vệ tổng tiền.
    public async Task<InvoiceDto?> ApplyFinancialChangeAsync(int? orderId, int? sessionChargeId, CancellationToken ct = default)
    {
        RequireTransaction();
        if (orderId.HasValue == sessionChargeId.HasValue || orderId is <= 0 || sessionChargeId is <= 0)
            throw new ArgumentException("Phải chọn đúng một nguồn đơn hoặc phí phiên.");
        var id = await repository.FindInvoiceForSourceAsync(orderId, sessionChargeId, ct);
        if (id is null) return null;
        var current = await ReadOneAsync(id.Value, null, ct);
        var sources = await repository.LockSourcesAsync(current.CustomerId, current.GamingSessionId,
            orderId is int sourceOrder ? [sourceOrder] : [], ct);
        var invoice = await repository.LockInvoiceAsync(id.Value, ct)
            ?? throw new EntityNotFoundException("Không tìm thấy hóa đơn nguồn.");
        var selected = sources.Payments.Where(x => orderId.HasValue ? x.OrderId == orderId : x.SessionChargeId == sessionChargeId).ToArray();
        LinkPayments(invoice, selected);
        // Hủy toàn đơn trước/sau phát hành đều giữ nguyên giá và giảm gốc.
        if (orderId.HasValue)
        {
            var cancelledSourceOrder = sources.Orders.Single();
            var detailIds = cancelledSourceOrder.Details.Where(x => cancelledSourceOrder.Status == "Cancelled" || x.Status == "Cancelled").Select(x => x.OrderDetailId).ToArray();
            foreach (var line in invoice.Details.Where(x => x.OrderDetailId != null && detailIds.Contains(x.OrderDetailId.Value)))
                line.CancelledAmount = line.Amount;
        }
        await unitOfWork.SaveChangesAsync(ct);
        await UpdateStatusAsync(invoice, ct);
        return await ReadOneAsync(invoice.InvoiceId, null, ct);
    }

    // Điểm gọi nội bộ sau khi caller xác minh hủy từng dòng; không có endpoint hủy hóa đơn tùy ý.
    public async Task RecordCancelledOrderLinesAsync(int orderId, IReadOnlyCollection<int> detailIds, CancellationToken ct = default)
    {
        RequireTransaction(); Positive(orderId);
        ArgumentNullException.ThrowIfNull(detailIds);
        if (detailIds.Count == 0 || detailIds.Any(x => x <= 0)) throw new ArgumentException("Phải chọn dòng món đã hủy hợp lệ.");
        var identity = await repository.IdentifySourceAsync([orderId], null, ct);
        var sources = await repository.LockSourcesAsync(identity.CustomerId, identity.SessionId, [orderId], ct);
        if (detailIds.Any(id => !sources.Orders.Single().Details.Any(x => x.OrderDetailId == id)))
            throw new BusinessRuleException("Dòng hủy không thuộc đơn nguồn.");
        if (sources.Orders.Single().Status != "Cancelled" && detailIds.Any(id => sources.Orders.Single().Details.Single(x => x.OrderDetailId == id).Status != "Cancelled"))
            throw new BusinessRuleException("Nguồn dòng món chưa được xác minh hủy.");
        var invoiceId = await repository.FindInvoiceForSourceAsync(orderId, null, ct);
        if (invoiceId is null) return;
        var invoice = await repository.LockInvoiceAsync(invoiceId.Value, ct)
            ?? throw new EntityNotFoundException("Không tìm thấy hóa đơn nguồn.");
        foreach (var line in invoice.Details.Where(x => x.OrderDetailId != null && detailIds.Contains(x.OrderDetailId.Value)))
            line.CancelledAmount = line.Amount; // Gán giá trị snapshot, gọi lặp không cộng lần hai.
        await unitOfWork.SaveChangesAsync(ct);
        await UpdateStatusAsync(invoice, ct);
    }

    private async Task<InvoiceDto?> CreateAsync(string kind, InvoiceSources sources, int? employeeId,
        string key, string hash, bool system, CancellationToken ct)
    {
        var invoice = new Invoice
        {
            CustomerId = sources.Customer.CustomerId,
            GamingSessionId = sources.Session?.GamingSessionId,
            EmployeeId = employeeId, ActorType = system ? "System" : "Staff", InvoiceDate = DateTime.UtcNow
        };
        IReadOnlyList<Payment> selectedPayments;
        IReadOnlyList<int> selectedCharges = [];
        if (kind == "Orders")
        {
            if (sources.BilledOrderIds.Count != 0) throw new BusinessRuleException("Một đơn đã được phát hành hóa đơn.");
            foreach (var order in sources.Orders)
            {
                if (order.Details.Count == 0) throw new BusinessRuleException("Đơn không có dòng món để phát hành.");
                decimal gross = 0, discount = 0;
                foreach (var detail in order.Details.OrderBy(x => x.OrderDetailId))
                {
                    Money(detail.UnitPrice); Money(detail.DiscountAmount);
                    if (detail.Quantity <= 0 || string.IsNullOrWhiteSpace(detail.ProductNameSnapshot))
                        throw new BusinessRuleException("Dòng món thiếu số lượng hoặc tên snapshot.");
                    var lineGross = detail.UnitPrice * detail.Quantity;
                    Money(lineGross);
                    if (gross > MaxMoney - lineGross) throw new BusinessRuleException("Tổng dòng món vượt giới hạn lưu trữ.");
                    if (detail.DiscountAmount > lineGross) throw new BusinessRuleException("Giảm dòng món vượt giá trị dòng.");
                    gross += lineGross; discount += detail.DiscountAmount;
                    var net = lineGross - detail.DiscountAmount;
                    invoice.Details.Add(new InvoiceDetail
                    {
                        OrderDetailId = detail.OrderDetailId, ItemType = "Food", Description = detail.ProductNameSnapshot,
                        Quantity = detail.Quantity, UnitPrice = detail.UnitPrice, Amount = net,
                        CancelledAmount = order.Status == "Cancelled" || detail.Status == "Cancelled" ? net : 0,
                        VoucherCodeSnapshot = order.VoucherCodeSnapshot,
                        VoucherDiscountTypeSnapshot = order.VoucherDiscountTypeSnapshot,
                        VoucherDiscountValueSnapshot = order.VoucherDiscountValueSnapshot
                    });
                }
                if (discount != order.DiscountAmount || gross - discount != order.TotalAmount)
                    throw new BusinessRuleException("Tổng đơn hoặc phân bổ giảm không khớp snapshot dòng món.");
                invoice.InvoiceOrders.Add(new InvoiceOrder { OrderId = order.OrderId });
                invoice.Subtotal += gross; invoice.Discount += discount;
            }
            selectedPayments = sources.Payments.Where(x => x.OrderId != null).ToArray();
        }
        else
        {
            if (sources.Session is null || sources.Session.Status != "Ended" || sources.Session.EndTime is null)
                throw new BusinessRuleException("Chỉ phát hành phí giờ của phiên đã kết thúc.");
            var unbilled = sources.Charges.Where(x => !sources.BilledChargeIds.Contains(x.ChargeId) && x.Amount > 0).ToArray();
            if (unbilled.Length == 0)
            {
                if (system) return null;
                throw new BusinessRuleException("Phiên không có phí chưa phát hành.");
            }
            selectedCharges = unbilled.Select(x => x.ChargeId).ToArray();
            selectedPayments = sources.Payments.Where(x => x.SessionChargeId != null && selectedCharges.Contains(x.SessionChargeId.Value)).ToArray();
            foreach (var charge in unbilled)
            {
                Money(charge.Amount);
                if (charge.EndTime <= charge.StartTime) throw new BusinessRuleException("Khoảng phí phiên không hợp lệ.");
                var chargePayments = selectedPayments.Where(x => x.SessionChargeId == charge.ChargeId).ToArray();
                var paid = InvoiceAccounting.Totals(sources.Customer.CustomerId, [], [charge.ChargeId], chargePayments, sources.Ledgers);
                if (paid.Net != charge.Amount) throw new BusinessRuleException("Phí giờ chưa có đủ khoản thu đã xác minh, cần đối soát.");
                invoice.Details.Add(new InvoiceDetail
                {
                    SessionChargeId = charge.ChargeId, ItemType = "Gaming",
                    Description = $"Phí phiên #{charge.GamingSessionId}: {charge.StartTime:yyyy-MM-dd HH:mm} – {charge.EndTime:yyyy-MM-dd HH:mm} UTC",
                    Quantity = 1, UnitPrice = charge.Amount, Amount = charge.Amount
                });
                invoice.Subtotal += charge.Amount;
            }
        }
        invoice.TotalAmount = invoice.Subtotal - invoice.Discount;
        Money(invoice.Subtotal); Money(invoice.Discount); Money(invoice.TotalAmount);
        // Đối soát ví trước khi gắn khoản thu vào hóa đơn.
        _ = InvoiceAccounting.Totals(invoice.CustomerId, invoice.InvoiceOrders.Select(x => x.OrderId).ToArray(),
            selectedCharges, selectedPayments, sources.Ledgers);
        LinkPayments(invoice, selectedPayments);
        var audit = new AuditLog { EntityType = "Invoice", EmployeeId = employeeId,
            Action = system
                ? $"Issue System; session={invoice.GamingSessionId}; reason={AutoIssueReason}; key={key}"
                : $"Issue {invoice.ActorType}; key={key}", CreatedAt = DateTime.UtcNow };
        await repository.AddAsync(invoice, new InvoiceIssueRequest { OperationKey = key, RequestHash = hash, Invoice = invoice }, audit, ct);
        await unitOfWork.SaveChangesAsync(ct);
        audit.EntityId = invoice.InvoiceId;
        await unitOfWork.SaveChangesAsync(ct);
        await UpdateStatusAsync(invoice, ct);
        return await ReadOneAsync(invoice.InvoiceId, null, ct);
    }

    private static void LinkPayments(Invoice invoice, IEnumerable<Payment> payments)
    {
        foreach (var payment in payments)
        {
            if (payment.InvoiceId is int existing && existing != invoice.InvoiceId)
                throw new BusinessRuleException("Khoản thu đã gắn hóa đơn khác, không được chuyển nguồn.");
            if (payment.Status == "Succeeded" && payment.InvoiceId is null) payment.Invoice = invoice;
        }
    }

    private async Task UpdateStatusAsync(Invoice invoice, CancellationToken ct)
    {
        var current = await ReadOneAsync(invoice.InvoiceId, null, ct);
        invoice.Status = current.Status == "Pending" ? "Unpaid" : current.Status;
        await unitOfWork.SaveChangesAsync(ct);
    }

    private async Task<InvoiceDto?> ReplayAsync(string key, string hash, CancellationToken ct)
    {
        var request = await repository.GetIssueRequestAsync(key, ct);
        if (request is null) return null;
        if (!string.Equals(request.RequestHash, hash, StringComparison.Ordinal))
            throw new BusinessRuleException("Khóa phát hành đã được dùng cho nội dung khác.");
        return await ReadOneAsync(request.InvoiceId, null, ct);
    }

    private async Task<InvoiceDto> ReadOneAsync(int id, int? customerId, CancellationToken ct)
    {
        var data = await repository.ReadAsync(customerId, id, ct);
        var invoice = data.Invoices.SingleOrDefault() ?? throw new EntityNotFoundException("Không tìm thấy hóa đơn.");
        return InvoiceAccounting.ToDto(invoice, data.Payments, data.Ledgers, data.CancelledDetailIds);
    }

    private async Task AuthorizeEmployeeAsync(int employeeId, string permission, CancellationToken ct)
    {
        var employee = await repository.GetEmployeeAsync(employeeId, ct);
        if (employee is null || !employee.IsActive || !employee.Role.RolePermissions.Any(x => x.Permission.PermissionName == permission))
            throw new UnauthorizedAccessException("Nhân viên hiện không có quyền thao tác hóa đơn.");
    }

    private static IssueInvoiceRequest Normalize(IssueInvoiceRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Kind is not ("Orders" or "Session") || request.OrderIds is null
            || request.OrderIds.Count > 500 || request.OrderIds.Any(x => x <= 0) || request.GamingSessionId is <= 0
            || (request.OrderIds.Count == 0 && request.GamingSessionId is null))
            throw new ArgumentException("Loại nguồn, danh sách đơn hoặc ID phiên không hợp lệ (tối đa 500 đơn).");
        return request with { OrderIds = request.OrderIds.Distinct().Order().ToList() };
    }

    private static void ValidateKey(string key, bool staff)
    {
        if (key is null || !Regex.IsMatch(key, "\\A[A-Za-z0-9:_-]{1,100}\\z", RegexOptions.CultureInvariant)
            || (staff && (key.StartsWith("SessionAutoInvoice:", StringComparison.OrdinalIgnoreCase)
                || key.StartsWith("SessionAutoClose:", StringComparison.OrdinalIgnoreCase))))
            throw new ArgumentException("Khóa phát hành sai định dạng hoặc thuộc tiền tố dành riêng cho hệ thống.");
    }

    private static string Hash(object value) => Convert.ToHexStringLower(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(value)));
    private static void Positive(int id) { if (id <= 0) throw new ArgumentException("ID phải lớn hơn 0."); }
    private static void Money(decimal amount)
    {
        if (amount < 0 || amount > MaxMoney || amount != decimal.Truncate(amount))
            throw new BusinessRuleException("Tiền snapshot phải là nguyên đồng không âm trong giới hạn lưu trữ.");
    }
    private void RequireTransaction()
    {
        if (!unitOfWork.HasActiveTransaction) throw new InvalidOperationException("Phải dùng transaction chung của nguồn hóa đơn.");
    }
}
