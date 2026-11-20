using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using CyberManagement.API;
using CyberManagement.API.Controller;
using CyberManagement.API.Realtime;
using CyberManagement.Application;
using CyberManagement.Application.DTOs.Computers;
using CyberManagement.Application.DTOs.Identity;
using CyberManagement.Application.DTOs.Realtime;
using CyberManagement.Application.Exceptions;
using CyberManagement.Application.Interfaces.Repositories;
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
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

internal static class SessionBillingChecks
{
    public static async Task<int> RunAsync(string? connection)
    {
        if (string.IsNullOrWhiteSpace(connection)) throw new ArgumentException("AUTH_CHECKS_SQL_CONNECTION is required.");
        var fixture = new AuthCheckDatabase(connection);
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        { EnvironmentName = "Development", ApplicationName = typeof(CyberManagement.API.Program).Assembly.FullName });
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();
        builder.Logging.AddConsole().SetMinimumLevel(LogLevel.Error);
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = connection,
            ["Authentication:Jwt:SigningKey"] = "session-checks-signing-key-at-least-32-bytes"
        });
        builder.Services.AddApplicationServices();
        builder.Services.AddApiServices(builder.Configuration);
        builder.Services.AddInfrastructureServices(builder.Configuration);
        var fault = new InvoiceFailure();
        builder.Services.AddDbContext<ApplicationDbContext>(options => { fixture.Configure(options); options.AddInterceptors(fault); });
        builder.Services.AddControllers().AddApplicationPart(typeof(AuthController).Assembly);
        await using var app = builder.Build();
        var checks = 0;
        const string password = "Session-check-only-2026!";
        var roomIds = new List<int>();
        var typeIds = new List<int>();
        var adminId = 0;
        var started = false;
        try
        {
            await using (var scope = app.Services.CreateAsyncScope())
            {
                var db = Db(scope);
                if (db.Database.HasPendingModelChanges())
                {
                    var snapshot = db.GetService<IMigrationsAssembly>().ModelSnapshot!.Model;
                    var initialized = db.GetService<IModelRuntimeInitializer>().Initialize(snapshot, designTime: true);
                    var differences = db.GetService<IMigrationsModelDiffer>().GetDifferences(
                        initialized.GetRelationalModel(), db.GetService<IDesignTimeModel>().Model.GetRelationalModel());
                    foreach (var difference in differences)
                        Console.WriteLine("MODEL DIFF " + difference.GetType().Name + " " + JsonSerializer.Serialize(
                            difference.GetType().GetProperties().Where(x => new[] { "Name", "Table", "Columns", "Filter", "IsUnique" }.Contains(x.Name))
                                .ToDictionary(x => x.Name, x => x.GetValue(difference))));
                    throw new InvalidOperationException("EF snapshot differs from current model.");
                }
                await fixture.InitializeAsync(db);
                Check("EF snapshot matches current model", !db.Database.HasPendingModelChanges());
                await scope.ServiceProvider.GetRequiredService<IIdentitySeeder>().SeedAsync(new IdentitySeedOptions(true,
                    RolePermissionCatalog.Roles.ToDictionary(x => x.Name, _ => (string?)password)), default);
                adminId = await db.Employees.Where(x => x.Username == "admin").Select(x => x.EmployeeId).SingleAsync();
                for (var i = 0; i < 8; i++)
                {
                    var room = new ComputerRoom { RoomName = $"Session room {i}", Floor = i };
                    var type = new ComputerType { TypeName = $"Session type {i}", PricePerHour = 10000 };
                    db.AddRange(room, type);
                    await db.SaveChangesAsync();
                    roomIds.Add(room.RoomId); typeIds.Add(type.ComputerTypeId);
                }
            }
            app.UseExceptionHandler(); app.UseAuthentication(); app.UseAuthorization();
            app.MapControllers(); app.MapHub<RealtimeHub>(RealtimeHub.Endpoint);
            await app.StartAsync(); started = true;
            using var client = app.GetTestClient();
            var admin = await Login("admin");
            var waiter = await Login("waiter01");
            var cashier = await Login("cashier01");
            var technician = await Login("tech01");

            // Reproduce the review's 8 parallel creates in different rooms, four rounds.
            for (var round = 0; round < 4; round++)
            {
                var number = round;
                var created = await Task.WhenAll(Enumerable.Range(0, 8).Select(i => CreateComputer($"Parallel-{number}-{i}", i)));
                Check($"SQL concurrent catalog round {round + 1}: eight distinct committed machines", created.Select(x => x.ComputerId).Distinct().Count() == 8);
            }
            var duplicateRequests = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ =>
                Send(HttpMethod.Post, "/api/admin/computers", admin,
                    new { computerName = "Same name", roomId = roomIds[0], computerTypeId = typeIds[0] })));
            Check("Concurrent duplicate machine name has exactly one winner and seven conflicts",
                duplicateRequests.Count(x => x.StatusCode is HttpStatusCode.OK or HttpStatusCode.Created) == 1
                && duplicateRequests.Count(x => x.StatusCode == HttpStatusCode.Conflict) == 7);
            foreach (var response in duplicateRequests) response.Dispose();
            await Status("Waiter cannot create a computer", HttpStatusCode.Forbidden,
                await Send(HttpMethod.Post, "/api/admin/computers", waiter, new { computerName = "Forbidden", roomId = roomIds[0], computerTypeId = typeIds[0] }));
            await Status("Fractional hourly price rejected", HttpStatusCode.BadRequest,
                await Send(HttpMethod.Post, "/api/admin/computer-types", admin,
                    new { typeName = "Fractional", pricePerHour = 1.5m }));
            var unusedType = await Read<ComputerTypeDto>(await Send(HttpMethod.Post, "/api/admin/computer-types", admin,
                new { typeName = "Temporary type", pricePerHour = 12000 }));
            await Status("Update computer type", HttpStatusCode.OK, await Send(HttpMethod.Put, $"/api/admin/computer-types/{unusedType.ComputerTypeId}", admin,
                new { typeName = "Updated type", pricePerHour = 15000 }));
            await Status("Delete unused computer type", HttpStatusCode.NoContent,
                await Send(HttpMethod.Delete, $"/api/admin/computer-types/{unusedType.ComputerTypeId}", admin));
            var maintenancePc = await CreateComputer("Maintenance");
            await Status("Update computer", HttpStatusCode.OK, await Send(HttpMethod.Put, $"/api/admin/computers/{maintenancePc.ComputerId}", admin,
                new { computerName = "Maintenance updated", roomId = roomIds[1], computerTypeId = typeIds[1] }));
            await Status("Technician starts maintenance", HttpStatusCode.Created,
                await Send(HttpMethod.Post, $"/api/admin/computers/{maintenancePc.ComputerId}/maintenances", technician, new { description = "Replace cooling" }));
            var customer = await NewCustomer(100000);
            var other = await NewCustomer(100000);
            var customerToken = await CustomerLogin(customer);
            var otherToken = await CustomerLogin(other);
            await Status("Open during maintenance rejected", HttpStatusCode.Conflict,
                await OpenResponse(maintenancePc.ComputerId, customer, "maintenance"));
            await Status("Duplicate maintenance rejected", HttpStatusCode.Conflict,
                await Send(HttpMethod.Post, $"/api/admin/computers/{maintenancePc.ComputerId}/maintenances", technician, new { description = "Duplicate" }));
            await Status("Maintenance record blocks generic Available status", HttpStatusCode.Conflict,
                await Send(HttpMethod.Patch, $"/api/admin/computers/{maintenancePc.ComputerId}/status", technician,
                    new { status = "Available", expectedStatus = "Maintenance" }));
            await Status("Complete maintenance", HttpStatusCode.OK,
                await Send(HttpMethod.Post, $"/api/admin/computers/{maintenancePc.ComputerId}/maintenances/1/complete", technician));
            await Status("Keep machine maintenance history", HttpStatusCode.Conflict,
                await Send(HttpMethod.Delete, $"/api/admin/computers/{maintenancePc.ComputerId}", admin));
            var unused = await CreateComputer("Delete me");
            await Status("Delete unused computer", HttpStatusCode.NoContent, await Send(HttpMethod.Delete, $"/api/admin/computers/{unused.ComputerId}", admin));

            var pc = await CreateComputer("Billing");
            foreach (var key in new[] { "SessionAutoClose:1", "sessionautoinvoice:1" })
                await Status("Staff cannot reserve a System key", HttpStatusCode.BadRequest, await OpenResponse(pc.ComputerId, customer, key));
            await Status("Browser cannot inject System actor", HttpStatusCode.BadRequest,
                await Send(HttpMethod.Post, "/api/admin/sessions", admin,
                    new { computerId = pc.ComputerId, customerId = customer, actorType = "System" }, "actor"));
            await Status("Waiter cannot open", HttpStatusCode.Forbidden,
                await Send(HttpMethod.Post, "/api/admin/sessions", waiter, new { computerId = pc.ComputerId, customerId = customer }, "waiter"));
            await using var peerA = await RealtimeTestPeer.ConnectAsync(app.GetTestServer(), admin);
            await using var peerB = await RealtimeTestPeer.ConnectAsync(app.GetTestServer(), cashier);
            await using var customerPeer = await RealtimeTestPeer.ConnectAsync(app.GetTestServer(), customerToken);
            var opened = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => Open(pc.ComputerId, customer, "same-open")));
            var session = opened[0];
            Check("Eight open retries return one stored session", opened.All(x => x.GamingSessionId == session.GamingSessionId));
            Check("Two staff SignalR clients receive committed session", await NextEvent(peerA, "SessionChanged") == session.GamingSessionId
                && await NextEvent(peerB, "SessionChanged") == session.GamingSessionId);
            Check("Owning customer receives committed session", await NextEvent(customerPeer, "SessionChanged") == session.GamingSessionId);
            var home = await Read<SessionHomeDto>(await Send(HttpMethod.Get, "/api/client/home", customerToken));
            Check("Home reads own persisted session", home.Session?.GamingSessionId == session.GamingSessionId);
            var otherHome = await Read<SessionHomeDto>(await Send(HttpMethod.Get, "/api/client/home", otherToken));
            Check("Home does not expose another customer session", otherHome.Session is null);
            await Status("Duplicate customer session rejected", HttpStatusCode.Conflict, await OpenResponse(maintenancePc.ComputerId, customer, "duplicate-customer"));
            await Status("Duplicate machine session rejected", HttpStatusCode.Conflict, await OpenResponse(pc.ComputerId, other, "duplicate-computer"));
            await Status("Open key payload mismatch rejected", HttpStatusCode.Conflict, await OpenResponse(pc.ComputerId, other, "same-open"));

            var start = DateTime.UtcNow.AddHours(-2);
            await Edit(async db => { var row = await db.GamingSessions.FindAsync(session.GamingSessionId); row!.StartTime = start; });
            var cumulative = new[] { (0d, 0m), (0.9d, 0m), (1d, 3m), (60d, 167m), (120d, 334m), (180d, 500m), (3600d, 10000m) };
            foreach (var (seconds, expected) in cumulative)
            {
                await Settle(customer, start.AddSeconds(seconds));
                await Settle(customer, start.AddSeconds(seconds)); // Same checkpoint must not charge twice.
                await using var scope = app.Services.CreateAsyncScope();
                var db = Db(scope);
                var row = await db.GamingSessions.AsNoTracking().SingleAsync(x => x.GamingSessionId == session.GamingSessionId);
                var charges = await db.SessionCharges.Where(x => x.GamingSessionId == session.GamingSessionId).SumAsync(x => x.Amount);
                Check($"Cumulative billing/retry at {seconds}s = {expected}", row.TotalCost == expected && charges == expected);
            }
            await Edit(async db => { var type = await db.ComputerTypes.FindAsync(typeIds[0]); type!.PricePerHour = 20000; });
            await using (var scope = app.Services.CreateAsyncScope())
                Check("Price snapshot survives tariff changes", (await Db(scope).GamingSessions.FindAsync(session.GamingSessionId))!.PricePerHourSnapshot == 10000);

            // Failure after debit/payment, while creating invoice, must rollback the entire close.
            var before = await Snapshot(session.GamingSessionId);
            fault.FailNextInvoice = 1;
            await Status("Injected invoice failure returns an error", HttpStatusCode.InternalServerError,
                await Send(HttpMethod.Post, $"/api/admin/sessions/{session.GamingSessionId}/close", admin, new { }, "close-main"));
            Check("Invoice failure rolls back wallet, charge, ledger, Payment, end and computer", before == await Snapshot(session.GamingSessionId));
            var closed = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => Close(session.GamingSessionId, "close-main")));
            Check("Eight close retries keep one end and invoice", closed.All(x => x.EndTimeUtc == closed[0].EndTimeUtc
                && x.InvoiceId == closed[0].InvoiceId) && closed[0].InvoiceId > 0 && closed[0].ClosedByActorType == "Staff");
            await CheckAccounting(session.GamingSessionId);
            var closedBefore = await Snapshot(session.GamingSessionId);
            await Close(session.GamingSessionId, "another-close");
            Check("Repeat close with another key does not mutate committed result", closedBefore == await Snapshot(session.GamingSessionId));
            Check("Close replay after restart returns the saved invoice", (await Close(session.GamingSessionId, "close-main")).InvoiceId == closed[0].InvoiceId);
            await Status("Browser cannot set actor on close", HttpStatusCode.BadRequest,
                await Send(HttpMethod.Post, $"/api/admin/sessions/{session.GamingSessionId}/close", admin, new { actorType = "System" }, "bad-close"));

            const decimal largeRate = 999_999_999m;
            const decimal largeBalance = 3_000_000_000m;
            await Edit(async db => (await db.ComputerTypes.FindAsync(typeIds[0]))!.PricePerHour = largeRate);
            var workerSession = await OpenLargeBacklog("Worker");
            await Bill(workerSession.CustomerId);
            var workerCheckpoint = await CheckLargeCharges(workerSession.GamingSessionId, "Active");
            var billed = await Snapshot(workerSession.GamingSessionId);
            await Settle(workerSession.CustomerId, workerCheckpoint);
            Check("Retry at the same split billing checkpoint does not debit again",
                billed == await Snapshot(workerSession.GamingSessionId));

            var staffSession = await OpenLargeBacklog("Staff");
            var beforeLargeClose = await Snapshot(staffSession.GamingSessionId);
            fault.FailNextInvoice = 1;
            await Status("Large close reaches invoice after split debit", HttpStatusCode.InternalServerError,
                await Send(HttpMethod.Post, $"/api/admin/sessions/{staffSession.GamingSessionId}/close", admin, new { }, "close-large"));
            Check("Invoice failure rolls back all split debits and session state",
                fault.FailNextInvoice == 0 && beforeLargeClose == await Snapshot(staffSession.GamingSessionId));
            var staffClosed = await Close(staffSession.GamingSessionId, "close-large");
            await CheckLargeCharges(staffSession.GamingSessionId, "Ended");
            await CheckAccounting(staffSession.GamingSessionId);
            var afterLargeClose = await Snapshot(staffSession.GamingSessionId);
            var replayed = await Close(staffSession.GamingSessionId, "close-large");
            Check("Large staff close retry returns the same invoice and preserves accounting",
                staffClosed.ClosedByActorType == "Staff" && staffClosed.InvoiceId > 0
                && replayed.InvoiceId == staffClosed.InvoiceId && afterLargeClose == await Snapshot(staffSession.GamingSessionId));

            // A positive remainder may still be insufficient for the next second: P=10000, B=4 -> one second, charge 3, remainder 1.
            await Edit(async db => { var type = await db.ComputerTypes.FindAsync(typeIds[0]); type!.PricePerHour = 10000; });
            var poor = await NewCustomer(4);
            var poorPc = await CreateComputer("Auto-close");
            var poorSession = await Open(poorPc.ComputerId, poor, "poor");
            await Edit(async db =>
            {
                var row = await db.GamingSessions.FindAsync(poorSession.GamingSessionId); row!.StartTime = DateTime.UtcNow.AddMinutes(-1);
                var opener = await db.Employees.FindAsync(adminId); opener!.IsActive = false;
            });
            await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => Bill(poor)));
            await using (var scope = app.Services.CreateAsyncScope())
            {
                var db = Db(scope);
                var row = await db.GamingSessions.SingleAsync(x => x.GamingSessionId == poorSession.GamingSessionId);
                var invoice = await db.Invoices.SingleAsync(x => x.GamingSessionId == poorSession.GamingSessionId);
                Check("Two workers/restart use System despite disabled opener and positive remainder",
                    row.Status == "Ended" && row.ClosedByActorType == "System" && row.ClosedByEmployeeId is null
                    && row.OpenedByEmployeeId == adminId && row.CloseReason == "InsufficientBalance"
                    && row.EndTime == row.StartTime.AddSeconds(1) && row.TotalCost == 3
                    && invoice.ActorType == "System" && invoice.EmployeeId is null
                    && (await db.Customers.FindAsync(poor))!.WalletBalance == 1);
                Check("System key and invoice source are durable and unique",
                    row.CloseOperationKey == $"SessionAutoClose:{row.GamingSessionId}"
                    && await db.InvoiceIssueRequests.CountAsync(x => x.OperationKey == $"SessionAutoInvoice:{row.GamingSessionId}") == 1);
            }
            await CheckAccounting(poorSession.GamingSessionId);
            var autoBefore = await Snapshot(poorSession.GamingSessionId);
            await Bill(poor);
            Check("Restart billing after auto-close is a no-op", autoBefore == await Snapshot(poorSession.GamingSessionId));
            await Edit(async db => { (await db.Employees.FindAsync(adminId))!.IsActive = true; });

            // Different clients opening the same machine: exactly one may win.
            var racingPc = await CreateComputer("Race open");
            var racers = new[] { await NewCustomer(100000), await NewCustomer(100000) };
            var racing = await Task.WhenAll(racers.Select((id, i) => OpenResponse(racingPc.ComputerId, id, $"race-{i}")));
            Check("Different customers racing one machine get one winner and one conflict",
                racing.Count(x => x.StatusCode is HttpStatusCode.OK or HttpStatusCode.Created) == 1 && racing.Count(x => x.StatusCode == HttpStatusCode.Conflict) == 1);
            var winner = await Read<SessionDto>(racing.Single(x => x.StatusCode is HttpStatusCode.OK or HttpStatusCode.Created));
            foreach (var response in racing) response.Dispose();
            await Task.WhenAll((Task)Bill(winner.CustomerId), Close(winner.GamingSessionId, "race-close"));
            await using (var scope = app.Services.CreateAsyncScope())
                Check("Worker racing staff close leaves a single ended session",
                    (await Db(scope).GamingSessions.FindAsync(winner.GamingSessionId))!.Status == "Ended");
            await CheckAccounting(winner.GamingSessionId);

            // A wallet caller may still fail after settlement: closing and notification must rollback with it.
            var outerCustomer = await NewCustomer(4);
            var outerPc = await CreateComputer("Outer rollback");
            var outerSession = await Open(outerPc.ComputerId, outerCustomer, "outer");
            await Edit(async db => (await db.GamingSessions.FindAsync(outerSession.GamingSessionId))!.StartTime = DateTime.UtcNow.AddMinutes(-1));
            await using var outerPeer = await RealtimeTestPeer.ConnectAsync(app.GetTestServer(), await CustomerLogin(outerCustomer));
            var beforeOuter = await Snapshot(outerSession.GamingSessionId);
            await using (var scope = app.Services.CreateAsyncScope())
            {
                await using var tx = await Db(scope).Database.BeginTransactionAsync();
                await scope.ServiceProvider.GetRequiredService<ISessionRepository>().SettleInTransactionAsync(outerCustomer, DateTime.UtcNow, default);
                await tx.RollbackAsync();
                var marker = new RealtimeEvent(Guid.NewGuid(), "CheckBarrier", 1, DateTimeOffset.UtcNow);
                await scope.ServiceProvider.GetRequiredService<IRealtimePublisher>().PublishToAccountAsync(
                    RealtimeAccountType.Customer, outerCustomer, marker, default);
                Check("Rolled-back outer debit publishes no session/wallet event",
                    (await outerPeer.NextAsync()).GetProperty("target").GetString() == "CheckBarrier");
            }
            Check("Outer debit rollback also restores auto-close and its invoice", beforeOuter == await Snapshot(outerSession.GamingSessionId));
            await Bill(outerCustomer);
            await CheckAccounting(outerSession.GamingSessionId);

            // Even a matching sum must not hide a corrupt charging checkpoint.
            var corruptCustomer = await NewCustomer(100000);
            var corruptPc = await CreateComputer("Corrupt checkpoint");
            var corruptSession = await Open(corruptPc.ComputerId, corruptCustomer, "corrupt-checkpoint");
            await Edit(async db =>
            {
                var row = await db.GamingSessions.FindAsync(corruptSession.GamingSessionId);
                row!.StartTime = DateTime.UtcNow.AddMinutes(-2); row.LastChargedAt = row.StartTime.AddSeconds(60);
            });
            var corruptBefore = await Snapshot(corruptSession.GamingSessionId);
            try { await Bill(corruptCustomer); throw new InvalidOperationException("Expected inconsistent checkpoint rejection."); }
            catch (BusinessRuleException) { Check("Inconsistent checkpoint is rejected without silently repairing fees", true); }
            Check("Rejected checkpoint preserves financial and machine state", corruptBefore == await Snapshot(corruptSession.GamingSessionId));
            await Edit(async db => (await db.GamingSessions.FindAsync(corruptSession.GamingSessionId))!.Status = "Ended");
            var listed = await Read<SessionDto[]>(await Send(HttpMethod.Get, "/api/admin/sessions", admin));
            Check("Admin list returns both the ended legacy row without an end time and the active session",
                listed.Any(x => x.GamingSessionId == corruptSession.GamingSessionId && x.Status == "Ended" && x.EndTimeUtc is null)
                && listed.Any(x => x.GamingSessionId == workerSession.GamingSessionId && x.Status == "Active"));
            return checks;

            async Task<string> Login(string username)
            {
                using var response = await client.PostAsJsonAsync("/api/auth/login", new { username, password });
                var json = await Read<JsonElement>(response);
                return json.GetProperty("accessToken").GetString()!;
            }
            async Task<string> CustomerLogin(int id)
            {
                await using var scope = app.Services.CreateAsyncScope();
                var phone = await Db(scope).Customers.Where(x => x.CustomerId == id).Select(x => x.Phone).SingleAsync();
                using var response = await client.PostAsJsonAsync("/api/auth/customer/login", new { phone, password });
                return (await Read<JsonElement>(response)).GetProperty("accessToken").GetString()!;
            }
            async Task<int> NewCustomer(decimal balance)
            {
                await using var scope = app.Services.CreateAsyncScope();
                var customer = new Customer { FullName = "Session check", Phone = Guid.NewGuid().ToString("N")[..18], Status = "Active", WalletBalance = balance };
                customer.PasswordHash = scope.ServiceProvider.GetRequiredService<ICustomerPasswordHasher>().HashPassword(customer, password);
                Db(scope).Customers.Add(customer); await Db(scope).SaveChangesAsync(); return customer.CustomerId;
            }
            async Task<ComputerDto> CreateComputer(string name, int room = 0) => await Read<ComputerDto>(
                await Send(HttpMethod.Post, "/api/admin/computers", admin,
                    new { computerName = name, roomId = roomIds[room], computerTypeId = typeIds[room] }));
            Task<HttpResponseMessage> OpenResponse(int computerId, int customerId, string key) =>
                Send(HttpMethod.Post, "/api/admin/sessions", admin, new { computerId, customerId }, key);
            async Task<SessionDto> Open(int computerId, int customerId, string key) => await Read<SessionDto>(await OpenResponse(computerId, customerId, key));
            async Task<SessionDto> Close(int id, string key) => await Read<SessionDto>(
                await Send(HttpMethod.Post, $"/api/admin/sessions/{id}/close", admin, new { }, key));
            async Task<SessionDto> OpenLargeBacklog(string scenario)
            {
                var id = await NewCustomer(largeBalance);
                var machine = await CreateComputer($"Large backlog {scenario}");
                var openedSession = await Open(machine.ComputerId, id, $"large-{scenario}");
                await Edit(async db => (await db.GamingSessions.FindAsync(openedSession.GamingSessionId))!.StartTime = DateTime.UtcNow.AddHours(-2));
                return openedSession;
            }
            async Task<DateTime> CheckLargeCharges(int id, string expectedStatus)
            {
                await using var scope = app.Services.CreateAsyncScope();
                var db = Db(scope);
                var row = await db.GamingSessions.Include(x => x.Customer).SingleAsync(x => x.GamingSessionId == id);
                var charges = await db.SessionCharges.Include(x => x.WalletTransaction)
                    .Where(x => x.GamingSessionId == id).OrderBy(x => x.EndTime).ToArrayAsync();
                var payments = await db.Payments.Where(x => x.SessionCharge != null && x.SessionCharge.GamingSessionId == id)
                    .ToDictionaryAsync(x => x.SessionChargeId!.Value);
                var checkpoint = row.StartTime;
                var total = 0m;
                foreach (var charge in charges)
                {
                    total += charge.Amount;
                    var ticks = (charge.EndTime - row.StartTime).Ticks;
                    Check("Split charge fits wallet limit, advances the checkpoint and matches cumulative billing",
                        charge.Amount is > 0 and <= largeRate && charge.StartTime == checkpoint && charge.EndTime > checkpoint
                        && ticks % TimeSpan.TicksPerSecond == 0
                        && total == decimal.Ceiling(largeRate * (ticks / TimeSpan.TicksPerSecond) / 3600m));
                    Check("Split charge has one matching Wallet Payment and ledger debit",
                        charge.OperationKey == $"SessionCharge:{id}:{charge.EndTime.Ticks}"
                        && charge.WalletTransaction?.OperationKey == charge.OperationKey
                        && charge.WalletTransaction?.Amount == -charge.Amount
                        && payments.TryGetValue(charge.ChargeId, out var payment)
                        && payment.WalletTransactionId == charge.WalletTransactionId && payment.OperationKey == charge.OperationKey
                        && payment.Amount == charge.Amount && payment.Method == "Wallet" && payment.Status == "Succeeded");
                    checkpoint = charge.EndTime;
                }
                Check($"Large backlog settles with status {expectedStatus}",
                    row.Status == expectedStatus && charges.Length >= 2 && checkpoint >= row.StartTime.AddHours(2)
                    && row.LastChargedAt == checkpoint && row.TotalCost == total && row.Customer.WalletBalance == largeBalance - total);
                return DateTime.SpecifyKind(checkpoint, DateTimeKind.Utc);
            }
            async Task<SessionDto?> Bill(int id)
            { await using var scope = app.Services.CreateAsyncScope(); await scope.ServiceProvider.GetRequiredService<ISessionRepository>().BillAsync(id, default); return null; }
            async Task Settle(int id, DateTime now)
            {
                await using var scope = app.Services.CreateAsyncScope();
                await using var transaction = await Db(scope).Database.BeginTransactionAsync();
                await scope.ServiceProvider.GetRequiredService<ISessionRepository>().SettleInTransactionAsync(id, now, default);
                await transaction.CommitAsync();
            }
            async Task Edit(Func<ApplicationDbContext, Task> edit)
            { await using var scope = app.Services.CreateAsyncScope(); await edit(Db(scope)); await Db(scope).SaveChangesAsync(); }
            async Task<string> Snapshot(int id)
            {
                await using var scope = app.Services.CreateAsyncScope(); var db = Db(scope);
                var row = await db.GamingSessions.AsNoTracking().SingleAsync(x => x.GamingSessionId == id);
                var ids = await db.SessionCharges.Where(x => x.GamingSessionId == id).Select(x => x.ChargeId).ToArrayAsync();
                return JsonSerializer.Serialize(new { row.TotalCost, row.EndTime, row.LastChargedAt, row.ClosedByActorType, row.ClosingInvoiceId,
                    balance = await db.Customers.Where(x => x.CustomerId == row.CustomerId).Select(x => x.WalletBalance).SingleAsync(),
                    computer = await db.Computers.Where(x => x.ComputerId == row.ComputerId).Select(x => x.Status).SingleAsync(),
                    charges = ids.Length, payments = await db.Payments.CountAsync(x => x.SessionChargeId != null && ids.Contains(x.SessionChargeId.Value)),
                    ledger = await db.WalletTransactions.CountAsync(x => x.CustomerId == row.CustomerId),
                    invoices = await db.Invoices.CountAsync(x => x.GamingSessionId == id) });
            }
            async Task CheckAccounting(int id)
            {
                await using var scope = app.Services.CreateAsyncScope(); var db = Db(scope);
                var row = await db.GamingSessions.SingleAsync(x => x.GamingSessionId == id);
                var charges = await db.SessionCharges.Where(x => x.GamingSessionId == id).ToListAsync();
                var ids = charges.Select(x => x.ChargeId).ToArray();
                var payments = await db.Payments.Include(x => x.WalletTransaction)
                    .Where(x => x.SessionChargeId != null && ids.Contains(x.SessionChargeId.Value)).ToListAsync();
                var lines = await db.InvoiceDetails.Where(x => x.SessionChargeId != null && ids.Contains(x.SessionChargeId.Value)).ToListAsync();
                Check($"Session {id}: charges/ledger/Payment/invoice totals reconcile", row.TotalCost == charges.Sum(x => x.Amount)
                    && payments.Count == charges.Count && payments.All(x => x.Status == "Succeeded" && x.Amount == -x.WalletTransaction!.Amount)
                    && payments.Sum(x => x.Amount) == row.TotalCost && lines.Sum(x => x.Amount) == row.TotalCost
                    && lines.Select(x => x.SessionChargeId).Distinct().Count() == lines.Count);
            }
            async Task<HttpResponseMessage> Send(HttpMethod method, string path, string? token, object? body = null, string? key = null)
            {
                using var request = new HttpRequestMessage(method, path);
                if (token is not null) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                if (key is not null) request.Headers.Add("Idempotency-Key", key);
                if (body is not null) request.Content = JsonContent.Create(body);
                return await client.SendAsync(request);
            }
            async Task<T> Read<T>(HttpResponseMessage response)
            {
                using (response)
                {
                    if (!response.IsSuccessStatusCode) throw new InvalidOperationException($"Expected success, received {(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
                    return (await response.Content.ReadFromJsonAsync<T>())!;
                }
            }
        }
        finally
        {
            if (started) await app.StopAsync();
            await fixture.CleanupAsync();
        }
        void Check(string label, bool condition)
        { if (!condition) throw new InvalidOperationException(label); checks++; Console.WriteLine("PASS " + label); }
        async Task Status(string label, HttpStatusCode expected, HttpResponseMessage response)
        { using (response) { Check(label + $" (HTTP {(int)response.StatusCode})", response.StatusCode == expected); await Task.CompletedTask; } }
    }

    private static ApplicationDbContext Db(AsyncServiceScope scope) => scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    private static async Task<int> NextEvent(RealtimeTestPeer peer, string type)
    {
        for (var i = 0; i < 100; i++)
        {
            var message = await peer.NextAsync();
            if (message.GetProperty("target").GetString() == type)
                return message.GetProperty("arguments")[0].GetProperty("entityId").GetInt32();
        }
        throw new InvalidOperationException("Expected realtime event not received.");
    }
    private sealed class InvoiceFailure : SaveChangesInterceptor
    {
        public int FailNextInvoice;
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (eventData.Context!.ChangeTracker.Entries<Invoice>().Any(x => x.State == EntityState.Added)
                && Interlocked.Exchange(ref FailNextInvoice, 0) == 1)
                throw new InvalidOperationException("Injected failure after fee debit, before invoice persistence.");
            return base.SavingChangesAsync(eventData, result, cancellationToken);
        }
    }
}
