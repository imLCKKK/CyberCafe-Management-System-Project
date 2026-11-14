using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using CyberManagement.API.Realtime;
using CyberManagement.Application.DTOs.Computers;
using CyberManagement.Application.DTOs.Realtime;
using CyberManagement.Application.Interfaces.Repositories;
using CyberManagement.Application.Interfaces.Services;
using CyberManagement.Application.Services;
using CyberManagement.Infrastructure.Persistance;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;

internal static class RealtimeChecks
{
    public static async Task<int> RunAsync(WebApplication app, HttpClient client,
        IReadOnlyDictionary<string, string> staffTokens, string customerToken, RealtimeSaveFailure saveFailure)
    {
        var checks = 0;
        var adminToken = staffTokens["Admin"];
        var handler = new JwtSecurityTokenHandler();
        var adminId = int.Parse(handler.ReadJwtToken(adminToken).Subject);
        var observerId = int.Parse(handler.ReadJwtToken(staffTokens["Waiter"]).Subject);
        var noPermissionId = adminId + 100;
        var otherCustomerId = adminId + 10;
        var noPermissionToken = Mint(adminToken, claims =>
        {
            claims.RemoveAll(claim => claim.Type == "permission");
            Replace(claims, "sub", noPermissionId.ToString());
        });
        // Deliberately signed test claims verify customer/staff separation even if IDs overlap.
        var sameIdCustomerToken = Mint(customerToken, claims =>
        {
            Replace(claims, "sub", adminId.ToString());
            Replace(claims, "customer_id", adminId.ToString());
            claims.Add(new Claim("permission", "computer.view"));
        });
        var otherCustomerToken = Mint(sameIdCustomerToken, claims =>
        {
            Replace(claims, "sub", otherCustomerId.ToString());
            Replace(claims, "customer_id", otherCustomerId.ToString());
        });

        await ExpectStatus("Anonymous hub negotiation is rejected", HttpStatusCode.Unauthorized,
            await SendAsync(HttpMethod.Post, $"{RealtimeHub.Endpoint}/negotiate?negotiateVersion=1", null));
        await ExpectStatus("Tampered hub token is rejected", HttpStatusCode.Unauthorized,
            await SendAsync(HttpMethod.Post, $"{RealtimeHub.Endpoint}/negotiate?negotiateVersion=1", adminToken + "tampered"));
        await ExpectStatus("Expired hub token is rejected", HttpStatusCode.Unauthorized,
            await SendAsync(HttpMethod.Post, $"{RealtimeHub.Endpoint}/negotiate?negotiateVersion=1",
                Mint(adminToken, expiresAt: DateTime.UtcNow.AddMinutes(-2))));
        foreach (var (name, token) in new[]
        {
            ("Invalid account ID", Mint(adminToken, claims => Replace(claims, "sub", "0"))),
            ("Unsupported principal", Mint(adminToken, claims => Replace(claims, "account_type", "Other"))),
            ("Mismatched customer identity", Mint(customerToken, claims => Replace(claims, "customer_id", "999999")))
        })
            await ExpectStatus($"{name} cannot negotiate the hub", HttpStatusCode.Forbidden,
                await SendAsync(HttpMethod.Post, $"{RealtimeHub.Endpoint}/negotiate?negotiateVersion=1", token));

        foreach (var (role, token) in staffTokens)
            await ExpectStatus($"{role} can negotiate an authenticated hub connection", HttpStatusCode.OK,
                await SendAsync(HttpMethod.Post, $"{RealtimeHub.Endpoint}/negotiate?negotiateVersion=1", token));
        await ExpectStatus("Customer can negotiate its own account connection", HttpStatusCode.OK,
            await SendAsync(HttpMethod.Post, $"{RealtimeHub.Endpoint}/negotiate?negotiateVersion=1", customerToken));
        await ExpectStatus("API does not accept query-string JWTs", HttpStatusCode.Unauthorized,
            await SendAsync(HttpMethod.Get, $"/api/auth/me?access_token={Uri.EscapeDataString(adminToken)}", null));
        await ExpectStatus("Hub negotiate does not accept query-string JWTs", HttpStatusCode.Unauthorized,
            await SendAsync(HttpMethod.Post,
                $"{RealtimeHub.Endpoint}/negotiate?negotiateVersion=1&access_token={Uri.EscapeDataString(adminToken)}", null));

        await using var admin = await RealtimeTestPeer.ConnectAsync(app.GetTestServer(), adminToken, queryToken: true);
        await using var observer = await RealtimeTestPeer.ConnectAsync(app.GetTestServer(), staffTokens["Waiter"]);
        await using var noPermission = await RealtimeTestPeer.ConnectAsync(app.GetTestServer(), noPermissionToken);
        await using var customer = await RealtimeTestPeer.ConnectAsync(app.GetTestServer(), sameIdCustomerToken);
        await using var otherCustomer = await RealtimeTestPeer.ConnectAsync(app.GetTestServer(), otherCustomerToken);
        Check("Header and browser query-token WebSockets finish joining server-assigned groups", true);

        await using var publisherScope = app.Services.CreateAsyncScope();
        var publisher = publisherScope.ServiceProvider.GetRequiredService<IRealtimePublisher>();
        await noPermission.SendAsync(new
        {
            type = 1, invocationId = "join-attempt", target = "JoinGroup",
            arguments = new[] { RealtimeGroups.ForPermission("computer.view") }
        });
        var deniedJoin = await noPermission.NextAsync();
        Check("Clients cannot invoke a group-join method", deniedJoin.GetProperty("type").GetInt32() == 3
            && deniedJoin.GetProperty("invocationId").GetString() == "join-attempt"
            && deniedJoin.TryGetProperty("error", out _));

        int computerId;
        string initialStatus;
        await using (var scope = app.Services.CreateAsyncScope())
        {
            var computer = await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>()
                .Computers.AsNoTracking().SingleAsync(row => row.ComputerName == "PC-02");
            computerId = computer.ComputerId;
            initialStatus = computer.Status;
        }
        var changedStatus = initialStatus == "Available" ? "Maintenance" : "Available";
        var changeStarted = DateTimeOffset.UtcNow;
        var patch = PatchAsync(changedStatus, initialStatus);
        var changed = Event(await admin.NextAsync(), RealtimeEvent.ComputerChanged);
        await using (var scope = app.Services.CreateAsyncScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            Check("Computer event is observed only after data is visible from a fresh DbContext",
                await database.Computers.AnyAsync(row => row.ComputerId == computerId && row.Status == changedStatus));
        }
        await ExpectStatus("Committed change keeps the existing HTTP response contract", HttpStatusCode.OK, await patch);
        var observerEvent = Event(await observer.NextAsync(), RealtimeEvent.ComputerChanged);
        Check("A read-only staff account receives the same permission-scoped event",
            observerEvent.GetProperty("eventId").GetGuid() == changed.GetProperty("eventId").GetGuid());
        var allowedFields = new HashSet<string>(["eventId", "type", "entityId", "occurredAtUtc"]);
        Check("Event is an invalidation hint without customer, wallet or session payload",
            changed.EnumerateObject().Select(property => property.Name).ToHashSet().SetEquals(allowedFields)
            && changed.GetProperty("eventId").GetGuid() != Guid.Empty
            && changed.GetProperty("entityId").GetInt32() == computerId
            && changed.GetProperty("occurredAtUtc").GetDateTimeOffset() >= changeStarted);
        await ExpectNoEvents(noPermission, RealtimeAccountType.Staff, noPermissionId, "Staff without computer.view receives no machine event");
        await ExpectNoEvents(customer, RealtimeAccountType.Customer, adminId, "Customer permission claim cannot enter staff groups");
        await ExpectNoEvents(otherCustomer, RealtimeAccountType.Customer, otherCustomerId, "Other customer receives no machine event");

        await ExpectStatus("Retry succeeds without another notification", HttpStatusCode.OK, await PatchAsync(changedStatus, initialStatus));
        await ExpectNoEvents(admin, RealtimeAccountType.Staff, adminId, "Idempotent retry emits no event");
        await ExpectNoEvents(observer, RealtimeAccountType.Staff, observerId, "Retry emits no duplicate to other viewers");
        await ExpectStatus("Stale status returns 409", HttpStatusCode.Conflict, await PatchAsync(initialStatus, initialStatus));
        await ExpectStatus("Invalid status returns 400", HttpStatusCode.BadRequest, await PatchAsync("Occupied", changedStatus));
        await ExpectNoEvents(admin, RealtimeAccountType.Staff, adminId, "Validation and conflict failures emit no event");
        await ExpectNoEvents(observer, RealtimeAccountType.Staff, observerId, "Failed requests do not notify other viewers");

        Interlocked.Exchange(ref saveFailure.FailNextSave, 1);
        await ExpectStatus("Injected persistence failure returns 500", HttpStatusCode.InternalServerError,
            await PatchAsync(initialStatus, changedStatus));
        Check("Failure injection reached SaveChanges", saveFailure.FailNextSave == 0);
        await using (var scope = app.Services.CreateAsyncScope())
            Check("Failed save leaves persisted machine state intact", await scope.ServiceProvider
                .GetRequiredService<ApplicationDbContext>().Computers.AsNoTracking()
                .AnyAsync(row => row.ComputerId == computerId && row.Status == changedStatus));
        await ExpectNoEvents(admin, RealtimeAccountType.Staff, adminId, "Persistence failure emits no event");
        await ExpectNoEvents(observer, RealtimeAccountType.Staff, observerId, "Persistence failure emits no event to other viewers");

        var privateEvent = NewEvent("AccountChanged", 1);
        await publisher.PublishToAccountAsync(RealtimeAccountType.Customer, adminId, privateEvent, CancellationToken.None);
        Check("Private event reaches only the intended customer account",
            Event(await customer.NextAsync(), "AccountChanged").GetProperty("eventId").GetGuid() == privateEvent.EventId);
        await ExpectNoEvents(admin, RealtimeAccountType.Staff, adminId, "Same numeric staff ID cannot receive customer event");
        await ExpectNoEvents(otherCustomer, RealtimeAccountType.Customer, otherCustomerId, "Another customer cannot receive private event");
        var staffEvent = NewEvent("AccountChanged", 2);
        await publisher.PublishToAccountAsync(RealtimeAccountType.Staff, adminId, staffEvent, CancellationToken.None);
        Check("Staff account event reaches its intended recipient",
            Event(await admin.NextAsync(), "AccountChanged").GetProperty("eventId").GetGuid() == staffEvent.EventId);
        await ExpectNoEvents(customer, RealtimeAccountType.Customer, adminId, "Same numeric customer ID cannot receive staff event");

        var logger = new WarningLogger();
        var failingPublisher = new SignalRRealtimePublisher(new UnavailableHubContext(), logger);
        await using (var scope = app.Services.CreateAsyncScope())
        {
            var service = new ComputerService(scope.ServiceProvider.GetRequiredService<IComputerRepository>(), failingPublisher);
            var saved = await service.UpdateStatusAsync(computerId,
                new UpdateComputerStatusCommand(initialStatus, changedStatus), CancellationToken.None);
            Check("Notification failure cannot turn a committed save into a failed operation",
                saved.Status == initialStatus && logger.Warnings == 1);
        }
        await using (var scope = app.Services.CreateAsyncScope())
            Check("Data remains committed when SignalR delivery fails", await scope.ServiceProvider
                .GetRequiredService<ApplicationDbContext>().Computers.AsNoTracking()
                .AnyAsync(row => row.ComputerId == computerId && row.Status == initialStatus));

        await using (var reconnected = await RealtimeTestPeer.ConnectAsync(app.GetTestServer(), staffTokens["Waiter"]))
        {
            var reconnectEvent = NewEvent("ComputerChanged", computerId);
            await publisher.PublishToPermissionAsync("computer.view", reconnectEvent, CancellationToken.None);
            Check("New connection is reauthenticated and rejoins its permission group",
                Event(await reconnected.NextAsync(), "ComputerChanged").GetProperty("eventId").GetGuid() == reconnectEvent.EventId);
        }
        await using (var expiring = await RealtimeTestPeer.ConnectAsync(app.GetTestServer(),
            Mint(adminToken, expiresAt: DateTime.UtcNow.AddSeconds(3))))
        {
            var closed = await expiring.NextAsync();
            Check("Hub closes an active connection when its JWT expires", closed.GetProperty("type").GetInt32() == 7);
        }

        return checks;

        static RealtimeEvent NewEvent(string type, int id) => new(Guid.NewGuid(), type, id, DateTimeOffset.UtcNow);

        JsonElement Event(JsonElement frame, string target)
        {
            if (frame.GetProperty("type").GetInt32() != 1 || frame.GetProperty("target").GetString() != target)
                throw new InvalidOperationException($"Expected {target}; received {frame.GetRawText()}");
            return frame.GetProperty("arguments")[0];
        }

        async Task ExpectNoEvents(RealtimeTestPeer peer, RealtimeAccountType type, int id, string name)
        {
            // A per-account marker is ordered after prior sends, avoiding sleep-based absence checks.
            var marker = NewEvent("CheckBarrier", 1);
            await publisher.PublishToAccountAsync(type, id, marker, CancellationToken.None);
            var received = Event(await peer.NextAsync(), "CheckBarrier");
            Check(name, received.GetProperty("eventId").GetGuid() == marker.EventId);
        }

        Task<HttpResponseMessage> PatchAsync(string status, string expectedStatus) =>
            SendAsync(HttpMethod.Patch, $"/api/admin/computers/{computerId}/status", adminToken, new { status, expectedStatus });

        async Task<HttpResponseMessage> SendAsync(HttpMethod method, string path, string? token, object? body = null)
        {
            using var request = new HttpRequestMessage(method, path);
            if (token is not null) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            if (body is not null) request.Content = JsonContent.Create(body);
            return await client.SendAsync(request);
        }

        Task ExpectStatus(string name, HttpStatusCode status, HttpResponseMessage response)
        {
            using (response)
                Check($"{name}: expected {(int)status}, got {(int)response.StatusCode}", response.StatusCode == status);
            return Task.CompletedTask;
        }

        void Check(string name, bool passed)
        {
            if (!passed) throw new InvalidOperationException(name);
            checks++;
            Console.WriteLine($"PASS {name}");
        }

        string Mint(string template, Action<List<Claim>>? edit = null, DateTime? expiresAt = null)
        {
            var claims = handler.ReadJwtToken(template).Claims
                .Where(claim => claim.Type is not ("exp" or "nbf" or "iat" or "iss" or "aud")).ToList();
            edit?.Invoke(claims);
            var credentials = new SigningCredentials(new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(app.Configuration["Authentication:Jwt:SigningKey"]!)), SecurityAlgorithms.HmacSha256);
            return handler.WriteToken(new JwtSecurityToken("CyberManagement", "CyberManagement.Client", claims,
                notBefore: DateTime.UtcNow.AddMinutes(-10), expires: expiresAt ?? DateTime.UtcNow.AddMinutes(5),
                signingCredentials: credentials));
        }
    }

    private static void Replace(List<Claim> claims, string type, string value)
    {
        claims.RemoveAll(claim => claim.Type == type);
        claims.Add(new Claim(type, value));
    }

    private sealed class UnavailableHubContext : IHubContext<RealtimeHub>
    {
        public IHubClients Clients => throw new IOException("Injected SignalR transport failure.");
        public IGroupManager Groups => throw new NotSupportedException();
    }

    private sealed class WarningLogger : ILogger<SignalRRealtimePublisher>
    {
        public int Warnings { get; private set; }
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel level) => true;
        public void Log<TState>(LogLevel level, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (level == LogLevel.Warning) Warnings++;
        }
    }
}
