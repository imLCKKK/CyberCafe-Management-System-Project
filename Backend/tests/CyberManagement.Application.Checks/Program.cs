using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using CyberManagement.Application.DTOs;
using CyberManagement.Application.Exceptions;
using CyberManagement.Application.Interfaces;
using CyberManagement.Application.Interfaces.Repositories;
using CyberManagement.Application.Services;
using CyberManagement.Domain.Entities;

var checks = new List<(string, Func<Task>)>
{
    ("Inventory request validates type, delta, reason and 64-bit IDs", () =>
    {
        bool Valid(object dto) => Validator.TryValidateObject(dto, new ValidationContext(dto), new List<ValidationResult>(), true);
        True(Valid(new CreateInventoryMovementRequest { ProductId = (long)int.MaxValue + 1, Type = "Receive", QuantityDelta = 10, Reason = "Nhập kho" }));
        True(Valid(new CreateInventoryMovementRequest { ProductId = 1, Type = "Adjust", QuantityDelta = -2, Reason = "Hàng hỏng" }));
        foreach (var type in new[] { "Sale", "Return", "In", "receive", "" })
            True(!Valid(new CreateInventoryMovementRequest { ProductId = 1, Type = type, QuantityDelta = 1, Reason = "Nhập" }));
        foreach (var delta in new[] { 0, -1, 1000001 })
            True(!Valid(new CreateInventoryMovementRequest { ProductId = 1, Type = "Receive", QuantityDelta = delta, Reason = "Nhập" }));
        True(!Valid(new CreateInventoryMovementRequest { ProductId = 0, Type = "Adjust", QuantityDelta = 1, Reason = "Nhập" }));
        foreach (var reason in new[] { "", "   ", new string('a', 201) })
            True(!Valid(new CreateInventoryMovementRequest { ProductId = 1, Type = "Adjust", QuantityDelta = 1, Reason = reason }));
        True(Valid(new UpdateProductStatusRequest { IsActive = false }));
        True(!Valid(new UpdateProductStatusRequest()));
        return Task.CompletedTask;
    }),
    ("Report dates are optional, exact calendar dates and ordered", () =>
    {
        bool Valid(object dto) => Validator.TryValidateObject(dto, new ValidationContext(dto), new List<ValidationResult>(), true);
        True(Valid(new ReportQueryRequest()));
        True(Valid(new ReportQueryRequest { From = "2026-10-01", To = "2026-10-29" }));
        True(Valid(new ReportQueryRequest { To = "2026-10-29" }));
        foreach (var date in new[] { "", "2026-02-30", "29/10/2026", "2026-1-01" })
            True(!Valid(new ReportQueryRequest { From = date }));
        True(!Valid(new ReportQueryRequest { From = "2026-10-29", To = "2026-10-01" }));
        return Task.CompletedTask;
    }),
    ("DTO JSON matches inventory, invoice and report contracts", () =>
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        using var inventory = JsonDocument.Parse(JsonSerializer.Serialize(new InventoryItemDto(), options));
        Equal("categoryId,currentPrice,id,isActive,name,stockQuantity", string.Join(",", inventory.RootElement.EnumerateObject().Select(x => x.Name).Order()));
        using var invoice = JsonDocument.Parse(JsonSerializer.Serialize(new InvoiceResponse { Id = (long)int.MaxValue + 1, IssuedAt = new DateTimeOffset(2026, 10, 29, 0, 0, 0, TimeSpan.FromHours(7)) }, options));
        Equal("customerId,gamingSessionId,grossAmount,id,issuedAt,orderId,paidAmount,refundAmount,status", string.Join(",", invoice.RootElement.EnumerateObject().Select(x => x.Name).Order()));
        Equal((long)int.MaxValue + 1, invoice.RootElement.GetProperty("id").GetInt64());
        True(invoice.RootElement.GetProperty("issuedAt").GetString()!.EndsWith("+07:00"));
        using var report = JsonDocument.Parse(JsonSerializer.Serialize(new ReportResponse(), options));
        Equal("activeSessions,cashRevenue,computers,customers,netRevenue,orders,pendingTopUps,refunds,revenue", string.Join(",", report.RootElement.EnumerateObject().Select(x => x.Name).Order()));
        True(new PaymentDTOs().PaymentTime is null);
        return Task.CompletedTask;
    }),
    ("Invoice statement reads repositories without an order service", async () =>
    {
        var store = new Store(); var user = new User();
        var item = new OrderDetail { OrderDetailId = 10, ProductId = 1, Product = store.Data.Product, Quantity = 2, UnitPrice = 100, Status = "Pending" };
        store.Data.Orders.Add(new Order { OrderId = 1, CustomerId = 1, SessionId = 1, TotalAmount = 200, OrderDetails = [item] });
        store.Data.Orders.Add(new Order { OrderId = 2, CustomerId = 1, SessionId = 2 });
        store.Data.Ledger.Add(new WalletTransactions { CustomerId = 1, SessionId = 1, Type = "GamingCharge", Amount = -50 });
        store.Data.Ledger.Add(new WalletTransactions { CustomerId = 1, OrderId = 1, Type = "OrderDebit", Amount = -200 });
        store.Data.Ledger.Add(new WalletTransactions { CustomerId = 1, OrderDetailId = 10, Type = "OrderItemRefund", Amount = 100 });
        var service = new InvoiceService(new InvoiceRepo(), store, store, user);
        var statement = (await service.GetSessionStatementAsync(1))!;
        Equal(150m, statement.TotalRecorded); Equal(1, statement.Orders.Count);
        Equal(10, statement.Orders[0].Items[0].OrderDetailId); Equal("Pending", statement.Orders[0].Status);
        Equal(1000m, store.Data.Customer.WalletBalance); Equal(10, store.Data.Product.Stock);
        Equal(3, store.Data.Ledger.Count); Equal(0, store.Data.Movements.Count);
        True(await service.GetSessionStatementAsync(999) == null);
        user.CustomerId = 2;
        await Throws<UnauthorizedAccessException>(() => service.GetSessionStatementAsync(1));
    }),
    ("Invoice reads enforce ownership and staff list access", async () =>
    {
        var store = new Store(); var user = new User();
        var service = new InvoiceService(new InvoiceRepo(), store, store, user);
        var invoice = (await service.GetByIdAsync(99))!;
        Equal(7m, invoice.Discount); Equal("Wallet", invoice.PaymentMethod);
        await Throws<UnauthorizedAccessException>(() => service.GetAllAsync());
        user.CustomerId = 2;
        await Throws<UnauthorizedAccessException>(() => service.GetByIdAsync(99));
        user.Role = "Staff"; user.EmployeeId = 8; user.CustomerId = null;
        Equal(0, (await service.GetAllAsync()).Count());
        user.IsActive = false;
        await Throws<UnauthorizedAccessException>(() => service.GetByIdAsync(99));
    }),
    ("Invoice statement rejects another customer's ledger", async () =>
    {
        var store = new Store(); var user = new User();
        store.Data.Ledger.Add(new WalletTransactions { CustomerId = 2, SessionId = 1, Type = "GamingCharge", Amount = -50 });
        var service = new InvoiceService(new InvoiceRepo(), store, store, user);
        await Throws<BusinessRuleException>(() => service.GetSessionStatementAsync(1));
        Equal(1000m, store.Data.Customer.WalletBalance);
    })
};
foreach (var (name, run) in checks) { await run(); Console.WriteLine($"PASS {name}"); }
Console.WriteLine($"{checks.Count} checks passed.");

