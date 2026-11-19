using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CyberManagement.Application.DTOs.Orders;
using CyberManagement.Application.DTOs.Realtime;
using CyberManagement.Application.Exceptions;
using CyberManagement.Application.Interfaces.Repositories;
using CyberManagement.Application.Interfaces.Services;
using CyberManagement.Application.Validators;
using CyberManagement.Application.Wallets;
using CyberManagement.Domain.Entities;

namespace CyberManagement.Application.Services;

public sealed class OrderService(IOrderRepository repository, IInventoryRepository products,
    IInventoryService inventory, IVoucherService vouchers, IWalletLedgerWriter wallet,
    ISessionSettlement settlement, IInvoiceService invoices, IRealtimePublisher realtime)
{
    public async Task<OrderResult> CreateAsync(OrderActor actor, StaffCreateOrderRequest request,
        string key, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        await AuthorizeAsync(actor, "order.create", ct);
        if (actor.Type == "Staff" && request.PaymentMethod == "Wallet")
            await repository.AuthorizeAsync(actor, "payment.create", ct);
        if (actor.Type == "Customer" && request.CustomerId != actor.Id) throw new UnauthorizedAccessException();
        Positive(request.CustomerId);
        if (actor.Type == "Customer" && request.GamingSessionId is null) throw Invalid("Khách phải chọn phiên đang hoạt động.");
        if (request.GamingSessionId is <= 0) throw Invalid("Mã phiên không hợp lệ.");
        if (request.PaymentMethod is not ("Wallet" or "Cash"))
            throw Invalid("S2-K-1 hỗ trợ Wallet/Cash; checkout ZaloPay thuộc S2-K-2.");
        if (request.Items is null || request.Items.Count is < 1 or > 100
            || request.Items.Any(x => x is null || x.ProductId <= 0 || x.Quantity is < 1 or > 99)
            || request.Items.Select(x => x.ProductId).Distinct().Count() != request.Items.Count)
            throw Invalid("Giỏ có 1–100 món khác nhau, mỗi món có số lượng 1–99.");
        var code = request.VoucherCode is null ? null : VoucherValidator.NormalizeCode(request.VoucherCode);
        var normalized = request with { VoucherCode = code };
        ValidateKey(key);
        var hash = Hash(normalized);
        const string route = "create";
        var result = await repository.ExecuteAsync(async () =>
        {
            var customer = await wallet.LockCustomerAsync(request.CustomerId, ct);
            var replay = await ReplayAsync(actor, route, key, hash, ct);
            if (replay is not null) return replay;
            if (customer.Status != "Active") throw Conflict("CustomerLocked", "Khách hàng không hoạt động.");
            if (request.PaymentMethod == "Wallet") await repository.LockWalletSourcesAsync(customer.CustomerId, ct);
            await repository.ValidateSessionAsync(customer.CustomerId, request.GamingSessionId, ct);
            var now = DateTime.UtcNow;
            // Flush source IDs inside the transaction before downstream writers use them.
            var order = new Order { CustomerId = customer.CustomerId, EmployeeId = actor.EmployeeId,
                ActorType = actor.Type, PaymentMethod = request.PaymentMethod, GamingSessionId = request.GamingSessionId,
                OrderTime = now, LastChangedAtUtc = now };
            repository.Add(order);
            await repository.SaveAsync(ct);
            var locked = (await products.GetProductsForUpdateAsync(request.Items.Select(x => x.ProductId).ToArray(), ct))
                .ToDictionary(x => x.ProductId);
            decimal subtotal = 0;
            for (var i = 0; i < request.Items.Count; i++)
            {
                var item = request.Items[i]; var product = locked[item.ProductId];
                if (!product.IsActive || !await repository.CategoryActiveAsync(product.CategoryId, ct))
                    throw Conflict("ProductUnavailable", "Món hoặc danh mục đã ngừng bán.");
                if (product.Stock < item.Quantity) throw Conflict("InsufficientStock", "Món không đủ tồn kho.");
                if (product.Price is < 0 or > 999_999_999 || decimal.Truncate(product.Price) != product.Price)
                    throw Conflict("InvalidPrice", "Giá món không phải số nguyên VND hợp lệ.");
                subtotal += product.Price * item.Quantity;
                if (subtotal > 999_999_999) throw Invalid("Tổng đơn vượt giới hạn 999999999 VND.");
                order.Details.Add(new OrderDetail { ProductId = product.ProductId, Quantity = item.Quantity,
                    ProductNameSnapshot = product.ProductName, UnitPrice = product.Price, LineNumber = i + 1 });
            }
            order.TotalAmount = subtotal;
            order.StatusHistory.Add(new OrderStatusHistory { ActorType = actor.Type, ChangedBy = actor.EmployeeId,
                NewStatus = "Pending", ChangedAt = now, Reason = "Created" });
            await repository.SaveAsync(ct);
            // Inventory takes only already-held product locks; voucher follows products.
            foreach (var detail in order.Details.OrderBy(x => x.ProductId))
                await inventory.ApplySaleAsync(detail.OrderDetailId, detail.ProductId, detail.Quantity,
                    actor.EmployeeId, $"OrderSale:{order.OrderId}:{detail.OrderDetailId}", ct);
            if (code is not null)
            {
                try { await vouchers.ValidateAndUseAsync(order.OrderId, code, customer.CustomerId, ct); }
                catch (EntityNotFoundException) { throw Conflict("VoucherInvalid", "Mã voucher không tồn tại."); }
                catch (BusinessRuleException error)
                {
                    throw Conflict(error.Message.Contains("lượt", StringComparison.Ordinal) ? "VoucherLimitExceeded" : "VoucherInvalid", error.Message);
                }
            }
            if (order.TotalAmount > 0 && request.PaymentMethod == "Wallet")
            {
                await settlement.BeforeWalletDebitAsync(customer.CustomerId, now, ct);
                if (customer.WalletBalance < order.TotalAmount)
                    throw Conflict("InsufficientWalletBalance", "Số dư sau quyết toán phí phiên không đủ thanh toán món.");
                var source = order.OrderId.ToString(CultureInfo.InvariantCulture);
                var operationKey = $"OrderPayment:{source}";
                var debit = await wallet.RecordAsync(new WalletMutation(customer.CustomerId, WalletMutationKind.Debit,
                    order.TotalAmount, "OrderPayment", "Order", source, operationKey,
                    new WalletActor(actor.Type, actor.EmployeeId), now), ct);
                repository.AddPayment(new Payment { OrderId = order.OrderId, Amount = order.TotalAmount, Method = "Wallet",
                    Status = "Succeeded", WalletTransaction = debit.Entry, OperationKey = operationKey,
                    CreatedAt = now, PaymentTime = now, ConfirmedAt = now, PaymentTimeBasis = "Confirmation" });
            }
            SaveOperation(actor, route, key, hash, order.OrderId, now);
            await repository.SaveAsync(ct);
            // There may be no invoice yet. Never issue or collect a second time here.
            await invoices.ApplyFinancialChangeAsync(order.OrderId, null, ct);
            return new OrderResult(await repository.ReadAsync(order.OrderId, Owner(actor), ct), false);
        }, ct);
        await PublishAsync(result, request.PaymentMethod == "Wallet", ct);
        return result;
    }

    public async Task<OrderDto> GetAsync(OrderActor actor, int orderId, CancellationToken ct = default)
    {
        Positive(orderId); await AuthorizeAsync(actor, "order.view", ct);
        return await repository.ReadAsync(orderId, Owner(actor), ct);
    }

    public async Task<OrderPageDto> ListAsync(OrderActor actor, int page, int pageSize, string? status,
        int? customerId, CancellationToken ct = default)
    {
        await AuthorizeAsync(actor, "order.view", ct);
        if (page < 1 || pageSize is < 1 or > 100 || (long)(page - 1) * pageSize > int.MaxValue
            || customerId is <= 0 || (status is not null && status is not ("Pending" or "Confirmed" or "Mixed" or "Cancelled" or "AwaitingPayment" or "Expired")))
            throw Invalid("Bộ lọc hoặc phân trang không hợp lệ.");
        return await repository.ReadPageAsync(Owner(actor) ?? customerId, status, page, pageSize, ct);
    }

    public Task<OrderResult> ConfirmAsync(OrderActor actor, int orderId, int detailId, OrderVersionRequest request,
        string key, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request); Positive(detailId);
        return ChangeAsync(actor, orderId, $"confirm:{orderId}:{detailId}", "order.status.update", request.ExpectedVersion,
            key, Hash(request), async (order, now) =>
            {
                var detail = order.Details.SingleOrDefault(x => x.OrderDetailId == detailId)
                    ?? throw new EntityNotFoundException("Dòng món không thuộc đơn này.");
                var summary = await repository.ReadAsync(orderId, null, ct);
                if (summary.Payment.OutstandingAmount > 0 || summary.Payment.PaymentStatus is not ("Paid" or "NotRequired" or "PartiallyRefunded"))
                    throw Conflict("PaymentRequired", "Phải thanh toán đủ trước khi nhận món.");
                try
                {
                    var verified = InvoiceAccounting.Totals(order.CustomerId, [orderId], [],
                        await repository.PaymentsAsync(orderId, ct), await repository.LedgersAsync(orderId, ct));
                    if (verified.Net < summary.PayableAmount)
                        throw Conflict("PaymentRequired", "Khoản thu chưa khớp số tiền phải trả.");
                }
                catch (BusinessRuleException error)
                { throw Conflict("PaymentRequired", error.Message); }
                if (detail.Status != "Pending" || order.Status is not ("Pending" or "Mixed"))
                    throw Conflict("InvalidOrderTransition", "Dòng món không còn chờ nhận.");
                detail.Status = "Confirmed"; detail.ConfirmedAtUtc = now; detail.ConfirmedByEmployeeId = actor.Id;
                var old = order.Status;
                order.Status = order.Details.All(x => x.Status == "Confirmed") ? "Confirmed" : "Mixed";
                order.StatusHistory.Add(new OrderStatusHistory { OldStatus = old, NewStatus = order.Status,
                    ChangedBy = actor.Id, ActorType = "Staff", OrderDetailId = detailId, ChangedAt = now, Reason = "ItemConfirmed" });
            }, ct);
    }

    public Task<OrderResult> CollectCashAsync(OrderActor actor, int orderId, CashCollectionRequest request,
        string key, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!request.CashReceived) throw Invalid("Chỉ xác nhận sau khi thực nhận đủ tiền mặt.");
        return ChangeAsync(actor, orderId, $"cash:{orderId}", "payment.create", request.ExpectedVersion,
            key, Hash(request), async (order, now) =>
            {
                var payments = await repository.PaymentsAsync(orderId, ct);
                var summary = await repository.ReadAsync(orderId, null, ct);
                if (order.PaymentMethod != "Cash" || order.Status is not ("Pending" or "Mixed")
                    || !order.Details.Any(x => x.Status == "Pending")
                    || order.Details.Any(x => x.Status is not ("Pending" or "Cancelled"))
                    || payments.Count != 0 || summary.Payment.OutstandingAmount <= 0)
                    throw Conflict("InvalidOrderTransition", "Đơn không còn đủ điều kiện thu tiền mặt.");
                repository.AddPayment(new Payment { OrderId = orderId, Method = "Cash", Status = "Succeeded",
                    Amount = summary.Payment.OutstandingAmount, OperationKey = $"OrderPayment:{orderId}",
                    CreatedAt = now, PaymentTime = now, ConfirmedAt = now, PaymentTimeBasis = "Confirmation",
                    Reason = $"CashReceived:Staff:{actor.Id}" });
                order.StatusHistory.Add(new OrderStatusHistory { OldStatus = order.Status, NewStatus = order.Status,
                    ChangedBy = actor.Id, ActorType = "Staff", ChangedAt = now, Reason = "CashReceived" });
            }, ct);
    }

    private async Task<OrderResult> ChangeAsync(OrderActor actor, int orderId, string route, string permission,
        string version, string key, string hash, Func<Order, DateTime, Task> change, CancellationToken ct)
    {
        Positive(orderId); await repository.AuthorizeAsync(actor, permission, ct);
        ValidateKey(key); var expected = ParseVersion(version);
        var customerId = await repository.FindCustomerIdAsync(orderId, null, ct);
        var result = await repository.ExecuteAsync(async () =>
        {
            await wallet.LockCustomerAsync(customerId, ct);
            var replay = await ReplayAsync(actor, route, key, hash, ct);
            if (replay is not null) return replay;
            await repository.LockRelatedSessionAsync(orderId, ct);
            var order = await repository.LockAsync(orderId, ct) ?? throw new EntityNotFoundException("Không tìm thấy đơn.");
            if (!order.Version.SequenceEqual(expected)) throw Conflict("OrderVersionConflict", "Đơn đã thay đổi, cần đọc lại.");
            var now = DateTime.UtcNow;
            await change(order, now);
            order.LastChangedAtUtc = now;
            SaveOperation(actor, route, key, hash, orderId, now);
            await repository.SaveAsync(ct);
            if (route.StartsWith("cash:", StringComparison.Ordinal)) await invoices.ApplyFinancialChangeAsync(orderId, null, ct);
            return new OrderResult(await repository.ReadAsync(orderId, null, ct), false);
        }, ct);
        await PublishAsync(result, false, ct);
        return result;
    }

    private Task AuthorizeAsync(OrderActor actor, string staffPermission, CancellationToken ct) =>
        repository.AuthorizeAsync(actor, actor.Type == "Customer" ? null : staffPermission, ct);

    private async Task<OrderResult?> ReplayAsync(OrderActor actor, string route, string key, string hash, CancellationToken ct)
    {
        var existing = await repository.FindOperationAsync(actor, route, key, ct);
        if (existing is null) return null;
        if (existing.RequestHash != hash) throw Conflict("IdempotencyConflict", "Khóa đã dùng với nội dung khác.");
        return new(await repository.ReadAsync(existing.OrderId, Owner(actor), ct), true);
    }

    private void SaveOperation(OrderActor actor, string route, string key, string hash, int orderId, DateTime now) =>
        repository.AddOperation(new OrderOperation { ActorType = actor.Type, ActorId = actor.Id, Route = route,
            Key = key, RequestHash = hash, OrderId = orderId, CreatedAtUtc = now });

    private async Task PublishAsync(OrderResult result, bool walletChanged, CancellationToken ct)
    {
        if (result.Replayed) return;
        var message = new RealtimeEvent(Guid.NewGuid(), "OrderChanged", result.Order.Id, DateTimeOffset.UtcNow);
        await realtime.PublishToPermissionAsync("order.view", message, CancellationToken.None);
        await realtime.PublishToAccountAsync(RealtimeAccountType.Customer, result.Order.CustomerId, message, CancellationToken.None);
        if (walletChanged && result.Order.OriginalPayable > 0)
        {
            var walletEvent = new RealtimeEvent(Guid.NewGuid(), "WalletChanged", result.Order.CustomerId, DateTimeOffset.UtcNow);
            await realtime.PublishToPermissionAsync("wallet.view", walletEvent, CancellationToken.None);
            await realtime.PublishToAccountAsync(RealtimeAccountType.Customer, result.Order.CustomerId, walletEvent, CancellationToken.None);
        }
    }

    private static int? Owner(OrderActor actor) => actor.Type == "Customer" ? actor.Id : null;
    private static string Hash<T>(T value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(value))));
    private static void Positive(int id) { if (id <= 0) throw Invalid("ID phải là số nguyên dương."); }
    private static void ValidateKey(string key)
    {
        if (key is null || key.Length is < 8 or > 100 || key.Any(c => !(char.IsAsciiLetterOrDigit(c) || c is '.' or '_' or ':' or '-')))
            throw Invalid("Idempotency-Key cần 8–100 ký tự ASCII chữ, số hoặc ._:-.");
    }
    private static byte[] ParseVersion(string version)
    {
        try { var bytes = Convert.FromBase64String(version ?? ""); if (bytes.Length == 8) return bytes; }
        catch (FormatException) { }
        throw Invalid("expectedVersion phải là rowversion base64 8 byte.");
    }
    private static ValidationException Invalid(string message) => new(message);
    private static OrderRuleException Conflict(string code, string message) => new(code, message);
}
