using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using CyberManagement.API.Controller;
using CyberManagement.Application.DTOs.Identity;
using CyberManagement.Application.DTOs.Realtime;
using CyberManagement.Application.Interfaces.Services;
using CyberManagement.Domain.Entities;
using CyberManagement.Infrastructure.Persistance;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

internal static class CounterTopUpChecks
{
    public static async Task<int> RunAsync(WebApplication app, HttpClient client,
        IReadOnlyDictionary<string, string> staffTokens, string customerToken, RealtimeSaveFailure saveFailure)
    {
        await using (var providerScope = app.Services.CreateAsyncScope())
        {
            if (!providerScope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Database.IsSqlServer())
            {
                Console.WriteLine("SKIP counter cash request/approval checks require SQL Server; InMemory cannot prove money transactions.");
                return 0;
            }
        }
        const string own = "/api/customers/me/wallet/counter-topups";
        const string adminPath = "/api/admin/topups";
        const string password = "Counter-check-password-2026!";
        var checks = 0;
        var admin = staffTokens["Admin"];
        var denominations = new[] { (10000m, 0m), (20000m, 0m), (30000m, 0m), (50000m, 5000m),
            (100000m, 15000m), (200000m, 35000m), (300000m, 50000m), (500000m, 100000m) };

        await Expect("Anonymous cannot inspect counter options", HttpStatusCode.Unauthorized, await Get(own + "/options"));
        await Expect("Anonymous cannot submit a cash request", HttpStatusCode.Unauthorized,
            await Post(own, null, new { amount = 10000 }, Guid.NewGuid().ToString()));
        await Expect("Customer cannot inspect the staff cash queue", HttpStatusCode.Forbidden, await Get(adminPath, customerToken));
        foreach (var (role, token) in staffTokens)
        {
            await Expect($"{role} cash queue policy", role is "Admin" or "Manager" or "Cashier"
                ? HttpStatusCode.OK : HttpStatusCode.Forbidden, await Get(adminPath, token));
            await Expect($"{role} cannot impersonate a customer cash request", HttpStatusCode.Forbidden,
                await Post(own, token, new { amount = 10000 }, Guid.NewGuid().ToString()));
        }
        using (var options = await Read(own + "/options", customerToken))
        {
            Check("Server exposes exactly the eight approved cash amounts", options.RootElement.GetArrayLength() == denominations.Length);
            foreach (var (amount, bonus) in denominations)
            {
                var option = options.RootElement.EnumerateArray().Single(row => row.GetProperty("amount").GetDecimal() == amount);
                Check($"Cash {amount} exposes server gift {bonus} and total", option.GetProperty("bonusAmount").GetDecimal() == bonus
                    && option.GetProperty("totalAmount").GetDecimal() == amount + bonus);
            }
        }

        var (fixtureId, fixtureToken) = await CreateCustomer("0790000101", "Counter denominations");
        var (_, otherToken) = await CreateCustomer("0790000102", "Other counter owner");
        using (var empty = await Read(own, fixtureToken))
            Check("Fresh customer has a persisted empty cash queue", empty.RootElement.GetProperty("totalCount").GetInt32() == 0
                && empty.RootElement.GetProperty("items").GetArrayLength() == 0);
        foreach (var badAmount in new[] { -1m, 0m, 1m, 15000m, 10000.5m, 999999999m })
            await Expect($"Invalid cash amount {badAmount} is rejected", HttpStatusCode.BadRequest,
                await Post(own, fixtureToken, new { amount = badAmount }, Guid.NewGuid().ToString()));
        await Expect("Caller cannot inject a gift or credited total", HttpStatusCode.BadRequest,
            await Post(own, fixtureToken, new { amount = 50000, bonusAmount = 900000, totalAmount = 950000 }, Guid.NewGuid().ToString()));
        await Expect("Caller cannot choose another cash customer", HttpStatusCode.BadRequest,
            await Post(own, fixtureToken, new { amount = 50000, customerId = fixtureId + 1 }, Guid.NewGuid().ToString()));
        foreach (var key in new string?[] { null, "", "not-a-guid", Guid.Empty.ToString() })
            await Expect("Missing or invalid idempotency UUID is rejected", HttpStatusCode.BadRequest,
                await Post(own, fixtureToken, new { amount = 10000 }, key));
        foreach (var query in new[] { "?page=0", "?pageSize=0", "?pageSize=101" })
            await Expect("Invalid cash history pagination is rejected", HttpStatusCode.BadRequest, await Get(own + query, fixtureToken));
        await Expect("Unknown queue status is rejected", HttpStatusCode.BadRequest, await Get(adminPath + "?status=Unknown", admin));

        decimal expectedBalance = 0;
        int firstRequestId = 0;
        string? firstKey = null;
        foreach (var (amount, bonus) in denominations)
        {
            var key = Guid.NewGuid().ToString();
            using var created = await Post(own, fixtureToken, new { amount }, key);
            Check($"Cash {amount} creates one Pending request", created.StatusCode == HttpStatusCode.Created);
            using var pending = await Json(created);
            var requestId = pending.RootElement.GetProperty("requestId").GetInt32();
            if (firstRequestId == 0) { firstRequestId = requestId; firstKey = key; }
            Check($"Cash {amount} snapshots exactly the approved gift", pending.RootElement.GetProperty("amount").GetDecimal() == amount
                && pending.RootElement.GetProperty("bonusAmount").GetDecimal() == bonus
                && pending.RootElement.GetProperty("totalAmount").GetDecimal() == amount + bonus
                && pending.RootElement.GetProperty("status").GetString() == "Pending");
            await using (var scope = app.Services.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                Check("A request never credits before cash is confirmed", await db.Customers.Where(row => row.CustomerId == fixtureId)
                    .Select(row => row.WalletBalance).SingleAsync() == expectedBalance
                    && await db.WalletTransactions.CountAsync(row => row.CustomerId == fixtureId) == Array.IndexOf(denominations, (amount, bonus)));
            }
            using (var repeated = await Post(own, fixtureToken, new { amount }, key))
            {
                Check("Identical create retry returns the saved request", repeated.StatusCode == HttpStatusCode.OK);
                using var result = await Json(repeated);
                Check("Create replay preserves ID and all three saved amounts", result.RootElement.GetProperty("requestId").GetInt32() == requestId
                    && result.RootElement.GetProperty("amount").GetDecimal() == amount
                    && result.RootElement.GetProperty("bonusAmount").GetDecimal() == bonus
                    && result.RootElement.GetProperty("totalAmount").GetDecimal() == amount + bonus);
            }
            await Expect("Reusing a create key for a different amount conflicts", HttpStatusCode.Conflict,
                await Post(own, fixtureToken, new { amount = amount == 10000 ? 20000 : 10000 }, key));
            await Expect("A second distinct key cannot create another Pending request", HttpStatusCode.Conflict,
                await Post(own, fixtureToken, new { amount }, Guid.NewGuid().ToString()));
            using (var otherHistory = await Read(own + "?customerId=" + fixtureId, otherToken))
                Check("Customer ID query cannot reveal another customer's requests", otherHistory.RootElement.GetProperty("items").GetArrayLength() == 0);
            await Expect("Customer cannot approve even its own cash request", HttpStatusCode.Forbidden,
                await Post($"{adminPath}/{requestId}/approve", fixtureToken, new { cashReceived = true, cashAmount = amount }));
            foreach (var role in new[] { "Waiter", "Technician" })
                await Expect($"{role} cannot approve a cash request", HttpStatusCode.Forbidden,
                    await Post($"{adminPath}/{requestId}/approve", staffTokens[role], new { cashReceived = true, cashAmount = amount }));
            await Expect("Approval explicitly requires confirmation of received cash", HttpStatusCode.BadRequest,
                await Post($"{adminPath}/{requestId}/approve", admin, new { cashReceived = false, cashAmount = amount }));
            await Expect("Cash collection excludes the gift", HttpStatusCode.BadRequest,
                await Post($"{adminPath}/{requestId}/approve", admin, new { cashReceived = true, cashAmount = amount + 1 }));
            await Expect("Approval cannot inject a different credited total", HttpStatusCode.BadRequest,
                await Post($"{adminPath}/{requestId}/approve", admin, new { cashReceived = true, cashAmount = amount, totalAmount = 999999 }));

            var approver = amount == 20000 ? staffTokens["Manager"] : amount == 30000 ? staffTokens["Cashier"] : admin;
            using var approvedResponse = await Post($"{adminPath}/{requestId}/approve", approver, new { cashReceived = true, cashAmount = amount });
            Check("Authorized staff approval succeeds", approvedResponse.StatusCode == HttpStatusCode.OK);
            using var approved = await Json(approvedResponse);
            expectedBalance += amount + bonus;
            var transactionId = approved.RootElement.GetProperty("transactionId").GetInt32();
            Check("Approved response identifies the committed credit", approved.RootElement.GetProperty("status").GetString() == "Approved"
                && approved.RootElement.GetProperty("balanceAfter").GetDecimal() == expectedBalance
                && approved.RootElement.GetProperty("approvedByEmployeeId").GetInt32() == int.Parse(new JwtSecurityTokenHandler().ReadJwtToken(approver).Subject)
                && approved.RootElement.GetProperty("approvedAtUtc").GetDateTimeOffset() <= DateTimeOffset.UtcNow);
            await using (var scope = app.Services.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var entry = await db.WalletTransactions.AsNoTracking().SingleAsync(row => row.TransactionId == transactionId);
                Check("Cash plus gift produces exactly one matching SQL ledger", entry.CustomerId == fixtureId
                    && entry.Amount == amount + bonus && entry.BalanceAfter == expectedBalance && entry.Type == "TopUp"
                    && await db.WalletTransactions.CountAsync(row => row.CustomerId == fixtureId) == Array.IndexOf(denominations, (amount, bonus)) + 1
                    && await db.Customers.Where(row => row.CustomerId == fixtureId).Select(row => row.WalletBalance).SingleAsync() == expectedBalance);
            }
            using (var replay = await Post($"{adminPath}/{requestId}/approve", admin, new { cashReceived = true, cashAmount = amount }))
            {
                Check("Approval retry succeeds without a second credit", replay.StatusCode == HttpStatusCode.OK);
                using var replayed = await Json(replay);
                Check("Approval retry preserves the original result and actor", replayed.RootElement.ToString() == approved.RootElement.ToString());
            }
        }

        using (var oldCreate = await Post(own, fixtureToken, new { amount = 10000 }, firstKey))
        {
            Check("Old create retry still resolves after subsequent requests", oldCreate.StatusCode == HttpStatusCode.OK);
            using var result = await Json(oldCreate);
            Check("Old create retry does not reset the approved snapshot", result.RootElement.GetProperty("requestId").GetInt32() == firstRequestId
                && result.RootElement.GetProperty("status").GetString() == "Approved"
                && result.RootElement.GetProperty("totalAmount").GetDecimal() == 10000);
        }
        using (var history = await Read(own + "?pageSize=3", fixtureToken))
            Check("Cash history persists and paginates after reload", history.RootElement.GetProperty("totalCount").GetInt32() == 8
                && history.RootElement.GetProperty("items").GetArrayLength() == 3
                && history.RootElement.GetProperty("items")[0].GetProperty("amount").GetDecimal() == 500000);
        using (var wallet = await Read("/api/customers/me/wallet", fixtureToken))
            Check("Wallet API reads the same credited SQL balance and ledger", wallet.RootElement.GetProperty("balance").GetDecimal() == expectedBalance
                && wallet.RootElement.GetProperty("totalCount").GetInt32() == 8);
        await Expect("Missing request approval returns 404", HttpStatusCode.NotFound,
            await Post(adminPath + "/2147483647/approve", admin, new { cashReceived = true, cashAmount = 10000 }));

        var (lockedId, lockedToken) = await CreateCustomer("0790000103", "Locked pending owner");
        using var lockedCreated = await Post(own, lockedToken, new { amount = 50000 }, Guid.NewGuid().ToString());
        Check("Active owner can start a cash request", lockedCreated.StatusCode == HttpStatusCode.Created);
        using var lockedJson = await Json(lockedCreated);
        var lockedRequestId = lockedJson.RootElement.GetProperty("requestId").GetInt32();
        await SetStatus(lockedId, "Locked");
        await Expect("Locked owner cannot create a cash request with an old JWT", HttpStatusCode.Forbidden,
            await Post(own, lockedToken, new { amount = 10000 }, Guid.NewGuid().ToString()));
        await Expect("Locked owner cannot read cash history with an old JWT", HttpStatusCode.Forbidden, await Get(own, lockedToken));
        await Expect("Cash approval cannot credit a now-locked customer", HttpStatusCode.Forbidden,
            await Post($"{adminPath}/{lockedRequestId}/approve", admin, new { cashReceived = true, cashAmount = 50000 }));
        await using (var scope = app.Services.CreateAsyncScope())
            Check("Refused locked approval leaves balance and ledger unchanged", await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>()
                .Customers.Where(row => row.CustomerId == lockedId).Select(row => row.WalletBalance).SingleAsync() == 0);

        await RunRealtimeAndRollback();
        await using (var scope = app.Services.CreateAsyncScope())
        {
            if (scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Database.IsSqlServer())
                await RunSqlRaces();
            else Console.WriteLine("SKIP counter concurrency/rollback isolation checks require SQL Server.");
        }
        return checks;

        async Task RunRealtimeAndRollback()
        {
            var (id, token) = await CreateCustomer("0790000104", "Realtime counter owner");
            await using var owner = await RealtimeTestPeer.ConnectAsync(app.GetTestServer(), token);
            await using var observer = await RealtimeTestPeer.ConnectAsync(app.GetTestServer(), otherToken);
            await using var staff = await RealtimeTestPeer.ConnectAsync(app.GetTestServer(), admin);
            await using var forbiddenStaff = await RealtimeTestPeer.ConnectAsync(app.GetTestServer(), staffTokens["Waiter"]);
            using var publisherScope = app.Services.CreateAsyncScope();
            var publisher = publisherScope.ServiceProvider.GetRequiredService<IRealtimePublisher>();
            using var created = await Post(own, token, new { amount = 100000 }, Guid.NewGuid().ToString());
            Check("Realtime cash request creates successfully", created.StatusCode == HttpStatusCode.Created);
            using var pending = await Json(created);
            var requestId = pending.RootElement.GetProperty("requestId").GetInt32();
            Check("New request notifies the owning customer", Event(await owner.NextAsync(), "CounterTopUpChanged").GetProperty("entityId").GetInt32() == requestId);
            Check("New request notifies staff with wallet.topup", Event(await staff.NextAsync(), "CounterTopUpChanged").GetProperty("entityId").GetInt32() == requestId);
            await Barrier(observer, RealtimeAccountType.Customer, await IdFor(otherToken), "Other customer receives no cash request event");
            await Barrier(forbiddenStaff, RealtimeAccountType.Staff, await IdFor(staffTokens["Waiter"]), "Staff without wallet.topup receives no cash request event");
            Interlocked.Exchange(ref saveFailure.FailNextSave, 1);
            await Expect("Injected cash approval persistence failure returns 500", HttpStatusCode.InternalServerError,
                await Post($"{adminPath}/{requestId}/approve", admin, new { cashReceived = true, cashAmount = 100000 }));
            Check("Cash approval failure reached persistence", saveFailure.FailNextSave == 0);
            await using (var scope = app.Services.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                Check("Approval rollback keeps the request Pending with no wallet credit", await db.Customers.Where(row => row.CustomerId == id)
                    .Select(row => row.WalletBalance).SingleAsync() == 0
                    && !await db.WalletTransactions.AnyAsync(row => row.CustomerId == id));
            }
            using (var history = await Read(own, token))
                Check("A failed approval cannot mark the request Approved", history.RootElement.GetProperty("items")[0].GetProperty("status").GetString() == "Pending");
            await Barrier(owner, RealtimeAccountType.Customer, id, "Rolled-back cash approval emits no customer event");
            await Barrier(staff, RealtimeAccountType.Staff, await IdFor(admin), "Rolled-back cash approval emits no staff event");
            var approval = Post($"{adminPath}/{requestId}/approve", admin, new { cashReceived = true, cashAmount = 100000 });
            var frames = new[] { await owner.NextAsync(), await owner.NextAsync() };
            Check("Committed approval invalidates both wallet and request for its owner", frames.Select(frame => frame.GetProperty("target").GetString())
                .ToHashSet().SetEquals(["WalletChanged", "CounterTopUpChanged"]));
            await using (var scope = app.Services.CreateAsyncScope())
                Check("Wallet event arrives only after its SQL credit is visible", await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>()
                    .Customers.Where(row => row.CustomerId == id).Select(row => row.WalletBalance).SingleAsync() == 115000);
            Check("Approval notifies the authorized staff queue", Event(await staff.NextAsync(), "CounterTopUpChanged").GetProperty("entityId").GetInt32() == requestId);
            Check("Staff with wallet.view receives a balance invalidation after commit", Event(await staff.NextAsync(), "WalletChanged")
                .GetProperty("entityId").GetInt32() == id);
            await Expect("Retry after rollback successfully credits once", HttpStatusCode.OK, await approval);
            await Barrier(observer, RealtimeAccountType.Customer, await IdFor(otherToken), "Other customer receives no wallet credit event");
            await Barrier(forbiddenStaff, RealtimeAccountType.Staff, await IdFor(staffTokens["Waiter"]), "Staff without wallet.topup receives no approval event");
            await Expect("Committed approval can be replayed", HttpStatusCode.OK,
                await Post($"{adminPath}/{requestId}/approve", admin, new { cashReceived = true, cashAmount = 100000 }));
            await Barrier(owner, RealtimeAccountType.Customer, id, "Approval replay emits no duplicate wallet event");
            await Barrier(staff, RealtimeAccountType.Staff, await IdFor(admin), "Approval replay emits no duplicate queue event");

            async Task Barrier(RealtimeTestPeer peer, RealtimeAccountType type, int accountId, string name)
            {
                var marker = new RealtimeEvent(Guid.NewGuid(), "CounterCheckBarrier", 1, DateTimeOffset.UtcNow);
                await publisher.PublishToAccountAsync(type, accountId, marker, CancellationToken.None);
                Check(name, Event(await peer.NextAsync(), marker.Type).GetProperty("eventId").GetGuid() == marker.EventId);
            }
        }

        async Task RunSqlRaces()
        {
            var (id, token) = await CreateCustomer("0790000105", "Concurrent cash owner");
            var sameKey = Guid.NewGuid().ToString();
            var same = await Task.WhenAll(Post(own, token, new { amount = 200000 }, sameKey), Post(own, token, new { amount = 200000 }, sameKey));
            int requestId;
            try
            {
                Check("Concurrent same-key cash create returns one create and one replay", same.Count(row => row.StatusCode == HttpStatusCode.Created) == 1
                    && same.Count(row => row.StatusCode == HttpStatusCode.OK) == 1);
                using var first = await Json(same[0]);
                using var second = await Json(same[1]);
                requestId = first.RootElement.GetProperty("requestId").GetInt32();
                Check("Concurrent create replay resolves one saved request", second.RootElement.GetProperty("requestId").GetInt32() == requestId);
            }
            finally { foreach (var response in same) response.Dispose(); }
            var approvals = await Task.WhenAll(
                Post($"{adminPath}/{requestId}/approve", admin, new { cashReceived = true, cashAmount = 200000 }),
                Post($"{adminPath}/{requestId}/approve", staffTokens["Cashier"], new { cashReceived = true, cashAmount = 200000 }));
            try
            {
                Check("Simultaneous staff approvals both resolve successfully", approvals.All(row => row.StatusCode == HttpStatusCode.OK));
                using var first = await Json(approvals[0]);
                using var second = await Json(approvals[1]);
                Check("Simultaneous staff approvals preserve the same receipt and actor", first.RootElement.ToString() == second.RootElement.ToString());
            }
            finally { foreach (var response in approvals) response.Dispose(); }
            await using (var scope = app.Services.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                Check("Concurrent approvals credit cash plus gift exactly once in SQL", await db.Customers.Where(row => row.CustomerId == id)
                    .Select(row => row.WalletBalance).SingleAsync() == 235000
                    && await db.WalletTransactions.CountAsync(row => row.CustomerId == id) == 1
                    && await db.WalletTransactions.Where(row => row.CustomerId == id).SumAsync(row => row.Amount) == 235000);
            }
            var (differentId, differentToken) = await CreateCustomer("0790000106", "Concurrent distinct keys");
            var different = await Task.WhenAll(
                Post(own, differentToken, new { amount = 50000 }, Guid.NewGuid().ToString()),
                Post(own, differentToken, new { amount = 100000 }, Guid.NewGuid().ToString()));
            try
            {
                Check("Concurrent different-key requests allow exactly one Pending", different.Count(row => row.StatusCode == HttpStatusCode.Created) == 1
                    && different.Count(row => row.StatusCode == HttpStatusCode.Conflict) == 1);
            }
            finally { foreach (var response in different) response.Dispose(); }
            using var history = await Read(own, differentToken);
            Check("Distinct-key create race leaves one request and no uncollected credit", history.RootElement.GetProperty("totalCount").GetInt32() == 1);
            await using var verify = app.Services.CreateAsyncScope();
            Check("Create race never changes the customer balance", await verify.ServiceProvider.GetRequiredService<ApplicationDbContext>()
                .Customers.Where(row => row.CustomerId == differentId).Select(row => row.WalletBalance).SingleAsync() == 0);
        }

        async Task<(int Id, string Token)> CreateCustomer(string phone, string name)
        {
            int id;
            await using (var scope = app.Services.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var row = new Customer { FullName = name, Phone = phone, Status = "Active", WalletBalance = 0 };
                row.PasswordHash = scope.ServiceProvider.GetRequiredService<ICustomerPasswordHasher>().HashPassword(row, password);
                db.Customers.Add(row);
                await db.SaveChangesAsync();
                id = row.CustomerId;
            }
            using var response = await client.PostAsJsonAsync("/api/auth/customer/login", new { phone, password });
            if (response.StatusCode != HttpStatusCode.OK) throw new InvalidOperationException("Counter fixture customer login failed.");
            var login = await response.Content.ReadFromJsonAsync<CustomerLoginResponse>() ?? throw new InvalidOperationException("No counter fixture JWT.");
            return (id, login.AccessToken);
        }
        async Task SetStatus(int id, string status)
        {
            await using var scope = app.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            (await db.Customers.SingleAsync(row => row.CustomerId == id)).Status = status;
            await db.SaveChangesAsync();
        }
        Task<int> IdFor(string token) => Task.FromResult(int.Parse(new JwtSecurityTokenHandler().ReadJwtToken(token).Subject));
        static JsonElement Event(JsonElement frame, string target)
        {
            if (frame.GetProperty("type").GetInt32() != 1 || frame.GetProperty("target").GetString() != target)
                throw new InvalidOperationException($"Expected {target}; received {frame.GetRawText()}");
            return frame.GetProperty("arguments")[0];
        }
        Task<HttpResponseMessage> Get(string path, string? token = null) => Request(HttpMethod.Get, path, token, null, null);
        Task<HttpResponseMessage> Post(string path, string? token, object body, string? key = null) => Request(HttpMethod.Post, path, token, body, key);
        async Task<HttpResponseMessage> Request(HttpMethod method, string path, string? token, object? body, string? key)
        {
            using var request = new HttpRequestMessage(method, path);
            if (token is not null) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            if (key is not null) request.Headers.Add("Idempotency-Key", key);
            if (body is not null) request.Content = JsonContent.Create(body);
            return await client.SendAsync(request);
        }
        static async Task<JsonDocument> Json(HttpResponseMessage response) => JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        async Task<JsonDocument> Read(string path, string token)
        {
            using var response = await Get(path, token);
            if (response.StatusCode != HttpStatusCode.OK)
                throw new InvalidOperationException($"{path}: expected 200, got {(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
            return await Json(response);
        }
        Task Expect(string name, HttpStatusCode status, HttpResponseMessage response)
        {
            using (response) Check($"{name} (expected {(int)status}, got {(int)response.StatusCode})", response.StatusCode == status);
            return Task.CompletedTask;
        }
        void Check(string name, bool condition)
        {
            if (!condition) throw new InvalidOperationException(name);
            checks++;
            Console.WriteLine("PASS " + name);
        }
    }
}