static void Equal<T>(T expected, T actual) { if (!EqualityComparer<T>.Default.Equals(expected, actual)) throw new Exception($"Expected {expected}, got {actual}"); }
static void True(bool value) { if (!value) throw new Exception("Assertion failed"); }
static async Task Throws<T>(Func<Task> action) where T : Exception
{ try { await action(); } catch (T) { return; } throw new Exception($"Expected {typeof(T).Name}"); }

sealed class User : ICurrentUser
{
    public long AccountId { get; set; } = 1;
    public long? CustomerId { get; set; } = 1;
    public long? EmployeeId { get; set; }
    public string Role { get; set; } = "Customer";
    public bool IsActive { get; set; } = true;
    public bool MustChangePassword { get; set; }
}
sealed class State
{
    public Customers Customer { get; set; } = new() { CustomerId = 1, WalletBalance = 1000 };
    public GamingSessions Session { get; set; } = new() { SessionId = 1, CustomerId = 1, Status = "Active" };
    public Product Product { get; set; } = new() { ProductId = 1, ProductName = "Coffee", Price = 100, Stock = 10, IsActive = true };
    public Product? Product2 { get; set; }
    public List<Order> Orders { get; set; } = new();
    public List<OrderItemCancelRequest> Requests { get; set; } = new();
    public List<WalletTransactions> Ledger { get; set; } = new();
    public List<OrderStatusHistory> History { get; set; } = new();
    public List<InventoryTransaction> Movements { get; set; } = new();
}
// Mô hình transaction trong bộ nhớ kiểm tra logic Application, chưa kiểm chứng mức cô lập trên SQL.
sealed class Store : IOrderRepository, IProductRepository, IOrderStatusHistoryRepository,
    IOrderWorkflowRepository, IInventoryTransactionRepository
{
    public State Data { get; set; } = new();
    public bool FailLedger { get; set; }
    private readonly SemaphoreSlim _gate = new(1);
    public async Task<T> ExecuteAsync<T>(Func<Task<T>> operation)
    {
        await _gate.WaitAsync();
        var before = JsonSerializer.Serialize(Data);
        try { return await operation(); }
        catch { Data = JsonSerializer.Deserialize<State>(before)!; Relink(); throw; }
        finally { _gate.Release(); }
    }
    private void Relink()
    {
        foreach (var item in Data.Orders.SelectMany(o => o.OrderDetails))
        { item.Product = item.ProductId == 1 ? Data.Product : Data.Product2!;
          item.CancellationRequests = Data.Requests.Where(r => r.OrderDetailId == item.OrderDetailId).ToList(); }
    }
    public Task<Customers?> GetCustomerForUpdateAsync(long id) => Task.FromResult(id == Data.Customer.CustomerId ? Data.Customer : null);
    public Task<GamingSessions?> GetSessionForUpdateAsync(long id) => GetSessionAsync(id);
    public Task<GamingSessions?> GetSessionAsync(long id) => Task.FromResult(id == Data.Session.SessionId ? Data.Session : null);
    public Task<Product?> GetProductForUpdateAsync(long id) => Task.FromResult(id == 1 ? Data.Product : id == 2 ? Data.Product2 : null);
    public Task<Order?> GetOrderForUpdateAsync(long id) => GetWithDetailsByIdAsync(id);
    public Task UpdateCustomerAsync(Customers customer) => Task.CompletedTask;
    public Task AddWalletTransactionAsync(WalletTransactions transaction)
    { if (FailLedger) throw new InvalidOperationException("Injected persistence failure"); Data.Ledger.Add(transaction); return Task.CompletedTask; }
    public Task<WalletTransactions?> GetOrderDebitAsync(long id) => Task.FromResult(Data.Ledger.SingleOrDefault(t => t.OrderId == id && t.Type == "OrderDebit"));
    public Task<bool> HasItemRefundAsync(long id) => Task.FromResult(Data.Ledger.Any(t => t.OrderDetailId == id && t.Type == "OrderItemRefund"));
    public Task<IEnumerable<WalletTransactions>> GetSessionWalletTransactionsAsync(long id) => Task.FromResult<IEnumerable<WalletTransactions>>(Data.Ledger.Where(t =>
        t.SessionId == id || Data.Orders.Any(o => o.SessionId == id && (o.OrderId == t.OrderId || o.OrderDetails.Any(i => i.OrderDetailId == t.OrderDetailId)))));
    public Task AddCancellationRequestAsync(OrderItemCancelRequest request)
    { request.RequestId = Data.Requests.Count + 1; Data.Requests.Add(request); Relink(); return Task.CompletedTask; }
    public Task UpdateCancellationRequestAsync(OrderItemCancelRequest request) => Task.CompletedTask;
    public Task<Order?> GetWithDetailsByIdAsync(long id) => Task.FromResult(Data.Orders.SingleOrDefault(o => o.OrderId == id));
    Task<IEnumerable<Order>> IOrderRepository.GetAllAsync() => Task.FromResult<IEnumerable<Order>>(Data.Orders);
    public Task<IEnumerable<Order>> GetByCustomerIdAsync(long id) => Task.FromResult(Data.Orders.Where(o => o.CustomerId == id));
    public Task<Order> AddAsync(Order order)
    { order.OrderId = Data.Orders.Count + 1; var next = Data.Orders.SelectMany(o => o.OrderDetails).Count() + 1;
      foreach (var item in order.OrderDetails) { item.OrderDetailId = next++; item.OrderId = order.OrderId; }
      Data.Orders.Add(order); return Task.FromResult(order); }
    public Task UpdateAsync(Order order) => Task.CompletedTask;
    public Task<IEnumerable<Order>> GetOrdersByStatusAsync(string status) => Task.FromResult(Data.Orders.Where(o => o.Status == status));
    Task<IEnumerable<Product>> IProductRepository.GetAllAsync() => Task.FromResult<IEnumerable<Product>>([Data.Product]);
    Task<Product?> IProductRepository.GetByIdAsync(long id) => GetProductForUpdateAsync(id);
    public Task<IEnumerable<Product>> GetByCategoryIdAsync(long id) => Task.FromResult<IEnumerable<Product>>([]);
    public Task<IEnumerable<Product>> SearchAsync(string keyword) => Task.FromResult<IEnumerable<Product>>([]);
    public Task<Product> AddAsync(Product product) => Task.FromResult(product);
    public Task UpdateAsync(Product product) => Task.CompletedTask;
    public Task DeleteAsync(Product product) => Task.CompletedTask;
    public Task<int> GetStockQuantityAsync(long id) => Task.FromResult(Data.Product.Stock);
    public Task AddAsync(OrderStatusHistory history) { Data.History.Add(history); return Task.CompletedTask; }
    public Task<IEnumerable<OrderStatusHistory>> GetByOrderIdAsync(long id) => Task.FromResult(Data.History.Where(h => h.OrderId == id));
    public Task<IReadOnlyList<InventoryMovementDto>> GetRecentAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<InventoryMovementDto>>(Data.Movements
            .OrderByDescending(m => m.CreatedAt).ThenByDescending(m => m.InvTransId).Take(500)
            .Select(m => new InventoryMovementDto
            {
                Id = m.InvTransId, ProductId = m.ProductId, Type = m.Type,
                QuantityDelta = m.Quantity, StockAfter = m.After, Reason = m.Reason,
                OrderItemId = m.OrderDetailId, RecordedByAccountId = m.RecordedByAccountId,
                RecordedAt = m.CreatedAt
            }).ToList());
    public Task AddAsync(InventoryTransaction movement) { Data.Movements.Add(movement); return Task.CompletedTask; }
    public Task<IEnumerable<InventoryTransaction>> GetByProductIdAsync(long id) => Task.FromResult(Data.Movements.Where(m => m.ProductId == id));
}
sealed class InvoiceRepo : IInvoiceRepository
{
    public Task<Invoice?> GetByIdAsync(long id) => Task.FromResult<Invoice?>(new Invoice
    { InvoiceId = id, CustomerId = 1, SessionId = 1, SubTotal = 100, Discount = 7, TotalAmount = 93, Status = "Paid",
      Payments = [new Payment { Method = "Wallet", Status = "Completed" }],
      InvoiceDetails = [new InvoiceDetail { ItemType = "Food", Quantity = 1, UnitPrice = 100, Amount = 100 }] });
    public Task<IEnumerable<Invoice>> GetAllAsync() => Task.FromResult<IEnumerable<Invoice>>([]);
    public Task AddAsync(Invoice invoice) => throw new Exception("Ordering/statement must not create an invoice");
    public Task<IEnumerable<Invoice>> GetByDateRangeAsync(DateTime from, DateTime to) => Task.FromResult<IEnumerable<Invoice>>([]);
}

