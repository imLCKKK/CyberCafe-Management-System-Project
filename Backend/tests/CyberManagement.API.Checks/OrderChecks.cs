using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using CyberManagement.API;
using CyberManagement.API.Controller;
using CyberManagement.Application;
using CyberManagement.Application.DTOs.Identity;
using CyberManagement.Application.DTOs.Invoices;
using CyberManagement.Application.DTOs.Orders;
using CyberManagement.Application.DTOs.Realtime;
using CyberManagement.Application.Interfaces.Services;
using CyberManagement.Application.Security;
using CyberManagement.Domain.Entities;
using CyberManagement.Infrastructure;
using CyberManagement.Infrastructure.Persistance;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

internal static class OrderChecks
{
    public static async Task<int> RunAsync(string? connection)
    {
        if (string.IsNullOrWhiteSpace(connection)) throw new ArgumentException("--orders-only requires AUTH_CHECKS_SQL_CONNECTION.");
        var fixture = new AuthCheckDatabase(connection);
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions {
            EnvironmentName = "Development", ApplicationName = typeof(CyberManagement.API.Program).Assembly.FullName });
        builder.WebHost.UseTestServer(); builder.Logging.ClearProviders();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?> {
            ["Authentication:Jwt:SigningKey"] = "order-check-only-signing-key-at-least-32-bytes",
            ["ConnectionStrings:DefaultConnection"] = connection });
        builder.Services.AddInfrastructureServices(builder.Configuration);
        var failure = new OrderSaveFailure();
        builder.Services.AddDbContext<ApplicationDbContext>(options => { fixture.Configure(options); options.AddInterceptors(failure); });
        builder.Services.AddApplicationServices(); builder.Services.AddApiServices(builder.Configuration);
        var events = new OrderEvents(); builder.Services.AddSingleton<IRealtimePublisher>(events);
        builder.Services.AddControllers().AddApplicationPart(typeof(AuthController).Assembly);
        await using var app = builder.Build();
        const string password = "Orders-check-only-2026!";
        var checks = 0;
        int customerA = 0, customerB = 0, poorId = 0, productId = 0, secondId = 0, sessionId = 0;
        try
        {
            await using (var scope = app.Services.CreateAsyncScope())
            {
                var db = Db(scope); await fixture.InitializeAsync(db);
                await scope.ServiceProvider.GetRequiredService<IIdentitySeeder>().SeedAsync(new IdentitySeedOptions(true,
                    RolePermissionCatalog.Roles.ToDictionary(x => x.Name, _ => (string?)password)), default);
                var customers = new[] {
                    new Customer { FullName = "Order A", Phone = "0900091001", WalletBalance = 1000000 },
                    new Customer { FullName = "Order B", Phone = "0900091002", WalletBalance = 1000000 },
                    new Customer { FullName = "Order poor", Phone = "0900091003", WalletBalance = 0 } };
                foreach (var account in customers) account.PasswordHash = scope.ServiceProvider.GetRequiredService<ICustomerPasswordHasher>().HashPassword(account, password);
                var category = new Category { CategoryName = "Order checks" };
                var food = new Product { ProductName = "Order food", Category = category, Price = 10000, Stock = 100 };
                var second = new Product { ProductName = "Second food", Category = category, Price = 5000, Stock = 100 };
                var session = new GamingSession { Customer = customers[0], StartTime = DateTime.UtcNow.AddSeconds(-60),
                    PricePerHourSnapshot = 10000, HourlyRateSnapshot = 10000,
                    Computer = new Computer { ComputerName = "Order PC", Status = "Occupied",
                        Room = new ComputerRoom { RoomName = "Order room" },
                        ComputerType = new ComputerType { TypeName = "Order type", PricePerHour = 10000 } } };
                db.AddRange(customers); db.AddRange(food, second, session);
                db.Promotions.AddRange(Promo("ORDER25", "Percent", 25), Promo("ORDERFREE", "Percent", 100),
                    Promo("LASTUSE", "Fixed", 1000, limit: 1), Promo("EXPIRED", "Fixed", 1000, expired: true));
                await db.SaveChangesAsync();
                customerA = customers[0].CustomerId; customerB = customers[1].CustomerId; poorId = customers[2].CustomerId;
                productId = food.ProductId; secondId = second.ProductId; sessionId = session.GamingSessionId;
                Check("Model and migrations synchronized", !db.Database.HasPendingModelChanges());
            }
            app.UseExceptionHandler(); app.UseAuthentication(); app.UseAuthorization(); app.MapControllers(); await app.StartAsync();
            using var client = app.GetTestClient();
            var admin = await Login("admin"); var waiter = await Login("waiter01"); var cashier = await Login("cashier01");
            var customer = await Login("0900091001", customer: true); var other = await Login("0900091002", customer: true);

            var body = new CreateOrderRequest(sessionId, [new(productId, 2), new(secondId, 1)], "Wallet", " order25 ");
            using var create = await Send(HttpMethod.Post, "/api/client/orders", customer, body, "create-original");
            await Status("Customer wallet + voucher", HttpStatusCode.Created, create);
            var order = (await create.Content.ReadFromJsonAsync<OrderDto>())!;
            Check("Server computes original snapshots", order.Subtotal == 25000 && order.DiscountAmount == 6250
                && order.OriginalPayable == 18750 && order.Items.Sum(x => x.DiscountAmount) == 6250
                && order.CreatedByEmployeeId is null && order.Payment.PaymentStatus == "Paid" && order.Status == "Pending");
            await using (var scope = app.Services.CreateAsyncScope())
            {
                var db = Db(scope);
                var fee = await db.SessionCharges.SumAsync(x => x.Amount);
                Check("Session fee before order debit; one committed ledger/payment", fee >= 167
                    && (await db.Customers.FindAsync(customerA))!.WalletBalance == 1000000 - fee - 18750
                    && await db.Payments.CountAsync(x => x.OrderId == order.Id) == 1
                    && await db.WalletTransactions.CountAsync(x => x.SourceType == "Order") == 1
                    && await db.InventoryTransactions.CountAsync(x => x.OrderId == order.Id) == 2);
                var ledger = await db.WalletTransactions.SingleAsync(x => x.SourceType == "Order");
                Check("Customer audit actor without fictitious employee", ledger.ActorType == "Customer" && ledger.ActorEmployeeId == null);
            }
            var beforeReplay = await Snapshot();
            using (var replay = await Send(HttpMethod.Post, "/api/client/orders", customer, body, "create-original"))
            {
                await Status("Replay returns current order", HttpStatusCode.OK, replay);
                Check("Replay header and same resource", replay.Headers.Contains("Idempotency-Replayed")
                    && (await replay.Content.ReadFromJsonAsync<OrderDto>())!.Id == order.Id);
            }
            Check("Replay does not settle fees or write again", beforeReplay == await Snapshot());
            await Expect("Key payload conflict", HttpStatusCode.Conflict, HttpMethod.Post, "/api/client/orders", customer,
                body with { Items = [new(productId, 1)] }, "create-original");
            await Expect("Own order hidden from other customer", HttpStatusCode.NotFound, HttpMethod.Get, $"/api/client/orders/{order.Id}", other);
            await Expect("Client cannot spoof price", HttpStatusCode.BadRequest, HttpMethod.Post, "/api/client/orders", customer,
                new { gamingSessionId = sessionId, items = new[] { new { productId, quantity = 1, unitPrice = 1 } }, paymentMethod = "Cash" }, "tampered-price");
            await Expect("Customer cannot use another session", HttpStatusCode.Conflict, HttpMethod.Post, "/api/client/orders", other, body, "wrong-session");
            await Expect("Staff identity cannot use client endpoint", HttpStatusCode.Forbidden, HttpMethod.Post, "/api/client/orders", admin, body, "wrong-principal");
            await Expect("Customer cannot use staff endpoint", HttpStatusCode.Forbidden, HttpMethod.Post, "/api/admin/orders", customer, Staff(customerA), "wrong-principal");
            await Expect("Missing key", HttpStatusCode.BadRequest, HttpMethod.Post, "/api/admin/orders", admin, Staff(customerA));
            await Expect("Invalid page", HttpStatusCode.BadRequest, HttpMethod.Get, "/api/admin/orders?page=0", admin);
            await Expect("Waiter cannot spend wallet", HttpStatusCode.Forbidden, HttpMethod.Post, "/api/admin/orders", waiter, Staff(customerA, "Wallet"), "waiter-wallet");
            await Expect("Poor customer wallet rejected", HttpStatusCode.Conflict, HttpMethod.Post, "/api/admin/orders", admin, Staff(poorId, "Wallet"), "poor-wallet");
            await Expect("Invalid voucher rolls back", HttpStatusCode.Conflict, HttpMethod.Post, "/api/admin/orders", admin, Staff(customerB, code: "MISSING"), "missing-code");
            await Expect("Expired voucher rolls back", HttpStatusCode.Conflict, HttpMethod.Post, "/api/admin/orders", admin, Staff(customerB, code: "EXPIRED"), "expired-code");
            await Expect("Duplicate products rejected", HttpStatusCode.BadRequest, HttpMethod.Post, "/api/admin/orders", admin,
                Staff(customerB) with { Items = [new(productId, 1), new(productId, 2)] }, "duplicate-products");
            await using (var scope = app.Services.CreateAsyncScope())
                Check("Failed orders left no source or HTTP key", await Db(scope).Orders.CountAsync() == 1 && await Db(scope).Set<OrderOperation>().CountAsync() == 1);

            decimal savedBalance;
            await using (var scope = app.Services.CreateAsyncScope())
            {
                var db = Db(scope); var owner = (await db.Customers.FindAsync(customerA))!;
                savedBalance = owner.WalletBalance; owner.WalletBalance = 10000;
                await db.SaveChangesAsync();
            }
            // Accrue a completed second without corrupting the saved session/charge timeline.
            await Task.Delay(TimeSpan.FromMilliseconds(1100));
            var beforeFeeFailure = await Snapshot();
            var beforeFeeFailureEvents = events.Messages.Count;
            await Expect("Counter wallet order accounts for active session fees", HttpStatusCode.Conflict, HttpMethod.Post,
                "/api/admin/orders", admin, Staff(customerA, "Wallet"), "insufficient-after-fees");
            Check("Insufficient balance after fees rolls back fee and checkout", beforeFeeFailure == await Snapshot());
            Check("Rolled-back session settlement publishes no events", beforeFeeFailureEvents == events.Messages.Count);
            await using (var scope = app.Services.CreateAsyncScope())
            { var db = Db(scope); (await db.Customers.FindAsync(customerA))!.WalletBalance = 1; await db.SaveChangesAsync(); }
            var beforeAutoCloseFailure = await Snapshot();
            await Expect("Exhausted session cannot fund an order", HttpStatusCode.Conflict, HttpMethod.Post,
                "/api/admin/orders", admin, Staff(customerA, "Wallet"), "exhausted-session-order");
            Check("Rejected order rolls back auto-close, invoice, fees and stock", beforeAutoCloseFailure == await Snapshot());
            Check("Rolled-back auto-close publishes no events", beforeFeeFailureEvents == events.Messages.Count);
            await using (var scope = app.Services.CreateAsyncScope())
            { var db = Db(scope); (await db.Customers.FindAsync(customerA))!.WalletBalance = savedBalance; await db.SaveChangesAsync(); }

            await CheckClosingSessionRemainder(atCounter: true);
            await CheckClosingSessionRemainder(atCounter: false);

            // Issue an unpaid food invoice through the existing module, then collect once.
            var cash = await CreateStaff(Staff(customerB), "create-cash");
            await Expect("Unpaid cash cannot be confirmed", HttpStatusCode.Conflict, HttpMethod.Post, ConfirmRoute(cash), waiter,
                new OrderVersionRequest(cash.Version), "confirm-unpaid");
            await Expect("Waiter cannot collect cash", HttpStatusCode.Forbidden, HttpMethod.Post, CashRoute(cash), waiter,
                new CashCollectionRequest(cash.Version, true), "waiter-collect");
            using var invoiceResponse = await Send(HttpMethod.Post, "/api/admin/invoices", admin,
                new IssueInvoiceRequest { Kind = "Orders", OrderIds = [cash.Id] }, "issue-order-cash");
            await Status("Existing invoice module reads order snapshots", HttpStatusCode.Created, invoiceResponse);
            var invoice = (await invoiceResponse.Content.ReadFromJsonAsync<InvoiceDto>())!;
            var collection = new CashCollectionRequest(cash.Version, true);
            using var paid = await Send(HttpMethod.Post, CashRoute(cash), cashier, collection, "cash-collection");
            await Status("Cashier confirms actual receipt", HttpStatusCode.OK, paid);
            cash = (await paid.Content.ReadFromJsonAsync<OrderDto>())!;
            Check("Cash stays Pending until waiter receives", cash.Status == "Pending" && cash.Payment.PaymentStatus == "Paid");
            await Expect("Cash collection replay before stale version", HttpStatusCode.OK, HttpMethod.Post, CashRoute(cash), cashier, collection, "cash-collection");
            await Expect("Different key cannot collect twice", HttpStatusCode.Conflict, HttpMethod.Post, CashRoute(cash), cashier,
                new CashCollectionRequest(cash.Version, true), "cash-duplicate");
            using (var readInvoice = await Send(HttpMethod.Get, $"/api/admin/invoices/{invoice.Id}", admin))
            {
                await Status("Invoice updated in cash transaction", HttpStatusCode.OK, readInvoice);
                Check("Invoice links the same payment", (await readInvoice.Content.ReadFromJsonAsync<InvoiceDto>())!.PaidAmount == cash.PayableAmount);
            }
            var confirmBody = new OrderVersionRequest(cash.Version);
            using (var confirmed = await Send(HttpMethod.Post, ConfirmRoute(cash), waiter, confirmBody, "confirm-paid"))
            {
                await Status("Waiter receives fully paid food", HttpStatusCode.OK, confirmed);
                var value = (await confirmed.Content.ReadFromJsonAsync<OrderDto>())!;
                Check("Rowversion advances after item confirmation", value.Version != cash.Version && value.Status == "Confirmed");
            }
            await Expect("Confirmation replay", HttpStatusCode.OK, HttpMethod.Post, ConfirmRoute(cash), waiter, confirmBody, "confirm-paid");
            await Expect("Stale confirmation rejected", HttpStatusCode.Conflict, HttpMethod.Post, ConfirmRoute(cash), waiter, confirmBody, "confirm-stale");
            var free = await CreateStaff(Staff(poorId, "Wallet", "ORDERFREE"), "create-free");
            Check("Fully discounted order creates no payment", free.Payment.PaymentStatus == "NotRequired" && free.Payment.PaymentId == null);
            await Expect("Free food can be received", HttpStatusCode.OK, HttpMethod.Post, ConfirmRoute(free), waiter,
                new OrderVersionRequest(free.Version), "confirm-free");

            await using (var scope = app.Services.CreateAsyncScope())
            { var db = Db(scope); (await db.Payments.SingleAsync(x => x.OrderId == order.Id)).Amount++; await db.SaveChangesAsync(); }
            await Expect("Paid wallet status still requires matching ledger", HttpStatusCode.Conflict, HttpMethod.Post,
                ConfirmRoute(order), waiter, new OrderVersionRequest(order.Version), "confirm-bad-ledger");
            await using (var scope = app.Services.CreateAsyncScope())
            { var db = Db(scope); (await db.Payments.SingleAsync(x => x.OrderId == order.Id)).Amount--; await db.SaveChangesAsync(); }
            await Expect("Verified wallet food can be received", HttpStatusCode.OK, HttpMethod.Post,
                ConfirmRoute(order), waiter, new OrderVersionRequest(order.Version), "confirm-wallet");

            // Inject failure after source IDs, stock and voucher flushes, before final commit.
            var beforeFailure = await Snapshot(); var eventsBeforeFailure = events.Messages.Count;
            failure.FailNext = true;
            await Expect("Failure after intermediate flush", HttpStatusCode.InternalServerError, HttpMethod.Post, "/api/admin/orders", admin,
                Staff(customerB, "Wallet", "ORDER25"), "rollback-operation");
            Check("Failure rolls back sources, stock, money, voucher and key", beforeFailure == await Snapshot());
            Check("Rollback publishes no events", eventsBeforeFailure == events.Messages.Count);
            await CreateStaff(Staff(customerB, "Wallet", "ORDER25"), "rollback-operation");

            var concurrent = await Task.WhenAll(Enumerable.Range(0, 5).Select(_ => Send(HttpMethod.Post, "/api/admin/orders", admin,
                Staff(customerB), "concurrent-same-key")));
            try
            {
                Check("Same-key contention creates once", concurrent.Count(x => x.StatusCode == HttpStatusCode.Created) == 1
                    && concurrent.Count(x => x.StatusCode == HttpStatusCode.OK) == 4);
                var ids = new List<int>(); foreach (var response in concurrent) ids.Add((await response.Content.ReadFromJsonAsync<OrderDto>())!.Id);
                Check("Same-key contention returns one ID", ids.Distinct().Count() == 1);
            }
            finally { foreach (var response in concurrent) response.Dispose(); }
            await SetStock(1);
            var stockRace = await Task.WhenAll(Send(HttpMethod.Post, "/api/admin/orders", admin, Staff(customerA), "last-stock-a"),
                Send(HttpMethod.Post, "/api/admin/orders", admin, Staff(customerB), "last-stock-b"));
            try { Check("Last stock has one winner", stockRace.Count(x => x.StatusCode == HttpStatusCode.Created) == 1
                && stockRace.Count(x => x.StatusCode == HttpStatusCode.Conflict) == 1); }
            finally { foreach (var response in stockRace) response.Dispose(); }
            await SetStock(10);
            var voucherRace = await Task.WhenAll(Send(HttpMethod.Post, "/api/admin/orders", admin, Staff(customerA, code: "LASTUSE"), "last-voucher-a"),
                Send(HttpMethod.Post, "/api/admin/orders", admin, Staff(customerB, code: "LASTUSE"), "last-voucher-b"));
            try { Check("Last voucher has one winner", voucherRace.Count(x => x.StatusCode == HttpStatusCode.Created) == 1
                && voucherRace.Count(x => x.StatusCode == HttpStatusCode.Conflict) == 1); }
            finally { foreach (var response in voucherRace) response.Dispose(); }
            await using (var scope = app.Services.CreateAsyncScope())
            {
                var db = Db(scope);
                Check("Quota loser did not deduct stock", (await db.Products.FindAsync(productId))!.Stock == 9);
                var original = await db.Products.FindAsync(productId); original!.Price = 999; original.ProductName = "Changed name";
                var promo = await db.Promotions.SingleAsync(x => x.Code == "ORDER25"); promo.DiscountValue = 1;
                var owner = await db.Customers.FindAsync(customerA); owner!.Status = "Locked";
                await db.SaveChangesAsync();
            }
            using (var historical = await Send(HttpMethod.Post, "/api/client/orders", customer, body, "create-original"))
            {
                await Status("Locked customer can replay committed order", HttpStatusCode.OK, historical);
                var value = (await historical.Content.ReadFromJsonAsync<OrderDto>())!;
                Check("Snapshot survives menu and voucher edits", value.Items[0].ProductNameSnapshot == "Order food"
                    && value.Items[0].UnitPrice == 10000 && value.Voucher!.DiscountValue == 25);
            }
            await Expect("Locked customer cannot create new order", HttpStatusCode.Conflict, HttpMethod.Post, "/api/client/orders", customer, body, "locked-new-order");
            using (var own = await Send(HttpMethod.Get, "/api/client/orders", other))
            {
                await Status("Own paginated list", HttpStatusCode.OK, own);
                Check("List contains only own orders", (await own.Content.ReadFromJsonAsync<OrderPageDto>())!.Items.All(x => x.CustomerId == customerB));
            }
            await using (var scope = app.Services.CreateAsyncScope())
            {
                var db = Db(scope); var employee = await db.Employees.SingleAsync(x => x.Username == "admin"); employee.IsActive = false; await db.SaveChangesAsync();
            }
            await Expect("Disabled staff denied with existing JWT", HttpStatusCode.Forbidden, HttpMethod.Get, "/api/admin/orders", admin);
            await Expect("Disabled staff cannot replay", HttpStatusCode.Forbidden, HttpMethod.Post, "/api/admin/orders", admin, Staff(customerB), "create-cash");
            await VerifyHistoricalUpgradeAsync(connection);
            Check("Upgrade preserves historical order, actor and line snapshots", true);
            return checks;

            StaffCreateOrderRequest Staff(int id, string method = "Cash", string? code = null) => new(id, null, [new(productId, 1)], method, code);
            string CashRoute(OrderDto value) => $"/api/admin/orders/{value.Id}/cash-collections";
            string ConfirmRoute(OrderDto value) => $"/api/admin/orders/{value.Id}/items/{value.Items[0].Id}/confirm";
            async Task CheckClosingSessionRemainder(bool atCounter)
            {
                var label = atCounter ? "Counter" : "Session-attached";
                var phone = atCounter ? "0900091010" : "0900091011";
                int ownerId, sourceSessionId, foodId;
                await using (var scope = app.Services.CreateAsyncScope())
                {
                    var db = Db(scope);
                    var owner = new Customer { FullName = label + " remainder", Phone = phone, WalletBalance = 4 };
                    owner.PasswordHash = scope.ServiceProvider.GetRequiredService<ICustomerPasswordHasher>().HashPassword(owner, password);
                    var food = new Product { ProductName = label + " one dong", Price = 1, Stock = 5,
                        Category = new Category { CategoryName = label + " remainder food" } };
                    var source = new GamingSession { Customer = owner, StartTime = DateTime.UtcNow.AddSeconds(-10),
                        PricePerHourSnapshot = 10000, HourlyRateSnapshot = 10000,
                        Computer = new Computer { ComputerName = label + " remainder PC", Status = "Occupied",
                            Room = new ComputerRoom { RoomName = label + " remainder room" },
                            ComputerType = new ComputerType { TypeName = label + " remainder type", PricePerHour = 10000 } } };
                    db.AddRange(food, source); await db.SaveChangesAsync();
                    ownerId = owner.CustomerId; sourceSessionId = source.GamingSessionId; foodId = food.ProductId;
                }
                var token = atCounter ? admin : await Login(phone, customer: true);
                var before = await Snapshot(); var eventCount = events.Messages.Count;
                object request = atCounter
                    ? new StaffCreateOrderRequest(ownerId, null, [new(foodId, 1)], "Wallet", "ORDER25")
                    : new CreateOrderRequest(sourceSessionId, [new(foodId, 1)], "Wallet", "ORDER25");
                await Expect(label + " rejects checkout after auto-close despite affordable one-dong food",
                    HttpStatusCode.Conflict, HttpMethod.Post, atCounter ? "/api/admin/orders" : "/api/client/orders",
                    token, request, "closed-session-remainder-" + (atCounter ? "counter" : "client"));
                Check(label + " rollback preserves session, wallet, stock, voucher, invoice and HTTP key", before == await Snapshot());
                Check(label + " rejected checkout emits no events", events.Messages.Count == eventCount);
                // The normal worker may commit the same auto-close independently of a failed debit.
                await using (var scope = app.Services.CreateAsyncScope())
                    await scope.ServiceProvider.GetRequiredService<CyberManagement.Application.Interfaces.Repositories.ISessionRepository>()
                        .BillAsync(ownerId, default);
                await using (var scope = app.Services.CreateAsyncScope())
                {
                    var db = Db(scope); var source = (await db.GamingSessions.FindAsync(sourceSessionId))!;
                    Check(label + " independent worker bills three dong, closes and retains one dong",
                        source.Status == "Ended" && source.TotalCost == 3 && source.ClosingInvoiceId != null
                        && (await db.Customers.FindAsync(ownerId))!.WalletBalance == 1
                        && (await db.Products.FindAsync(foodId))!.Stock == 5
                        && !await db.Orders.AnyAsync(x => x.CustomerId == ownerId));
                }
            }
            async Task<OrderDto> CreateStaff(StaffCreateOrderRequest request, string key)
            {
                using var response = await Send(HttpMethod.Post, "/api/admin/orders", admin, request, key);
                await Status("Create " + key, HttpStatusCode.Created, response);
                return (await response.Content.ReadFromJsonAsync<OrderDto>())!;
            }
            async Task<string> Login(string username, bool customer = false)
            {
                using var response = await client.PostAsJsonAsync(customer ? "/api/auth/customer/login" : "/api/auth/login",
                    customer ? (object)new { phone = username, password } : new { username, password });
                await Status("Login " + username, HttpStatusCode.OK, response);
                using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
                return json.RootElement.GetProperty("accessToken").GetString()!;
            }
            async Task<HttpResponseMessage> Send(HttpMethod method, string path, string? token, object? request = null, string? key = null)
            {
                using var message = new HttpRequestMessage(method, path);
                if (token is not null) message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                if (key is not null) message.Headers.Add("Idempotency-Key", key);
                if (request is not null) message.Content = JsonContent.Create(request);
                return await client.SendAsync(message);
            }
            async Task Expect(string name, HttpStatusCode expected, HttpMethod method, string path, string? token, object? request = null, string? key = null)
            { using var response = await Send(method, path, token, request, key); await Status(name, expected, response); }
            async Task Status(string name, HttpStatusCode expected, HttpResponseMessage response)
            {
                if (response.StatusCode != expected) throw new Exception($"{name}: expected {expected}, got {response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
                Check(name, true);
            }
        }
        finally { await app.StopAsync(); await fixture.CleanupAsync(); }

        void Check(string name, bool condition)
        { if (!condition) throw new Exception("FAIL: " + name); checks++; Console.WriteLine("PASS: " + name); }
        async Task<string> Snapshot()
        {
            await using var scope = app.Services.CreateAsyncScope(); var db = Db(scope);
            return JsonSerializer.Serialize(new { orders = await db.Orders.CountAsync(), details = await db.OrderDetails.CountAsync(),
                payments = await db.Payments.CountAsync(), usages = await db.PromotionUsages.CountAsync(),
                ledger = await db.WalletTransactions.CountAsync(), movements = await db.InventoryTransactions.CountAsync(),
                charges = await db.SessionCharges.CountAsync(), histories = await db.OrderStatusHistories.CountAsync(),
                sessions = await db.GamingSessions.OrderBy(x => x.GamingSessionId).Select(x => new {
                    x.TotalCost, x.LastChargedAt, x.EndTime, x.Status, x.ClosingInvoiceId }).ToListAsync(),
                invoices = await db.Invoices.CountAsync(), audit = await db.AuditLogs.CountAsync(),
                computers = await db.Computers.OrderBy(x => x.ComputerId).Select(x => x.Status).ToListAsync(),
                operations = await db.Set<OrderOperation>().CountAsync(), stocks = await db.Products.OrderBy(x => x.ProductId).Select(x => x.Stock).ToListAsync(),
                balances = await db.Customers.OrderBy(x => x.CustomerId).Select(x => x.WalletBalance).ToListAsync() });
        }
        async Task SetStock(int stock)
        { await using var scope = app.Services.CreateAsyncScope(); var db = Db(scope); (await db.Products.FindAsync(productId))!.Stock = stock; await db.SaveChangesAsync(); }
    }

    private static ApplicationDbContext Db(AsyncServiceScope scope) => scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    private static async Task VerifyHistoricalUpgradeAsync(string connection)
    {
        var fixture = new AuthCheckDatabase(connection);
        var options = new DbContextOptionsBuilder<ApplicationDbContext>(); fixture.Configure(options);
        await using var db = new ApplicationDbContext(options.Options);
        try
        {
            await fixture.InitializeAsync(db);
            await db.GetService<IMigrator>().MigrateAsync("20261117130111_AddProductImageUrl");
            var customer = new Customer { FullName = "Historical order", Phone = "0900091999" };
            var employee = new Employee { Username = "historical", FullName = "Historical staff", PasswordHash = "fixture-no-login",
                Role = new Role { RoleName = "Historical role" } };
            var food = new Product { ProductName = "Historical food", Price = 1234, Stock = 9,
                Category = new Category { CategoryName = "Historical category" } };
            db.AddRange(customer, employee, food); await db.SaveChangesAsync();
            var when = new DateTime(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc);
            await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO [Orders] ([CustomerId],[EmployeeId],[OrderTime],[Status],[TotalAmount],[DiscountAmount]) VALUES ({customer.CustomerId},{employee.EmployeeId},{when},N'Pending',{1234m},{0m})");
            var id = await db.Orders.Select(x => x.OrderId).SingleAsync();
            await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO [OrderDetails] ([OrderId],[ProductId],[Quantity],[UnitPrice],[ProductNameSnapshot],[DiscountAmount],[Status]) VALUES ({id},{food.ProductId},1,{1234m},N'Historical food',{0m},N'Pending')");
            await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO [OrderStatusHistory] ([OrderId],[NewStatus],[ChangedBy],[ChangedAt]) VALUES ({id},N'Pending',{employee.EmployeeId},{when})");
            var script = db.GetService<IMigrator>().GenerateScript("20261117130111_AddProductImageUrl",
                options: MigrationsSqlGenerationOptions.Idempotent);
            await db.Database.OpenConnectionAsync();
            try
            {
                // Exercise the exported upgrade script twice, including the data backfill.
                for (var attempt = 0; attempt < 2; attempt++)
                    foreach (var batch in System.Text.RegularExpressions.Regex.Split(script, @"^\s*GO\s*$",
                        System.Text.RegularExpressions.RegexOptions.Multiline | System.Text.RegularExpressions.RegexOptions.IgnoreCase))
                        if (!string.IsNullOrWhiteSpace(batch)) await db.Database.ExecuteSqlRawAsync(batch);
            }
            finally { await db.Database.CloseConnectionAsync(); }
            db.ChangeTracker.Clear();
            var order = await db.Orders.Include(x => x.Details).Include(x => x.StatusHistory).SingleAsync();
            if (order.EmployeeId != employee.EmployeeId || order.ActorType != "Staff" || order.PaymentMethod != null
                || order.TotalAmount != 1234 || order.LastChangedAtUtc != when || order.Version.Length != 8
                || order.Details.Single().LineNumber != 1 || order.StatusHistory.Single().ActorType != "Staff")
                throw new Exception("Historical checkout migration did not preserve source values.");
        }
        finally { await fixture.CleanupAsync(); }
    }
    private static Promotion Promo(string code, string type, decimal value, int? limit = null, bool expired = false) => new()
    {
        Code = code, PromoName = code, DiscountType = type, DiscountValue = value, TotalUsageLimit = limit,
        StartDate = DateTimeOffset.UtcNow.AddDays(-2), EndDate = DateTimeOffset.UtcNow.AddDays(expired ? -1 : 2), IsActive = true
    };

    private sealed class OrderSaveFailure : SaveChangesInterceptor
    {
        public bool FailNext { get; set; }
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (FailNext && eventData.Context!.ChangeTracker.Entries<OrderOperation>().Any(x => x.State == EntityState.Added))
            { FailNext = false; throw new InvalidOperationException("Injected order final-save failure."); }
            return ValueTask.FromResult(result);
        }
    }
    private sealed class OrderEvents : IRealtimePublisher
    {
        public ConcurrentQueue<RealtimeEvent> Messages { get; } = new();
        public Task PublishToPermissionAsync(string permission, RealtimeEvent message, CancellationToken ct) { Messages.Enqueue(message); return Task.CompletedTask; }
        public Task PublishToAccountAsync(RealtimeAccountType type, int id, RealtimeEvent message, CancellationToken ct) { Messages.Enqueue(message); return Task.CompletedTask; }
        public Task PublishTournamentChangedAsync(RealtimeEvent message, CancellationToken ct) => Task.CompletedTask;
    }
}
