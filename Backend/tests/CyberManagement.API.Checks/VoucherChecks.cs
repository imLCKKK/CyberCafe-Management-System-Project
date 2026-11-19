using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using CyberManagement.API;
using CyberManagement.API.Controller;
using CyberManagement.API.Realtime;
using CyberManagement.Application.DTOs.Realtime;
using CyberManagement.Application;
using CyberManagement.Application.DTOs.Identity;
using CyberManagement.Application.DTOs.Vouchers;
using CyberManagement.Application.Exceptions;
using CyberManagement.Application.Interfaces.Repositories;
using CyberManagement.Application.Interfaces.Services;
using CyberManagement.Application.Security;
using CyberManagement.Domain.Entities;
using CyberManagement.Infrastructure;
using CyberManagement.Infrastructure.Persistance;
using CyberManagement.Infrastructure.Repositories;
using CyberManagement.Infrastructure.Security;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

// SQL thật để kiểm tra khóa, transaction, quota và snapshot; chỉ dùng DB sinh riêng.
internal static class VoucherChecks
{
    public static async Task<int> RunAsync(string? connection)
    {
        if (string.IsNullOrWhiteSpace(connection))
            throw new ArgumentException("--voucher-only requires AUTH_CHECKS_SQL_CONNECTION.");
        var fixture = new AuthCheckDatabase(connection);
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = "Development",
            ApplicationName = typeof(CyberManagement.API.Program).Assembly.FullName
        });
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Authentication:Jwt:SigningKey"] = "voucher-check-only-signing-key-at-least-32-bytes",
            ["ConnectionStrings:DefaultConnection"] = connection
        });
        builder.Services.AddInfrastructureServices(builder.Configuration);
        builder.Services.AddDbContext<ApplicationDbContext>(fixture.Configure);
        builder.Services.AddApplicationServices();
        builder.Services.AddApiServices(builder.Configuration);
        builder.Services.AddControllers().AddApplicationPart(typeof(AuthController).Assembly);
        await using var app = builder.Build();
        const string password = "Voucher-check-only-2026!";
        var checks = 0;
        int customerA = 0, customerB = 0, employeeId = 0, productId = 0, pausedId = 0, smallId = 0;
        try
        {
            await using (var scope = app.Services.CreateAsyncScope())
            {
                var services = scope.ServiceProvider;
                var db = services.GetRequiredService<ApplicationDbContext>();
                await fixture.InitializeAsync(db);
                await services.GetRequiredService<IIdentitySeeder>().SeedAsync(new IdentitySeedOptions(true,
                    RolePermissionCatalog.Roles.ToDictionary(x => x.Name, _ => (string?)password)), default);
                var customers = new[]
                {
                    new Customer { FullName = "Voucher A", Phone = "0900032001" },
                    new Customer { FullName = "Voucher B", Phone = "0900032002" }
                };
                foreach (var customer in customers)
                    customer.PasswordHash = services.GetRequiredService<ICustomerPasswordHasher>().HashPassword(customer, password);
                db.Customers.AddRange(customers);
                var category = new Category { CategoryName = "Voucher checks" };
                var products = new[]
                {
                    new Product { ProductName = "Voucher food", Category = category, Price = 100000, Stock = 20, IsActive = true },
                    new Product { ProductName = "Paused food", Category = category, Price = 100, IsActive = false },
                    new Product { ProductName = "Rounding food", Category = category, Price = 5, IsActive = true }
                };
                db.Products.AddRange(products);
                // Một role ngoài catalog chỉ có permission quản lý.
                var customRole = new Role { RoleName = "VoucherOperator" };
                customRole.RolePermissions.Add(new RolePermission
                {
                    Permission = await db.Permissions.SingleAsync(x => x.PermissionName == "promotion.manage")
                });
                var customEmployee = new Employee { Username = "voucher-operator", FullName = "Voucher Operator", Role = customRole };
                customEmployee.PasswordHash = services.GetRequiredService<IEmployeePasswordHasher>().HashPassword(customEmployee, password);
                db.Employees.Add(customEmployee);
                await db.SaveChangesAsync();
                customerA = customers[0].CustomerId;
                customerB = customers[1].CustomerId;
                productId = products[0].ProductId;
                pausedId = products[1].ProductId;
                smallId = products[2].ProductId;
                employeeId = await db.Employees.Where(x => x.Username == "admin").Select(x => x.EmployeeId).SingleAsync();
            }
            app.UseExceptionHandler();
            app.UseAuthentication();
            app.UseAuthorization();
            app.MapControllers();
            app.MapHub<RealtimeHub>(RealtimeHub.Endpoint);
            await app.StartAsync();
            using var client = app.GetTestClient();
            client.BaseAddress = new Uri("https://localhost");
            var tokens = new Dictionary<string, string>();
            foreach (var user in new[] { "admin", "manager01", "cashier01", "waiter01", "tech01", "voucher-operator" })
            {
                using var response = await client.PostAsJsonAsync("/api/auth/login", new { username = user, password });
                await Status("Login " + user, HttpStatusCode.OK, response);
                using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
                tokens[user] = json.RootElement.GetProperty("accessToken").GetString()!;
            }
            using var customerLogin = await client.PostAsJsonAsync("/api/auth/customer/login", new { phone = "0900032001", password });
            await Status("Customer login", HttpStatusCode.OK, customerLogin);
            using var customerJson = JsonDocument.Parse(await customerLogin.Content.ReadAsStringAsync());
            var customerToken = customerJson.RootElement.GetProperty("accessToken").GetString()!;
            var admin = tokens["admin"];
            const string route = "/api/admin/vouchers";
            await ExpectHttp("Anonymous list", HttpStatusCode.Unauthorized, HttpMethod.Get, route, null);
            foreach (var (user, token) in tokens)
                await ExpectHttp(user + " list permission", user is "waiter01" or "tech01" or "voucher-operator"
                    ? HttpStatusCode.Forbidden : HttpStatusCode.OK, HttpMethod.Get, route, token);
            await ExpectHttp("Customer cannot list admin vouchers", HttpStatusCode.Forbidden, HttpMethod.Get, route, customerToken);
            foreach (var user in new[] { "cashier01", "waiter01", "tech01" })
                await ExpectHttp(user + " cannot create", HttpStatusCode.Forbidden, HttpMethod.Post, route, tokens[user], Body("DENIED"));
            await ExpectHttp("Customer cannot create", HttpStatusCode.Forbidden, HttpMethod.Post, route, customerToken, Body("DENIED"));

            {
                await using var adminPeer = await RealtimeTestPeer.ConnectAsync(app.GetTestServer(), admin);
                await using var cashierPeer = await RealtimeTestPeer.ConnectAsync(app.GetTestServer(), tokens["cashier01"]);
                await using var waiterPeer = await RealtimeTestPeer.ConnectAsync(app.GetTestServer(), tokens["waiter01"]);
                await using var customerPeer = await RealtimeTestPeer.ConnectAsync(app.GetTestServer(), customerToken);
                var created = await Create(Body("EVENTCHECK"));
                await Changed(created.Id);
                await ExpectHttp("Update emits after commit", HttpStatusCode.OK, HttpMethod.Put,
                    route + "/" + created.Id, admin, Body(created.Code) with { Name = "Event saved" });
                await Changed(created.Id);
                await ExpectHttp("Toggle emits after commit", HttpStatusCode.OK, HttpMethod.Put,
                    route + "/" + created.Id + "/status", admin, new { isActive = false });
                await Changed(created.Id);
                await using (var scope = app.Services.CreateAsyncScope())
                    Check("Toggle persisted in fresh DbContext",
                        await Db(scope).Promotions.AsNoTracking().AnyAsync(x => x.PromotionId == created.Id && !x.IsActive && x.PromoName == "Event saved"));
                await ExpectHttp("Duplicate emits no event", HttpStatusCode.Conflict,
                    HttpMethod.Post, route, admin, Body(created.Code));
                await ExpectHttp("Invalid input emits no event", HttpStatusCode.BadRequest,
                    HttpMethod.Post, route, admin, Body("INVALIDEVENT") with { DiscountValue = 101 });
                await ExpectHttp("Forbidden write emits no event", HttpStatusCode.Forbidden,
                    HttpMethod.Put, route + "/" + created.Id + "/status", tokens["cashier01"], new { isActive = true });
                await Quiet(adminPeer, RealtimeAccountType.Staff, employeeId);
                var cashierId = int.Parse(new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler().ReadJwtToken(tokens["cashier01"]).Subject);
                var waiterId = int.Parse(new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler().ReadJwtToken(tokens["waiter01"]).Subject);
                await Quiet(cashierPeer, RealtimeAccountType.Staff, cashierId);
                await Quiet(waiterPeer, RealtimeAccountType.Staff, waiterId);
                await Quiet(customerPeer, RealtimeAccountType.Customer, customerA);

                async Task Changed(int id)
                {
                    var message = await adminPeer.NextAsync();
                    var other = await cashierPeer.NextAsync();
                    Check("Admin and read-only cashier receive permission-scoped invalidation",
                        message.GetProperty("target").GetString() == RealtimeEvent.PromotionChanged
                        && other.GetProperty("target").GetString() == RealtimeEvent.PromotionChanged);
                    var payload = message.GetProperty("arguments")[0];
                    Check("Voucher event has only invalidation metadata",
                        payload.EnumerateObject().Select(x => x.Name).ToHashSet().SetEquals(["eventId", "type", "entityId", "occurredAtUtc"])
                        && payload.GetProperty("entityId").GetInt32() == id
                        && payload.GetProperty("eventId").GetGuid() == other.GetProperty("arguments")[0].GetProperty("eventId").GetGuid());
                    await using var scope = app.Services.CreateAsyncScope();
                    Check("Voucher is visible after event", await Db(scope).Promotions.AsNoTracking().AnyAsync(x => x.PromotionId == id));
                }
                async Task Quiet(RealtimeTestPeer peer, RealtimeAccountType type, int id)
                {
                    await using var scope = app.Services.CreateAsyncScope();
                    await scope.ServiceProvider.GetRequiredService<IRealtimePublisher>().PublishToAccountAsync(type, id,
                        new RealtimeEvent(Guid.NewGuid(), "VoucherCheckBarrier", id, DateTimeOffset.UtcNow), default);
                    Check("Failed writes emit nothing; non-viewers receive no voucher event",
                        (await peer.NextAsync()).GetProperty("target").GetString() == "VoucherCheckBarrier");
                }
            }

            var voucher = await Create(Body(" sale10 "));
            Check("Code normalized; null caps preserved", voucher.Code == "SALE10" && voucher.MaxDiscountAmount is null && voucher.TotalUsageLimit is null);
            await ExpectHttp("Case-insensitive duplicate", HttpStatusCode.Conflict, HttpMethod.Post, route, admin, Body("sale10"));
            _ = await Create(Body("MANAGER"), tokens["manager01"]);
            _ = await Create(Body("CUSTOMROLE"), tokens["voucher-operator"]);
            foreach (var invalid in new[]
            {
                Body("BAD CODE"), Body("MISSINGNAME") with { Name = " " }, Body("TYPE") with { DiscountType = "Other" },
                Body("P0") with { DiscountValue = 0 }, Body("P101") with { DiscountValue = 101 },
                Body("PRECISION") with { DiscountValue = 10.001m }, Body("MIN") with { MinOrderAmount = -1 },
                Body("CAP") with { MaxDiscountAmount = 0 }, Body("FRACTION") with { MinOrderAmount = 0.5m },
                Body("LIMIT") with { TotalUsageLimit = 0 }, Body("CUSTOMLIMIT") with { PerCustomerUsageLimit = -1 },
                Body("DATES") with { EndDate = DateTimeOffset.UtcNow.AddDays(-2) },
                Body("FIXED") with { DiscountType = "Fixed", DiscountValue = 0.5m }
            }) await ExpectHttp("Invalid voucher " + invalid.Code, HttpStatusCode.BadRequest, HttpMethod.Post, route, admin, invalid);
            await ExpectHttp("Missing required fields", HttpStatusCode.BadRequest, HttpMethod.Post, route, admin, new { code = "MISSING" });
            await ExpectHttp("Unknown request field", HttpStatusCode.BadRequest, HttpMethod.Post, route, admin,
                new { code = "EXTRA", subtotal = 1 });
            await ExpectHttp("Missing voucher", HttpStatusCode.NotFound, HttpMethod.Get, route + "/2147483647", admin);
            await ExpectHttp("Invalid voucher ID", HttpStatusCode.BadRequest, HttpMethod.Get, route + "/0", admin);
            await ExpectHttp("Manager updates", HttpStatusCode.OK, HttpMethod.Put, route + "/" + voucher.Id,
                tokens["manager01"], Body(voucher.Code) with { Name = "Updated voucher" });
            foreach (var user in new[] { "cashier01", "waiter01", "tech01" })
            {
                await ExpectHttp(user + " cannot update", HttpStatusCode.Forbidden, HttpMethod.Put, route + "/" + voucher.Id, tokens[user], Body(voucher.Code));
                await ExpectHttp(user + " cannot toggle", HttpStatusCode.Forbidden, HttpMethod.Put, route + "/" + voucher.Id + "/status", tokens[user], new { isActive = false });
            }
            await ExpectHttp("Missing status", HttpStatusCode.BadRequest, HttpMethod.Put, route + "/" + voucher.Id + "/status", admin, new { });
            await ExpectHttp("Hard delete absent", HttpStatusCode.MethodNotAllowed, HttpMethod.Delete, route + "/" + voucher.Id, admin);

            var previewBody = new PreviewVoucherRequest { Code = voucher.Code, Items = [new() { ProductId = productId, Quantity = 2 }] };
            const string previewRoute = "/api/client/vouchers/preview";
            await ExpectHttp("Anonymous preview", HttpStatusCode.Unauthorized, HttpMethod.Post, previewRoute, null, previewBody);
            await ExpectHttp("Employee cannot preview as customer", HttpStatusCode.Forbidden, HttpMethod.Post, previewRoute, admin, previewBody);
            var preview = await Preview(previewBody);
            Check("Preview uses server price", preview.Subtotal == 200000 && preview.DiscountAmount == 20000 && preview.TotalAmount == 180000);
            await ExpectHttp("Cannot inject subtotal/customer", HttpStatusCode.BadRequest, HttpMethod.Post, previewRoute, customerToken,
                new { code = voucher.Code, items = new[] { new { productId, quantity = 1 } }, subtotal = 1, customerId = customerB });
            await ExpectHttp("Empty cart", HttpStatusCode.BadRequest, HttpMethod.Post, previewRoute, customerToken, previewBody with { Items = [] });
            await ExpectHttp("Null cart item", HttpStatusCode.BadRequest, HttpMethod.Post, previewRoute, customerToken,
                new { code = voucher.Code, items = new object?[] { null } });
            await ExpectHttp("Missing product", HttpStatusCode.NotFound, HttpMethod.Post, previewRoute, customerToken,
                previewBody with { Items = [new() { ProductId = int.MaxValue, Quantity = 1 }] });
            await ExpectHttp("Paused product", HttpStatusCode.Conflict, HttpMethod.Post, previewRoute, customerToken,
                previewBody with { Items = [new() { ProductId = pausedId, Quantity = 1 }] });
            await ExpectHttp("Zero quantity", HttpStatusCode.BadRequest, HttpMethod.Post, previewRoute, customerToken,
                previewBody with { Items = [new() { ProductId = productId, Quantity = 0 }] });
            var capped = await Create(Body("CAPPED") with { MaxDiscountAmount = 10000 });
            Check("Percent cap", (await Preview(previewBody with { Code = capped.Code })).DiscountAmount == 10000);
            var fixedVoucher = await Create(Body("FIXEDBIG") with { DiscountType = "Fixed", DiscountValue = 300000 });
            Check("Fixed clamped to subtotal", (await Preview(previewBody with { Code = fixedVoucher.Code })).DiscountAmount == 200000);
            Check("Half dong rounds up", (await Preview(previewBody with { Items = [new() { ProductId = smallId, Quantity = 1 }] })).DiscountAmount == 1);
            var expired = await Create(Body("EXPIRED") with { StartDate = DateTimeOffset.UtcNow.AddDays(-3), EndDate = DateTimeOffset.UtcNow.AddDays(-1) });
            var future = await Create(Body("FUTURE") with { StartDate = DateTimeOffset.UtcNow.AddDays(1), EndDate = DateTimeOffset.UtcNow.AddDays(2) });
            var threshold = await Create(Body("MINIMUM") with { MinOrderAmount = 300000 });
            foreach (var code in new[] { expired.Code, future.Code, threshold.Code })
                await ExpectHttp("Unavailable " + code, HttpStatusCode.Conflict, HttpMethod.Post, previewRoute, customerToken, previewBody with { Code = code });
            await ExpectHttp("Toggle off", HttpStatusCode.OK, HttpMethod.Put, route + "/" + voucher.Id + "/status", admin, new { isActive = false });
            await ExpectHttp("Disabled preview", HttpStatusCode.Conflict, HttpMethod.Post, previewRoute, customerToken, previewBody);
            await ExpectHttp("Toggle on", HttpStatusCode.OK, HttpMethod.Put, route + "/" + voucher.Id + "/status", admin, new { isActive = true });
            await using (var scope = app.Services.CreateAsyncScope())
                Check("Preview never creates usages/orders", !await Db(scope).PromotionUsages.AnyAsync() && !await Db(scope).Orders.AnyAsync());

            var allocation = await Create(Body("ALLOCATION") with { DiscountType = "Fixed", DiscountValue = 1 });
            var orderId = await NewOrder(customerA, 1m, 1m, 1m);
            var applied = await Apply(orderId, allocation.Code, customerA);
            Check("Stable allocation of remaining dong", applied.DiscountAmount == 1 && applied.Lines[0].DiscountAmount == 1
                && applied.Lines.Skip(1).All(x => x.DiscountAmount == 0));
            await using (var scope = app.Services.CreateAsyncScope())
            {
                var db = Db(scope);
                var order = await db.Orders.AsNoTracking().SingleAsync(x => x.OrderId == orderId);
                Check("Order snapshot and net total saved", order.VoucherCodeSnapshot == allocation.Code && order.DiscountAmount == 1 && order.TotalAmount == 2);
                Check("One usage saved", await db.PromotionUsages.CountAsync(x => x.OrderId == orderId) == 1);
            }
            await ExpectHttp("Used voucher code immutable", HttpStatusCode.Conflict, HttpMethod.Put, route + "/" + allocation.Id,
                admin, Body("RENAMED") with { DiscountType = "Fixed", DiscountValue = 1 });
            await ExpectHttp("Change rule after use", HttpStatusCode.OK, HttpMethod.Put, route + "/" + allocation.Id,
                admin, Body(allocation.Code) with { DiscountType = "Fixed", DiscountValue = 99, IsActive = false });
            var replay = await Apply(orderId, allocation.Code, customerA);
            Check("Retry keeps original snapshot despite rule edit", replay.DiscountValue == 1 && replay.DiscountAmount == 1
                && replay.Lines.SequenceEqual(applied.Lines));
            await Throws<BusinessRuleException>("Cannot switch voucher", () => Apply(orderId, voucher.Code, customerA));
            await Throws<UnauthorizedAccessException>("Cannot use another customer's order", () => Apply(orderId, allocation.Code, customerB));
            await using (var scope = app.Services.CreateAsyncScope())
                await Throws<InvalidOperationException>("Apply requires caller transaction", () => Service(scope).ValidateAndUseAsync(orderId, allocation.Code, customerA));
            await using (var scope = app.Services.CreateAsyncScope())
                await Throws<BusinessRuleException>("Partial/not-cancelled order does not release", () => Uow(scope).ExecuteAsync(() => Service(scope).ReleaseForCancelledOrderAsync(orderId)));
            Check("Full cancellation releases once", await Cancel(orderId));
            Check("Repeated cancellation does not release twice", !await Cancel(orderId));
            await Throws<BusinessRuleException>("Released order cannot reuse voucher", () => Apply(orderId, allocation.Code, customerA));
            await ExpectHttp("History remains immutable after release", HttpStatusCode.Conflict, HttpMethod.Put, route + "/" + allocation.Id,
                admin, Body("RENAMEDAFTERRELEASE"));
            await using (var scope = app.Services.CreateAsyncScope())
                Check("Release preserves snapshot", (await Db(scope).Orders.AsNoTracking().SingleAsync(x => x.OrderId == orderId)).DiscountAmount == 1);

            var expiryVoucher = await Create(Body("EXPIRY") with { TotalUsageLimit = 1, PerCustomerUsageLimit = 1 });
            var expiryOrder = await NewOrder(customerA, 100m, 50m);
            await Apply(expiryOrder, expiryVoucher.Code, customerA);
            Order expirySnapshot;
            PromotionUsage usageBeforeExpiry;
            await using (var scope = app.Services.CreateAsyncScope())
            {
                var db = Db(scope);
                var order = await db.Orders.SingleAsync(x => x.OrderId == expiryOrder);
                order.Status = "AwaitingPayment";
                await db.SaveChangesAsync();
                expirySnapshot = await db.Orders.AsNoTracking().Include(x => x.Details).SingleAsync(x => x.OrderId == expiryOrder);
                usageBeforeExpiry = await db.PromotionUsages.AsNoTracking().SingleAsync(x => x.OrderId == expiryOrder);
                await Throws<BusinessRuleException>("AwaitingPayment must not release before expiry",
                    () => Uow(scope).ExecuteAsync(() => Service(scope).ReleaseForCancelledOrderAsync(expiryOrder)));
            }
            var replacementOrder = await NewOrder(customerA, 100m);
            await Throws<BusinessRuleException>("Reserved gateway order occupies last voucher slot",
                () => Apply(replacementOrder, expiryVoucher.Code, customerA));
            Check("Expired gateway order releases its voucher", await Expire(expiryOrder));
            DateTimeOffset? firstReleasedAt;
            await using (var scope = app.Services.CreateAsyncScope())
            {
                var db = Db(scope);
                var usage = await db.PromotionUsages.AsNoTracking().SingleAsync(x => x.OrderId == expiryOrder);
                firstReleasedAt = usage.ReleasedAt;
                Check("Expiry retains usage identity and original usage time", usage.PromotionUsageId == usageBeforeExpiry.PromotionUsageId
                    && usage.UsedAt == usageBeforeExpiry.UsedAt && usage.ReleasedAt >= usage.UsedAt);
                Check("Expiry frees global and personal quota", !await db.PromotionUsages.AnyAsync(x => x.PromotionId == expiryVoucher.Id && x.ReleasedAt == null));
                var order = await db.Orders.AsNoTracking().Include(x => x.Details).SingleAsync(x => x.OrderId == expiryOrder);
                Check("Expiry preserves Expired status and all discount snapshots", order.Status == "Expired"
                    && order.VoucherCodeSnapshot == expirySnapshot.VoucherCodeSnapshot
                    && order.VoucherDiscountTypeSnapshot == expirySnapshot.VoucherDiscountTypeSnapshot
                    && order.VoucherDiscountValueSnapshot == expirySnapshot.VoucherDiscountValueSnapshot
                    && order.DiscountAmount == expirySnapshot.DiscountAmount && order.TotalAmount == expirySnapshot.TotalAmount
                    && order.Details.OrderBy(x => x.OrderDetailId).Select(x => (x.OrderDetailId, x.UnitPrice, x.Quantity, x.ProductNameSnapshot, x.DiscountAmount))
                        .SequenceEqual(expirySnapshot.Details.OrderBy(x => x.OrderDetailId).Select(x => (x.OrderDetailId, x.UnitPrice, x.Quantity, x.ProductNameSnapshot, x.DiscountAmount))));
            }
            Check("Repeated expiry does not release again", !await Expire(expiryOrder));
            await using (var scope = app.Services.CreateAsyncScope())
                Check("Expiry retry retains the first release timestamp", (await Db(scope).PromotionUsages.AsNoTracking()
                    .SingleAsync(x => x.OrderId == expiryOrder)).ReleasedAt == firstReleasedAt);
            await Throws<BusinessRuleException>("Expired released order cannot reapply its voucher",
                () => Apply(expiryOrder, expiryVoucher.Code, customerA));
            await Apply(replacementOrder, expiryVoucher.Code, customerA);
            Check("Another order can use the returned global and personal slot", true);
            Check("Late expiry retry does not release the replacement order slot", !await Expire(expiryOrder));
            await using (var scope = app.Services.CreateAsyncScope())
                Check("Replacement usage stays active and expired history remains", await Db(scope).PromotionUsages
                    .CountAsync(x => x.PromotionId == expiryVoucher.Id) == 2 && await Db(scope).PromotionUsages
                    .CountAsync(x => x.PromotionId == expiryVoucher.Id && x.OrderId == replacementOrder && x.ReleasedAt == null) == 1);

            var expiryRollbackVoucher = await Create(Body("EXPIRYROLLBACK") with { TotalUsageLimit = 1 });
            var expiryRollbackOrder = await NewOrder(customerA, 100m);
            await Apply(expiryRollbackOrder, expiryRollbackVoucher.Code, customerA);
            await using (var scope = app.Services.CreateAsyncScope())
            {
                var order = await Db(scope).Orders.SingleAsync(x => x.OrderId == expiryRollbackOrder);
                order.Status = "AwaitingPayment"; await Db(scope).SaveChangesAsync();
            }
            await Throws<CheckRollback>("Caller rollback undoes expiry and voucher release",
                () => Expire(expiryRollbackOrder, rollback: true));
            await using (var scope = app.Services.CreateAsyncScope())
                Check("Rolled-back expiry retains reservation and source state", (await Db(scope).Orders.AsNoTracking()
                    .SingleAsync(x => x.OrderId == expiryRollbackOrder)).Status == "AwaitingPayment"
                    && await Db(scope).PromotionUsages.AnyAsync(x => x.OrderId == expiryRollbackOrder && x.ReleasedAt == null));
            Check("Expiry can succeed after caller rollback", await Expire(expiryRollbackOrder));

            var personal = await Create(Body("PERSONAL") with { PerCustomerUsageLimit = 1 });
            var personalA = await NewOrder(customerA, 100m);
            await Apply(personalA, personal.Code, customerA);
            var personalRetry = await NewOrder(customerA, 100m);
            await Throws<BusinessRuleException>("Per-customer quota", () => Apply(personalRetry, personal.Code, customerA));
            await Apply(await NewOrder(customerB, 100m), personal.Code, customerB);
            Check("Different customer has separate quota", true);
            await Cancel(personalA);
            await Apply(personalRetry, personal.Code, customerA);
            Check("Released slot becomes available", true);

            var rollbackVoucher = await Create(Body("ROLLBACK"));
            var rollbackOrder = await NewOrder(customerA, 100m);
            await Throws<CheckRollback>("Caller rollback undoes voucher", async () =>
            {
                await using var scope = app.Services.CreateAsyncScope();
                await Uow(scope).ExecuteAsync<int>(async () =>
                {
                    await Service(scope).ValidateAndUseAsync(rollbackOrder, rollbackVoucher.Code, customerA);
                    throw new CheckRollback();
                });
            });
            await using (var scope = app.Services.CreateAsyncScope())
                Check("Rollback removes both usage and snapshot", !await Db(scope).PromotionUsages.AnyAsync(x => x.OrderId == rollbackOrder)
                    && (await Db(scope).Orders.AsNoTracking().SingleAsync(x => x.OrderId == rollbackOrder)).VoucherCodeSnapshot is null);

            var withinTx = await Create(Body("SAMETX") with { TotalUsageLimit = 1 });
            var sameOrders = new[] { await NewOrder(customerA, 100m), await NewOrder(customerA, 100m) };
            await Throws<BusinessRuleException>("Same transaction counts its saved usage", async () =>
            {
                await using var scope = app.Services.CreateAsyncScope();
                await Uow(scope).ExecuteAsync(async () =>
                {
                    await Service(scope).ValidateAndUseAsync(sameOrders[0], withinTx.Code, customerA);
                    return await Service(scope).ValidateAndUseAsync(sameOrders[1], withinTx.Code, customerA);
                });
            });
            await using (var scope = app.Services.CreateAsyncScope())
                Check("Quota failure rolls back whole operation", !await Db(scope).PromotionUsages.AnyAsync(x => sameOrders.Contains(x.OrderId)));

            var last = await Create(Body("LAST") with { TotalUsageLimit = 1 });
            var competing = new[] { await NewOrder(customerA, 100m), await NewOrder(customerB, 100m) };
            var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var tasks = competing.Select((id, i) => Task.Run(async () =>
            {
                await start.Task;
                try { await Apply(id, last.Code, i == 0 ? customerA : customerB); return true; }
                catch (BusinessRuleException) { return false; }
            })).ToArray();
            start.SetResult();
            var winners = await Task.WhenAll(tasks).WaitAsync(TimeSpan.FromSeconds(45));
            Check("Exactly one concurrent request gets last slot", winners.Count(x => x) == 1);
            await using (var scope = app.Services.CreateAsyncScope())
                Check("Concurrent result has exactly one active usage", await Db(scope).PromotionUsages.CountAsync(x => x.PromotionId == last.Id && x.ReleasedAt == null) == 1);
            var big = await Create(Body("LARGE") with { DiscountValue = 99.99m });
            var bigResult = await Apply(await NewOrder(customerA, 333333333333333333m, 333333333333333333m), big.Code, customerA);
            Check("Large values allocate exactly without overflow", bigResult.Lines.Sum(x => x.DiscountAmount) == bigResult.DiscountAmount);
            await using (var scope = app.Services.CreateAsyncScope())
            {
                await Db(scope).Promotions.SingleAsync(x => x.PromotionId == voucher.Id);
                await Uow(scope).ExecuteAsync(async () =>
                {
                    var tracked = Db(scope).ChangeTracker.Entries<Promotion>().Single().Entity;
                    tracked.DiscountValue = 50;
                    await Throws<BusinessRuleException>("Unsaved pre-lock edits are rejected",
                        () => scope.ServiceProvider.GetRequiredService<IVoucherRepository>().GetByIdForUpdateAsync(voucher.Id));
                    return true;
                });
            }
            return checks;

            SaveVoucherRequest Body(string code) => new()
            {
                Code = code, Name = "Voucher check", DiscountType = "Percent", DiscountValue = 10,
                MinOrderAmount = 0, MaxDiscountAmount = null, StartDate = DateTimeOffset.UtcNow.AddDays(-1),
                EndDate = DateTimeOffset.UtcNow.AddDays(1), TotalUsageLimit = null, PerCustomerUsageLimit = null, IsActive = true
            };
            async Task<VoucherDto> Create(SaveVoucherRequest body, string? token = null)
            {
                using var response = await Send(HttpMethod.Post, route, token ?? admin, body);
                await Status("Create " + body.Code, HttpStatusCode.Created, response);
                var value = (await response.Content.ReadFromJsonAsync<VoucherDto>())!;
                Check("Created Location targets detail", response.Headers.Location?.OriginalString.EndsWith("/api/admin/vouchers/" + value.Id) == true);
                return value;
            }
            async Task<VoucherPreviewDto> Preview(PreviewVoucherRequest body)
            {
                using var response = await Send(HttpMethod.Post, previewRoute, customerToken, body);
                await Status("Preview " + body.Code, HttpStatusCode.OK, response);
                return (await response.Content.ReadFromJsonAsync<VoucherPreviewDto>())!;
            }
            async Task<HttpResponseMessage> Send(HttpMethod method, string url, string? token, object? body = null)
            {
                using var request = new HttpRequestMessage(method, url);
                if (token is not null) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                if (body is not null) request.Content = JsonContent.Create(body);
                return await client.SendAsync(request);
            }
            async Task ExpectHttp(string name, HttpStatusCode expected, HttpMethod method, string url, string? token, object? body = null)
            {
                using var response = await Send(method, url, token, body);
                await Status(name, expected, response);
            }
            async Task Status(string name, HttpStatusCode expected, HttpResponseMessage response)
            {
                if (response.StatusCode != expected)
                    throw new Exception($"{name}: expected {expected}, got {response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
                Check(name, true);
            }
            async Task<int> NewOrder(int customerId, params decimal[] prices)
            {
                await using var scope = app.Services.CreateAsyncScope();
                var order = new Order { CustomerId = customerId, EmployeeId = employeeId, TotalAmount = prices.Sum() };
                foreach (var price in prices) order.Details.Add(new OrderDetail
                {
                    ProductId = productId, ProductNameSnapshot = "Food snapshot", UnitPrice = price, Quantity = 1
                });
                Db(scope).Orders.Add(order);
                await Db(scope).SaveChangesAsync();
                return order.OrderId;
            }
            async Task<VoucherApplicationResult> Apply(int id, string code, int customerId)
            {
                await using var scope = app.Services.CreateAsyncScope();
                return await Uow(scope).ExecuteAsync(async () =>
                {
                    // Caller lấy khóa khách trước nguồn đơn/voucher theo contract chung.
                    await Db(scope).Customers.FromSqlInterpolated(
                        $"SELECT * FROM [dbo].[Customers] WITH (UPDLOCK, HOLDLOCK) WHERE [CustomerId] = {customerId}")
                        .AsNoTracking().SingleAsync();
                    return await Service(scope).ValidateAndUseAsync(id, code, customerId);
                });
            }
            async Task<bool> Cancel(int id)
            {
                await using var scope = app.Services.CreateAsyncScope();
                return await Uow(scope).ExecuteAsync(async () =>
                {
                    var order = await scope.ServiceProvider.GetRequiredService<IVoucherRepository>().GetOrderForUpdateAsync(id);
                    order!.Status = "Cancelled";
                    await Uow(scope).SaveChangesAsync();
                    return await Service(scope).ReleaseForCancelledOrderAsync(id);
                });
            }
            // Caller mô phỏng xác minh hết hạn/hủy toàn đơn; voucher chỉ hoàn lượt.
            async Task<bool> Expire(int id, bool rollback = false)
            {
                await using var scope = app.Services.CreateAsyncScope();
                return await Uow(scope).ExecuteAsync(async () =>
                {
                    var customerId = await Db(scope).Orders.AsNoTracking().Where(x => x.OrderId == id).Select(x => x.CustomerId).SingleAsync();
                    await Db(scope).Customers.FromSqlInterpolated(
                        $"SELECT * FROM [dbo].[Customers] WITH (UPDLOCK, HOLDLOCK) WHERE [CustomerId] = {customerId}")
                        .AsNoTracking().SingleAsync();
                    var order = await scope.ServiceProvider.GetRequiredService<IVoucherRepository>().GetOrderForUpdateAsync(id);
                    if (order!.Status is not ("AwaitingPayment" or "Expired")) throw new Exception("Invalid expiry fixture state.");
                    order.Status = "Expired";
                    await Uow(scope).SaveChangesAsync();
                    var released = await Service(scope).ReleaseForCancelledOrderAsync(id);
                    if (rollback) throw new CheckRollback();
                    return released;
                });
            }
            static ApplicationDbContext Db(AsyncServiceScope scope) => scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            static IVoucherService Service(AsyncServiceScope scope) => scope.ServiceProvider.GetRequiredService<IVoucherService>();
            static IVoucherUnitOfWork Uow(AsyncServiceScope scope) => scope.ServiceProvider.GetRequiredService<IVoucherUnitOfWork>();
            async Task Throws<T>(string name, Func<Task> action) where T : Exception
            {
                try { await action(); }
                catch (T) { Check(name, true); return; }
                throw new Exception("Expected " + typeof(T).Name + ": " + name);
            }
            void Check(string name, bool passed)
            {
                if (!passed) throw new Exception("Voucher check failed: " + name);
                checks++;
                Console.WriteLine("PASS Voucher: " + name);
            }
        }
        finally
        {
            await app.StopAsync();
            await fixture.CleanupAsync();
        }
    }

    private sealed class CheckRollback : Exception;
}
