// Kiểm tra khách hàng + ví + sổ cái trên SQL Server THẬT (S1-M-1 / SCRUM-5).
// Luôn tạo một database thử tên ngẫu nhiên, áp migrations, chạy toàn bộ kiểm tra qua HTTP (TestServer) rồi dọn.
// Cần SQL Server vì khóa dòng (UPDLOCK) và transaction không có ở InMemory. Không có connection string thì
// chương trình KHÔNG chạy kiểm tra và thoát mã 2 (không tính là đã pass).
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using CyberManagement.API;
using CyberManagement.API.Controller;
using CyberManagement.Application;
using CyberManagement.Application.DTOs.Customers;
using CyberManagement.Application.DTOs.Identity;
using CyberManagement.Application.Exceptions;
using CyberManagement.Application.Interfaces.Repositories;
using CyberManagement.Application.Interfaces.Services;
using CyberManagement.Application.Security;
using CyberManagement.Application.Wallet;
using CyberManagement.Domain.Entities;
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

var sqlConnection = Environment.GetEnvironmentVariable("WALLET_CHECKS_SQL_CONNECTION");
if (string.IsNullOrWhiteSpace(sqlConnection))
{
    Console.Error.WriteLine("KHÔNG CHẠY: đặt WALLET_CHECKS_SQL_CONNECTION (cần quyền tạo/xóa database thử). Chưa có kiểm tra nào được thực hiện.");
    return 2;
}

const string StaffPasswordSuffix = "-check-password-2026!";
const string CustomerPassword = "Customer-check-password-2026!";
var databaseFixture = new AuthCheckDatabase(sqlConnection);
var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    EnvironmentName = "Development",
    ApplicationName = typeof(CyberManagement.API.Program).Assembly.FullName
});
builder.WebHost.UseTestServer();
builder.Logging.ClearProviders();
builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
{
    ["Authentication:Jwt:SigningKey"] = "checks-only-signing-key-that-is-at-least-32-bytes",
    ["SeedAccounts:Admin:Password"] = "Admin" + StaffPasswordSuffix,
    ["SeedAccounts:Manager:Password"] = "Manager" + StaffPasswordSuffix,
    ["SeedAccounts:Cashier:Password"] = "Cashier" + StaffPasswordSuffix,
    ["SeedAccounts:Waiter:Password"] = "Waiter" + StaffPasswordSuffix,
    ["SeedAccounts:Technician:Password"] = "Technician" + StaffPasswordSuffix
});
builder.Services.AddDbContext<ApplicationDbContext>(databaseFixture.Configure);
builder.Services.AddApplicationServices();
builder.Services.AddApiServices(builder.Configuration);
builder.Services.AddScoped<IIdentityRepository, IdentityRepository>();
builder.Services.AddScoped<ICustomerIdentityRepository, CustomerIdentityRepository>();
builder.Services.AddScoped<ICustomerRepository, CustomerRepository>();
builder.Services.AddScoped<IEmployeePasswordHasher, EmployeePasswordHasher>();
builder.Services.AddScoped<ICustomerPasswordHasher, CustomerPasswordHasher>();
builder.Services.AddControllers().AddApplicationPart(typeof(AuthController).Assembly);

