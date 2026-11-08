using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using CyberManagement.API.Controller;
using CyberManagement.Application.Interfaces.Services;
using CyberManagement.Domain.Entities;
using CyberManagement.Infrastructure.Persistance;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

internal static class CustomerChecks
{
    public static async Task<int> RunAsync(IServiceProvider services, HttpClient client,
        IReadOnlyDictionary<string, string> staffTokens, string customerToken)
    {
        var checks = 0;
        var admin = staffTokens["Admin"];
        const string list = "/api/admin/customers";
        const string own = "/api/customers/me";
        const string password = "Customer-created-password-2026!";
        await Expect("Anonymous cannot list customers", HttpStatusCode.Unauthorized, await Send(list));
        await Expect("Anonymous cannot read own wallet", HttpStatusCode.Unauthorized, await Send(own + "/wallet"));
        await Expect("Customer cannot list other customers", HttpStatusCode.Forbidden, await Send(list, customerToken));
        foreach (var (role, token) in staffTokens)
        {
            await Expect($"{role} customer.view policy", role == "Technician" ? HttpStatusCode.Forbidden : HttpStatusCode.OK,
                await Send(list, token));
            await Expect($"{role} cannot impersonate customer /me", HttpStatusCode.Forbidden, await Send(own, token));
        }

        using var me = await Read(own, customerToken);
        var customerId = me.RootElement.GetProperty("customerId").GetInt32();
        Check("Own profile is read from SQL identity", me.RootElement.GetProperty("phone").GetString() == "0900000001");
        Check("Profile excludes credentials and financial fields", ProfileFields(me.RootElement));
        foreach (var (role, token) in staffTokens)
            await Expect($"{role} wallet.view policy", role is "Admin" or "Manager" or "Cashier" ? HttpStatusCode.OK : HttpStatusCode.Forbidden,
                await Send($"{list}/{customerId}/wallet", token));
        await Expect("Customer cannot read wallet via admin route", HttpStatusCode.Forbidden,
            await Send($"{list}/{customerId}/wallet", customerToken));

        var create = new { fullName = "  SQL Created Customer  ", phone = " 0900000081 ", email = " created@example.test ", password };
        await Expect("Anonymous cannot create customers", HttpStatusCode.Unauthorized, await Post(list, null, create));
        await Expect("Customer cannot create customers", HttpStatusCode.Forbidden, await Post(list, customerToken, create));
        foreach (var role in new[] { "Waiter", "Technician" })
            await Expect($"{role} cannot create customers", HttpStatusCode.Forbidden, await Post(list, staffTokens[role], create));
        await Expect("Caller cannot supply balance, status or customer ID", HttpStatusCode.BadRequest,
            await Post(list, admin, new { fullName = "Rejected", phone = "0900000089", password,
                walletBalance = 999999m, status = "Locked", customerId = int.MaxValue }));
        using var createdResponse = await Post(list, admin, create);
        Check("Admin creates a customer", createdResponse.StatusCode == HttpStatusCode.Created);
        using var created = JsonDocument.Parse(await createdResponse.Content.ReadAsStringAsync());
        var createdId = created.RootElement.GetProperty("customerId").GetInt32();
        Check("Create returns canonical persisted profile only", ProfileFields(created.RootElement)
            && createdId != int.MaxValue && created.RootElement.GetProperty("fullName").GetString() == "SQL Created Customer"
            && created.RootElement.GetProperty("phone").GetString() == "0900000081"
            && created.RootElement.GetProperty("email").GetString() == "created@example.test"
            && created.RootElement.GetProperty("status").GetString() == "Active");
        Check("Create Location points to customer resource", createdResponse.Headers.Location?.ToString().EndsWith($"/{createdId}") == true);
        await using (var scope = services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var saved = await db.Customers.AsNoTracking().SingleAsync(row => row.CustomerId == createdId);
            var hasher = scope.ServiceProvider.GetRequiredService<ICustomerPasswordHasher>();
            Check("New context sees zero balance, Active status and a hashed password", saved.WalletBalance == 0
                && saved.Status == "Active" && saved.PasswordHash != password
                && hasher.VerifyHashedPassword(saved, saved.PasswordHash!, password) != PasswordVerificationResult.Failed);
            Check("Creating a customer does not invent wallet ledger", !await db.WalletTransactions.AnyAsync(row => row.CustomerId == createdId));
        }
        using (var reloaded = await Read($"{list}/{createdId}", admin))
            Check("Customer survives a new API request", reloaded.RootElement.GetProperty("customerId").GetInt32() == createdId);
        using (var searched = await Read(list + "?search=0900000081&pageSize=1", staffTokens["Waiter"]))
            Check("Search and pagination read persisted customer", searched.RootElement.GetProperty("totalCount").GetInt32() == 1
                && searched.RootElement.GetProperty("items")[0].GetProperty("customerId").GetInt32() == createdId
                && ProfileFields(searched.RootElement.GetProperty("items")[0]));
        using (var emptySearch = await Read(list + "?search=NoMatchingCustomer-2026", admin))
            Check("No search results return an empty page", emptySearch.RootElement.GetProperty("totalCount").GetInt32() == 0
                && emptySearch.RootElement.GetProperty("items").GetArrayLength() == 0);
        await Expect("Duplicate normalized phone returns 409", HttpStatusCode.Conflict, await Post(list, admin, create));
        foreach (var (role, phone) in new[] { ("Manager", "0900000082"), ("Cashier", "0900000083") })
            await Expect($"{role} customer.create policy", HttpStatusCode.Created,
                await Post(list, staffTokens[role], new { fullName = role + " customer", phone, password }));

        var invalid = new object?[]
        {
            null, new { }, new { fullName = " ", phone = "0900000091", password },
            new { fullName = new string('n', 151), phone = "0900000091", password },
            new { fullName = "Invalid", phone = "abcdefghi", password },
            new { fullName = "Invalid", phone = "123", password },
            new { fullName = "Invalid", phone = "0900000091", password = "short" },
            new { fullName = "Invalid", phone = "0900000091", password = new string('p', 129) },
            new { fullName = "Invalid", phone = "0900000091", password, email = "not-an-email" }
        };
        for (var i = 0; i < invalid.Length; i++)
            await Expect($"Invalid customer body {i} returns 400", HttpStatusCode.BadRequest, await Post(list, admin, invalid[i]));
        foreach (var suffix in new[] { "?page=0", "?page=-1", "?pageSize=0", "?pageSize=101", "?page=not-an-int" })
        {
            await Expect("Invalid customer pagination " + suffix, HttpStatusCode.BadRequest, await Send(list + suffix, admin));
            await Expect("Invalid wallet pagination " + suffix, HttpStatusCode.BadRequest, await Send(own + "/wallet" + suffix, customerToken));
        }
        foreach (var id in new[] { "0", "-1", "not-an-int", "2147483648" })
            await Expect("Invalid customer ID " + id, HttpStatusCode.BadRequest, await Send(list + "/" + id, admin));
        await Expect("Missing customer returns 404", HttpStatusCode.NotFound, await Send(list + "/2147483647", admin));
        await Expect("Missing wallet returns 404", HttpStatusCode.NotFound, await Send(list + "/2147483647/wallet", admin));
        await Expect("Customer cannot request someone else's admin profile", HttpStatusCode.Forbidden,
            await Send($"{list}/{createdId}", customerToken));
        using (var injected = await Read(own + "?customerId=" + createdId, customerToken))
            Check("Query parameter cannot override JWT ownership", injected.RootElement.GetProperty("customerId").GetInt32() == customerId);

        using var loginResponse = await client.PostAsJsonAsync("/api/auth/customer/login", new { phone = "0900000081", password });
        Check("Newly created customer can log in", loginResponse.StatusCode == HttpStatusCode.OK);
        var login = await loginResponse.Content.ReadFromJsonAsync<CustomerLoginResponse>()
            ?? throw new InvalidOperationException("Created customer login returned no body.");
        using (var emptyWallet = await Read(own + "/wallet", login.AccessToken))
            Check("New customer wallet returns SQL zero and empty ledger", emptyWallet.RootElement.GetProperty("balance").GetDecimal() == 0
                && emptyWallet.RootElement.GetProperty("transactions").GetArrayLength() == 0
                && emptyWallet.RootElement.GetProperty("totalCount").GetInt32() == 0);
        await using (var scope = services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var current = await db.Customers.SingleAsync(row => row.CustomerId == customerId);
            current.FullName = "Updated SQL profile";
            current.WalletBalance = 125000.50m;
            var instant = new DateTime(2026, 11, 8, 8, 0, 0, DateTimeKind.Utc);
            db.WalletTransactions.AddRange(
                new WalletTransaction { CustomerId = customerId, Amount = 100000.50m, BalanceAfter = 100000.50m, Type = "TopUp", CreatedAt = instant.AddDays(-1) },
                new WalletTransaction { CustomerId = customerId, Amount = -25000m, BalanceAfter = 75000.50m, Type = "OrderPayment", Description = "SQL debit", CreatedAt = instant },
                new WalletTransaction { CustomerId = customerId, Amount = 50000m, BalanceAfter = 125000.50m, Type = "OrderRefund", Description = "SQL refund", CreatedAt = instant });
            await db.SaveChangesAsync();
        }
        using (var fresh = await Read(own, customerToken))
            Check("Profile reads SQL changes rather than stale JWT name", fresh.RootElement.GetProperty("fullName").GetString() == "Updated SQL profile");
        using var wallet = await Read(own + "/wallet?pageSize=2", customerToken);
        using var adminWallet = await Read($"{list}/{customerId}/wallet?pageSize=2", admin);
        Check("Staff and customer see identical wallet snapshot", wallet.RootElement.ToString() == adminWallet.RootElement.ToString());
        var entries = wallet.RootElement.GetProperty("transactions");
        Check("SQL balance, count and ledger page are returned", wallet.RootElement.GetProperty("balance").GetDecimal() == 125000.50m
            && wallet.RootElement.GetProperty("totalCount").GetInt32() == 3 && entries.GetArrayLength() == 2);
        Check("Ledger sorting handles equal timestamps with descending ID", entries[0].GetProperty("type").GetString() == "OrderRefund"
            && entries[1].GetProperty("type").GetString() == "OrderPayment");
        Check("Ledger preserves signed amounts and decimal balances", entries[0].GetProperty("amount").GetDecimal() == 50000m
            && entries[1].GetProperty("amount").GetDecimal() == -25000m
            && entries[0].GetProperty("balanceAfter").GetDecimal() == 125000.50m);
        Check("Ledger timestamp explicitly identifies UTC", entries[0].GetProperty("createdAtUtc").GetString()!.EndsWith('Z'));
        using (var page2 = await Read(own + "/wallet?page=2&pageSize=2", customerToken))
            Check("Ledger page two is stable", page2.RootElement.GetProperty("transactions").GetArrayLength() == 1
                && page2.RootElement.GetProperty("transactions")[0].GetProperty("amount").GetDecimal() == 100000.50m);
        using (var noLeak = await Read(own + "/wallet", login.AccessToken))
            Check("Other customer's ledger stays private", noLeak.RootElement.GetProperty("customerId").GetInt32() == createdId
                && noLeak.RootElement.GetProperty("transactions").GetArrayLength() == 0);

        await using (var scope = services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            if (db.Database.IsSqlServer())
            {
                var race = new { fullName = "Phone race", phone = "0900000099", password };
                var results = await Task.WhenAll(Post(list, admin, race), Post(list, admin, race));
                try
                {
                    Check("Concurrent duplicate phone creates one row and returns 409", results.Count(row => row.StatusCode == HttpStatusCode.Created) == 1
                        && results.Count(row => row.StatusCode == HttpStatusCode.Conflict) == 1
                        && await db.Customers.CountAsync(row => row.Phone == race.phone) == 1);
                }
                finally { foreach (var result in results) result.Dispose(); }
            }
            else Console.WriteLine("SKIP concurrent unique-phone test requires SQL Server.");
            var current = await db.Customers.SingleAsync(row => row.CustomerId == customerId);
            current.Status = "Locked";
            await db.SaveChangesAsync();
        }
        await Expect("Previously issued token cannot read a now-locked customer's profile", HttpStatusCode.Forbidden, await Send(own, customerToken));
        await Expect("Previously issued token cannot read a now-locked customer's wallet", HttpStatusCode.Forbidden, await Send(own + "/wallet", customerToken));
        await using (var scope = services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var current = await db.Customers.SingleAsync(row => row.CustomerId == customerId);
            current.Status = "Active";
            await db.SaveChangesAsync();
        }
        return checks;

        Task<HttpResponseMessage> Send(string path, string? token = null) => Request(HttpMethod.Get, path, token, null);
        Task<HttpResponseMessage> Post(string path, string? token, object? body) => Request(HttpMethod.Post, path, token, body);
        async Task<HttpResponseMessage> Request(HttpMethod method, string path, string? token, object? body)
        {
            using var request = new HttpRequestMessage(method, path);
            if (token is not null) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            if (method == HttpMethod.Post) request.Content = JsonContent.Create(body);
            return await client.SendAsync(request);
        }
        async Task<JsonDocument> Read(string path, string token)
        {
            using var response = await Send(path, token);
            if (response.StatusCode != HttpStatusCode.OK)
                throw new InvalidOperationException($"{path}: expected 200, got {(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
            return JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        }
        async Task Expect(string name, HttpStatusCode status, HttpResponseMessage response)
        {
            using (response)
                Check(name + $" ({(int)response.StatusCode})", response.StatusCode == status);
            await Task.CompletedTask;
        }
        void Check(string name, bool condition)
        {
            if (!condition) throw new InvalidOperationException(name);
            checks++;
            Console.WriteLine("PASS " + name);
        }
    }

    private static bool ProfileFields(JsonElement profile) => profile.EnumerateObject().Select(property => property.Name).ToHashSet().SetEquals(
        new[] { "customerId", "fullName", "phone", "email", "status", "createdAtUtc" });
}