await using var app = builder.Build();
HttpClient client = null!;
var checks = 0;
var started = false;
try
{
    await using (var scope = app.Services.CreateAsyncScope())
    {
        await databaseFixture.InitializeAsync(scope.ServiceProvider.GetRequiredService<ApplicationDbContext>());
        var passwords = RolePermissionCatalog.Roles.ToDictionary(
            role => role.Name, role => app.Configuration[$"SeedAccounts:{role.Name}:Password"], StringComparer.Ordinal);
        await scope.ServiceProvider.GetRequiredService<IIdentitySeeder>()
            .SeedAsync(new IdentitySeedOptions(true, passwords), CancellationToken.None);
    }

    app.UseExceptionHandler();
    app.UseAuthentication();
    app.UseAuthorization();
    app.MapControllers();
    await app.StartAsync();
    started = true;
    client = app.GetTestClient();

    var admin = await StaffToken("admin", "Admin");
    var manager = await StaffToken("manager01", "Manager");
    var cashier = await StaffToken("cashier01", "Cashier");
    var waiter = await StaffToken("waiter01", "Waiter");
    var technician = await StaffToken("tech01", "Technician");
    var adminId = await Query(db => db.Employees.Where(e => e.Username == "admin").Select(e => e.EmployeeId).SingleAsync());

    // ---------- Phân quyền nhân viên ----------
    await Expect("Anonymous cannot list customers", HttpStatusCode.Unauthorized, await Send(HttpMethod.Get, "/api/admin/customers"));
    await Expect("Technician (no customer.view) is rejected", HttpStatusCode.Forbidden, await Send(HttpMethod.Get, "/api/admin/customers", technician));
    await Expect("Waiter (customer.view) can list", HttpStatusCode.OK, await Send(HttpMethod.Get, "/api/admin/customers", waiter));
    await Expect("Waiter cannot create customers", HttpStatusCode.Forbidden,
        await Send(HttpMethod.Post, "/api/admin/customers", waiter, NewCustomer("Waiter Try", "0910000001")));

    // ---------- Tạo khách, đọc lại, đăng nhập bằng auth chung ----------
    using var createdResponse = await Send(HttpMethod.Post, "/api/admin/customers", admin, NewCustomer("Khách A", "0910000002", "a@example.test"));
    Check("Admin creates customer: 201 + Location", createdResponse.StatusCode == HttpStatusCode.Created && createdResponse.Headers.Location is not null);
    var customerA = (await createdResponse.Content.ReadFromJsonAsync<CustomerDto>())!;
    Check("Create response matches request and starts at zero wallet", customerA is { FullName: "Khách A", Phone: "0910000002", Email: "a@example.test", WalletBalance: 0, Status: "Active", HasActiveSession: false });
    var persistedA = await Query(db => db.Customers.AsNoTracking().SingleAsync(c => c.CustomerId == customerA.CustomerId));
    Check("Create response equals persisted data", persistedA.FullName == customerA.FullName && persistedA.Phone == customerA.Phone && persistedA.WalletBalance == 0);
    Check("Password is stored hashed, not plain", !string.IsNullOrEmpty(persistedA.PasswordHash) && persistedA.PasswordHash != CustomerPassword);
    var historyA = await Query(db => db.CustomerStatusHistories.AsNoTracking().Where(h => h.CustomerId == customerA.CustomerId).ToListAsync());
    Check("Creation is audited with the acting employee", historyA is [{ OldStatus: null, NewStatus: "Active" } h0] && h0.ChangedBy == adminId);

    await Expect("New customer logs in with shared customer auth", HttpStatusCode.OK, await CustomerLogin("0910000002", CustomerPassword));
    await Expect("New customer with wrong password is rejected", HttpStatusCode.Unauthorized, await CustomerLogin("0910000002", "wrong-password"));

    var customerB = await CreateCustomer(admin, "Khách B", "0910000003");
    var list = (await (await Send(HttpMethod.Get, "/api/admin/customers", admin)).Content.ReadFromJsonAsync<List<CustomerDto>>())!;
    Check("List works and returns newest first", list.Count >= 2 && list[0].CustomerId == customerB.CustomerId && list.Any(c => c.CustomerId == customerA.CustomerId));
    using (var detail = await Send(HttpMethod.Get, $"/api/admin/customers/{customerA.CustomerId}", admin))
        Check("Detail works and matches", detail.StatusCode == HttpStatusCode.OK && (await detail.Content.ReadFromJsonAsync<CustomerDto>())!.Phone == "0910000002");
    await Expect("Unknown customer detail is 404", HttpStatusCode.NotFound, await Send(HttpMethod.Get, "/api/admin/customers/99999999", admin));

    // ---------- Kiểm tra dữ liệu vào ----------
    await Expect("Duplicate phone is 409", HttpStatusCode.Conflict, await Send(HttpMethod.Post, "/api/admin/customers", admin, NewCustomer("Trùng", "0910000002")));
    await Expect("Invalid phone is 400", HttpStatusCode.BadRequest, await Send(HttpMethod.Post, "/api/admin/customers", admin, NewCustomer("Sai", "12345")));
    await Expect("Empty name is 400", HttpStatusCode.BadRequest, await Send(HttpMethod.Post, "/api/admin/customers", admin, NewCustomer("  ", "0910000004")));
    await Expect("Short password is 400", HttpStatusCode.BadRequest, await Send(HttpMethod.Post, "/api/admin/customers", admin,
        new { fullName = "Ngắn", phone = "0910000005", email = (string?)null, initialPassword = "short" }));
    await Expect("Invalid email is 400", HttpStatusCode.BadRequest, await Send(HttpMethod.Post, "/api/admin/customers", admin, NewCustomer("Mail", "0910000006", "not-an-email")));
    Check("Rejected creations leave no rows", await Query(db => db.Customers.CountAsync(c => c.Phone == "0910000004" || c.Phone == "0910000005" || c.Phone == "0910000006" || c.Phone == "12345")) == 0);
    var cashierCreated = await Send(HttpMethod.Post, "/api/admin/customers", cashier, NewCustomer("Do thu ngân", "0910000007"));
    await Expect("Cashier (customer.create) can create", HttpStatusCode.Created, cashierCreated);

    // ---------- Sửa hồ sơ: không đổi được ví qua request ----------
    using (var update = await Send(HttpMethod.Put, $"/api/admin/customers/{customerA.CustomerId}", admin,
        new { fullName = "Khách A (đã sửa)", phone = "0910000002", email = (string?)null, walletBalance = 999999 }))
        Check("Update works", update.StatusCode == HttpStatusCode.OK && (await update.Content.ReadFromJsonAsync<CustomerDto>())!.FullName == "Khách A (đã sửa)");
    Check("Update ignores walletBalance in the request", await Query(db => db.Customers.Where(c => c.CustomerId == customerA.CustomerId).Select(c => c.WalletBalance).SingleAsync()) == 0);
    await Expect("Update to another customer's phone is 409", HttpStatusCode.Conflict,
        await Send(HttpMethod.Put, $"/api/admin/customers/{customerA.CustomerId}", admin, new { fullName = "X", phone = "0910000003", email = (string?)null }));
    await Expect("Update unknown customer is 404", HttpStatusCode.NotFound,
        await Send(HttpMethod.Put, "/api/admin/customers/99999999", admin, new { fullName = "X", phone = "0910000099", email = (string?)null }));

    // ---------- Khách chỉ đọc dữ liệu của chính mình ----------
    var tokenA = await CustomerToken("0910000002");
    var tokenB = await CustomerToken("0910000003");
    var profileA = await ReadProfile(tokenA);
    var profileB = await ReadProfile(tokenB);
    Check("Customer A reads own profile", profileA.CustomerId == customerA.CustomerId);
    Check("Customer B reads own profile", profileB.CustomerId == customerB.CustomerId);
    var spoofed = (await (await Send(HttpMethod.Get, $"/api/client/profile?customerId={customerB.CustomerId}", tokenA)).Content.ReadFromJsonAsync<CustomerDto>())!;
    Check("Changing the id in the request does not change whose data is returned", spoofed.CustomerId == customerA.CustomerId);
    await Expect("Staff token cannot use client profile", HttpStatusCode.Forbidden, await Send(HttpMethod.Get, "/api/client/profile", admin));
    await Expect("Customer token cannot use admin customer list", HttpStatusCode.Forbidden, await Send(HttpMethod.Get, "/api/admin/customers", tokenA));
    await Expect("Customer token cannot top up wallets", HttpStatusCode.Forbidden,
        await Send(HttpMethod.Post, $"/api/admin/customers/{customerA.CustomerId}/wallet/top-ups", tokenA, TopUp(50_000)));
    await Expect("Anonymous cannot read client wallet", HttpStatusCode.Unauthorized, await Send(HttpMethod.Get, "/api/client/wallet"));

    // ---------- Nạp tại quầy ----------
    await Expect("Waiter (no wallet.topup) cannot top up", HttpStatusCode.Forbidden,
        await Send(HttpMethod.Post, $"/api/admin/customers/{customerA.CustomerId}/wallet/top-ups", waiter, TopUp(50_000)));
    using (var topUp = await Send(HttpMethod.Post, $"/api/admin/customers/{customerA.CustomerId}/wallet/top-ups", cashier, TopUp(50_000)))
    {
        var tx = (await topUp.Content.ReadFromJsonAsync<WalletTransactionDto>())!;
        Check("Cashier top-up: 201, +50.000, TopUp, balance after 50.000", topUp.StatusCode == HttpStatusCode.Created
            && tx is { Amount: 50_000, BalanceAfter: 50_000, Type: "TopUp" } && tx.CustomerId == customerA.CustomerId);
    }
    Check("Top-up balance and ledger agree in SQL",
        await Balance(customerA.CustomerId) == 50_000 && await LedgerCount(customerA.CustomerId) == 1);
    var walletA = (await (await Send(HttpMethod.Get, "/api/client/wallet", tokenA)).Content.ReadFromJsonAsync<ClientWalletDto>())!;
    Check("Client wallet shows SQL balance and ledger", walletA.WalletBalance == 50_000 && walletA.Transactions.Count == 1 && walletA.Transactions[0].BalanceAfter == 50_000);
    var walletB = (await (await Send(HttpMethod.Get, "/api/client/wallet", tokenB)).Content.ReadFromJsonAsync<ClientWalletDto>())!;
    Check("Client B does not see A's money or transactions", walletB.WalletBalance == 0 && walletB.Transactions.Count == 0);
    using (var history = await Send(HttpMethod.Get, $"/api/admin/customers/{customerA.CustomerId}/wallet-transactions", cashier))
        Check("Staff reads a customer's ledger", history.StatusCode == HttpStatusCode.OK && (await history.Content.ReadFromJsonAsync<List<WalletTransactionDto>>())!.Count == 1);
    foreach (var (name, body) in new (string, object)[]
    {
        ("amount below minimum", TopUp(5_000)),
        ("amount not a multiple of 1.000", TopUp(15_500)),
        ("amount above maximum", TopUp(3_000_000)),
        ("unknown collection method", new { amount = 20_000, collectionMethod = "Card" }),
    })
    {
        await Expect($"Top-up rejected: {name}", HttpStatusCode.BadRequest,
            await Send(HttpMethod.Post, $"/api/admin/customers/{customerA.CustomerId}/wallet/top-ups", cashier, body));
    }
    Check("Rejected top-ups change nothing", await Balance(customerA.CustomerId) == 50_000 && await LedgerCount(customerA.CustomerId) == 1);
    await Expect("Top-up for unknown customer is 404", HttpStatusCode.NotFound,
        await Send(HttpMethod.Post, "/api/admin/customers/99999999/wallet/top-ups", cashier, TopUp(20_000)));

    // ---------- Khóa/mở khóa ----------
    var customerC = await CreateCustomer(admin, "Khách C", "0910000008");
    await Expect("Cashier cannot lock (no customer.status.change)", HttpStatusCode.Forbidden,
        await Send(HttpMethod.Put, $"/api/admin/customers/{customerC.CustomerId}/status", cashier, new { status = "Locked", reason = "x" }));
    await Expect("Lock without reason is 400", HttpStatusCode.BadRequest,
        await Send(HttpMethod.Put, $"/api/admin/customers/{customerC.CustomerId}/status", manager, new { status = "Locked", reason = "" }));
    await Expect("Invalid status is 400", HttpStatusCode.BadRequest,
        await Send(HttpMethod.Put, $"/api/admin/customers/{customerC.CustomerId}/status", manager, new { status = "Banned", reason = "x" }));
    await Expect("Manager locks customer", HttpStatusCode.OK,
        await Send(HttpMethod.Put, $"/api/admin/customers/{customerC.CustomerId}/status", manager, new { status = "Locked", reason = "Vi phạm nội quy" }));
    var historyC = await Query(db => db.CustomerStatusHistories.AsNoTracking().Where(h => h.CustomerId == customerC.CustomerId).OrderBy(h => h.HistoryId).ToListAsync());
    Check("Lock is audited with reason and old/new status", historyC.Count == 2 && historyC[1] is { OldStatus: "Active", NewStatus: "Locked", Reason: "Vi phạm nội quy" });
    await Expect("Locking again is 409", HttpStatusCode.Conflict,
        await Send(HttpMethod.Put, $"/api/admin/customers/{customerC.CustomerId}/status", manager, new { status = "Locked", reason = "lại" }));
    await Expect("Locked customer cannot log in", HttpStatusCode.Forbidden, await CustomerLogin("0910000008", CustomerPassword));
    await Expect("Locked customer cannot be topped up", HttpStatusCode.Conflict,
        await Send(HttpMethod.Post, $"/api/admin/customers/{customerC.CustomerId}/wallet/top-ups", cashier, TopUp(20_000)));
    Check("Rejected top-up of a locked customer leaves no ledger row", await LedgerCount(customerC.CustomerId) == 0 && await Balance(customerC.CustomerId) == 0);
    await Expect("Manager unlocks customer", HttpStatusCode.OK,
        await Send(HttpMethod.Put, $"/api/admin/customers/{customerC.CustomerId}/status", manager, new { status = "Active", reason = "Đã làm việc" }));
    await Expect("Unlocked customer can be topped up", HttpStatusCode.Created,
        await Send(HttpMethod.Post, $"/api/admin/customers/{customerC.CustomerId}/wallet/top-ups", cashier, TopUp(20_000)));

    // ---------- Đồng thời: nạp song song cùng một ví ----------
    var customerD = await CreateCustomer(admin, "Khách D", "0910000009");
    var parallelTopUps = await Task.WhenAll(Enumerable.Range(0, 10).Select(_ =>
        Send(HttpMethod.Post, $"/api/admin/customers/{customerD.CustomerId}/wallet/top-ups", cashier, TopUp(10_000))));
    Check("10 parallel top-ups all succeed", parallelTopUps.All(r => r.StatusCode == HttpStatusCode.Created));
    var dAfter = await Query(db => db.WalletTransactions.AsNoTracking().Where(t => t.CustomerId == customerD.CustomerId).OrderBy(t => t.TransactionId).ToListAsync());
    Check("Parallel top-ups: balance is exactly the sum (no lost update)", await Balance(customerD.CustomerId) == 100_000 && dAfter.Count == 10);
    Check("Parallel top-ups: ledger BalanceAfter is a clean 10k..100k sequence",
        dAfter.Select(t => t.BalanceAfter).SequenceEqual(Enumerable.Range(1, 10).Select(i => i * 10_000m)));

    // ---------- Sổ cái: trừ vượt số dư, rollback, trừ song song ----------
    var customerE = await CreateCustomer(admin, "Khách E", "0910000010");
    await ExpectThrows<InsufficientWalletBalanceException>("Debit over balance is rejected", () => InTransaction(customerE.CustomerId, customer =>
        WalletLedger.Record(customer, WalletTransactionTypes.GamingCharge, -10_000, DateTime.UtcNow, "Check")));
    Check("Rejected debit leaves balance and ledger unchanged", await Balance(customerE.CustomerId) == 0 && await LedgerCount(customerE.CustomerId) == 0);

    await ExpectThrows<InvalidOperationException>("Failure after ledger write rolls everything back", async () =>
    {
        await using var scope = app.Services.CreateAsyncScope();
        var repository = scope.ServiceProvider.GetRequiredService<ICustomerRepository>();
        await repository.ExecuteInTransactionAsync<int>(async () =>
        {
            var customer = (await repository.FindTrackedForWalletUpdateAsync(customerE.CustomerId, CancellationToken.None))!;
            WalletLedger.Record(customer, WalletTransactionTypes.TopUp, 20_000, DateTime.UtcNow, "Check");
            await repository.SaveChangesAsync(CancellationToken.None);
            throw new InvalidOperationException("simulated failure after SaveChanges");
        }, CancellationToken.None);
    });
    Check("Rollback restores balance and ledger together", await Balance(customerE.CustomerId) == 0 && await LedgerCount(customerE.CustomerId) == 0);

    var customerF = await CreateCustomer(admin, "Khách F", "0910000011");
    await Expect("Fund customer F", HttpStatusCode.Created,
        await Send(HttpMethod.Post, $"/api/admin/customers/{customerF.CustomerId}/wallet/top-ups", cashier, TopUp(30_000)));
    var debitResults = await Task.WhenAll(Enumerable.Range(0, 5).Select(async _ =>
    {
        try
        {
            await InTransaction(customerF.CustomerId, customer =>
                WalletLedger.Record(customer, WalletTransactionTypes.GamingCharge, -10_000, DateTime.UtcNow, "Check parallel"));
            return true;
        }
        catch (InsufficientWalletBalanceException)
        {
            return false;
        }
    }));
    var fLedger = await Query(db => db.WalletTransactions.AsNoTracking().Where(t => t.CustomerId == customerF.CustomerId).ToListAsync());
    Check("5 parallel 10k debits on a 30k wallet: exactly 3 succeed", debitResults.Count(ok => ok) == 3);
    Check("Parallel debits never overspend: balance 0, 1 top-up + 3 charges, no negative BalanceAfter",
        await Balance(customerF.CustomerId) == 0 && fLedger.Count == 4 && fLedger.All(t => t.BalanceAfter >= 0));

    // ---------- Quy ước ledger (không cần DB) ----------
    var probe = new Customer { CustomerId = 1, FullName = "Probe", Phone = "0", Status = "Active", WalletBalance = 10_000 };
    ExpectThrowsSync<ArgumentException>("Credit type with negative amount is rejected", () => WalletLedger.Record(probe, WalletTransactionTypes.TopUp, -1_000, DateTime.UtcNow));
    ExpectThrowsSync<ArgumentException>("Debit type with positive amount is rejected", () => WalletLedger.Record(probe, WalletTransactionTypes.GamingCharge, 1_000, DateTime.UtcNow));
    ExpectThrowsSync<ArgumentException>("Zero amount is rejected", () => WalletLedger.Record(probe, WalletTransactionTypes.TopUp, 0, DateTime.UtcNow));
    ExpectThrowsSync<ArgumentException>("Unknown type is rejected", () => WalletLedger.Record(probe, "Bogus", 1_000, DateTime.UtcNow));
    probe.Status = "Locked";
    ExpectThrowsSync<BusinessRuleException>("Locked customer cannot change wallet", () => WalletLedger.Record(probe, WalletTransactionTypes.TopUp, 1_000, DateTime.UtcNow));
    ExpectThrowsSync<BusinessRuleException>("Locked customer cannot be charged", () => WalletLedger.Record(probe, WalletTransactionTypes.GamingCharge, -1_000, DateTime.UtcNow));
    Check("Rejected ledger calls never touched the probe balance", probe.WalletBalance == 10_000 && probe.WalletTransactions.Count == 0);
    WalletLedger.Record(probe, WalletTransactionTypes.OrderRefund, 2_000, DateTime.UtcNow, "Check refund");
    Check("Locked customer still receives a refund", probe.WalletBalance == 12_000 && probe.WalletTransactions.Count == 1);

    // ---------- Giới hạn đã biết (KHÔNG tính là pass) ----------
    var customerG = await CreateCustomer(admin, "Khách G", "0910000012");
    for (var i = 0; i < 2; i++)
        await Send(HttpMethod.Post, $"/api/admin/customers/{customerG.CustomerId}/wallet/top-ups", cashier, TopUp(10_000));
    Console.WriteLine(await Balance(customerG.CustomerId) == 20_000
        ? "KNOWN GAP (not a pass): two identical sequential top-up requests are both applied; no idempotency key in schema yet (Backend/docs/002-wallet-ledger.md section 4)."
        : "NOTE: duplicate top-up behaviour changed; update docs/002-wallet-ledger.md section 4.");

    Console.WriteLine($"{checks} customer/wallet checks passed on SQL Server ({databaseFixture.Provider}).");
}
finally
{
    client?.Dispose();
    try
    {
        if (started)
            await app.StopAsync();
    }
    finally
    {
        await databaseFixture.CleanupAsync();
    }
}
return 0;

// ===== Hàm phụ =====
static object NewCustomer(string name, string phone, string? email = null) =>
    new { fullName = name, phone, email, initialPassword = CustomerPassword };

static object TopUp(decimal amount) => new { amount, collectionMethod = "Cash" };

async Task<HttpResponseMessage> Send(HttpMethod method, string path, string? token = null, object? body = null)
{
    using var request = new HttpRequestMessage(method, path);
    if (token is not null)
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
    if (body is not null)
        request.Content = JsonContent.Create(body);
    return await client.SendAsync(request);
}

async Task<string> ReadToken(HttpResponseMessage response, string name)
{
    using (response)
    {
        if (response.StatusCode != HttpStatusCode.OK)
            throw new InvalidOperationException($"{name}: expected 200, received {(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        return json.GetProperty("accessToken").GetString()!;
    }
}

async Task<string> StaffToken(string username, string role) =>
    await ReadToken(await client.PostAsJsonAsync("/api/auth/login", new { username, password = role + StaffPasswordSuffix }), $"{role} login");

Task<HttpResponseMessage> CustomerLogin(string phone, string password) =>
    client.PostAsJsonAsync("/api/auth/customer/login", new { phone, password });

async Task<string> CustomerToken(string phone) => await ReadToken(await CustomerLogin(phone, CustomerPassword), $"Customer {phone} login");

async Task<CustomerDto> CreateCustomer(string token, string name, string phone)
{
    using var response = await Send(HttpMethod.Post, "/api/admin/customers", token, NewCustomer(name, phone));
    if (response.StatusCode != HttpStatusCode.Created)
        throw new InvalidOperationException($"Create {phone}: expected 201, received {(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
    return (await response.Content.ReadFromJsonAsync<CustomerDto>())!;
}

async Task<CustomerDto> ReadProfile(string token)
{
    using var response = await Send(HttpMethod.Get, "/api/client/profile", token);
    return (await response.Content.ReadFromJsonAsync<CustomerDto>())!;
}

async Task<T> Query<T>(Func<ApplicationDbContext, Task<T>> action)
{
    await using var scope = app.Services.CreateAsyncScope();
    return await action(scope.ServiceProvider.GetRequiredService<ApplicationDbContext>());
}

Task<decimal> Balance(int customerId) =>
    Query(db => db.Customers.AsNoTracking().Where(c => c.CustomerId == customerId).Select(c => c.WalletBalance).SingleAsync());

Task<int> LedgerCount(int customerId) =>
    Query(db => db.WalletTransactions.CountAsync(t => t.CustomerId == customerId));

// Mô phỏng một nghiệp vụ khác (phiên chơi/đơn): mở transaction, khóa ví, ghi sổ cái, lưu, commit.
async Task InTransaction(int customerId, Action<Customer> work)
{
    await using var scope = app.Services.CreateAsyncScope();
    var repository = scope.ServiceProvider.GetRequiredService<ICustomerRepository>();
    await repository.ExecuteInTransactionAsync<int>(async () =>
    {
        var customer = await repository.FindTrackedForWalletUpdateAsync(customerId, CancellationToken.None)
            ?? throw new EntityNotFoundException("Customer not found.");
        work(customer);
        await repository.SaveChangesAsync(CancellationToken.None);
        return 0;
    }, CancellationToken.None);
}

async Task Expect(string name, HttpStatusCode expected, HttpResponseMessage response)
{
    using (response)
    {
        if (response.StatusCode != expected)
            throw new InvalidOperationException($"{name}: expected {(int)expected}, received {(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        checks++;
        Console.WriteLine($"PASS {name}");
    }
}

void Check(string name, bool passed)
{
    if (!passed)
        throw new InvalidOperationException($"FAIL {name}");
    checks++;
    Console.WriteLine($"PASS {name}");
}

async Task ExpectThrows<TException>(string name, Func<Task> action) where TException : Exception
{
    try
    {
        await action();
    }
    catch (TException)
    {
        checks++;
        Console.WriteLine($"PASS {name}");
        return;
    }
    throw new InvalidOperationException($"FAIL {name}: no {typeof(TException).Name} thrown");
}

void ExpectThrowsSync<TException>(string name, Action action) where TException : Exception
{
    try
    {
        action();
    }
    catch (TException)
    {
        checks++;
        Console.WriteLine($"PASS {name}");
        return;
    }
    throw new InvalidOperationException($"FAIL {name}: no {typeof(TException).Name} thrown");
}
