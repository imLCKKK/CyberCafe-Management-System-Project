using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using CyberManagement.Domain.Entities;
using CyberManagement.Infrastructure.Persistance;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

internal static class TournamentChecks
{
    public static async Task<int> RunAsync(
        IServiceProvider services, HttpClient client,
        IReadOnlyDictionary<string, string> staffTokens, string customerToken)
    {
        var checks = 0;
        var adminToken = staffTokens["Admin"];
        int customerId;
        int gameId;
        int rankId;
        await using (var scope = services.CreateAsyncScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            customerId = int.Parse(new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler()
                .ReadJwtToken(customerToken).Claims.Single(claim => claim.Type == "customer_id").Value);
            var game = new Game { GameName = "Tournament API checks", TeamSize = 5, IsActive = true };
            var rank = new GameRank { Game = game, RankName = "Test rank", RankOrder = 10 };
            database.AddRange(game, rank);
            await database.SaveChangesAsync();
            gameId = game.GameId;
            rankId = rank.RankId;
        }

        await ExpectStatus("Anonymous cannot list tournaments", HttpStatusCode.Unauthorized,
            await SendAsync(HttpMethod.Get, "/api/client/tournaments", null));
        await ExpectStatus("Staff cannot use customer tournament endpoints", HttpStatusCode.Forbidden,
            await SendAsync(HttpMethod.Get, "/api/client/tournaments", adminToken));
        await ExpectStatus("Waiter without tournament.view cannot read admin tournaments", HttpStatusCode.Forbidden,
            await SendAsync(HttpMethod.Get, "/api/admin/tournaments", staffTokens["Waiter"]));
        await ExpectStatus("Cashier cannot manage tournaments", HttpStatusCode.Forbidden,
            await SendAsync(HttpMethod.Post, "/api/admin/tournaments", staffTokens["Cashier"], CreateBody(gameId, 0)));

        var createBody = CreateBody(gameId, 0);
        var createKey = Guid.NewGuid().ToString();
        var draft = await ReadSuccessAsync<JsonElement>(await SendAsync(
            HttpMethod.Post, "/api/admin/tournaments", adminToken, createBody, createKey));
        var tournamentId = draft.GetProperty("id").GetInt32();
        Check("Admin creates a SQL tournament in Draft", draft.GetProperty("status").GetString() == "Draft"
            && draft.GetProperty("createdByEmployeeId").GetInt32() > 0);
        var replayResponse = await SendAsync(HttpMethod.Post, "/api/admin/tournaments", adminToken, createBody, createKey);
        Check("Tournament create replay is marked", replayResponse.StatusCode == HttpStatusCode.OK
            && replayResponse.Headers.TryGetValues("Idempotency-Replayed", out var replayHeader)
            && replayHeader.Single() == "true");
        var replay = await ReadSuccessAsync<JsonElement>(replayResponse);
        Check("Tournament create replay returns the same ID", replay.GetProperty("id").GetInt32() == tournamentId);
        await ExpectStatus("Create key cannot be reused with different content", HttpStatusCode.Conflict,
            await SendAsync(HttpMethod.Post, "/api/admin/tournaments", adminToken, CreateBody(gameId, 0), createKey));
        await ExpectStatus("Create rejects malformed idempotency key", HttpStatusCode.BadRequest,
            await SendAsync(HttpMethod.Post, "/api/admin/tournaments", adminToken, createBody, "bad"));
        await using (var scope = services.CreateAsyncScope())
        {
            if (scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Database.IsSqlServer())
            {
                var concurrentBody = CreateBody(gameId, 0);
                var concurrentKey = Guid.NewGuid().ToString();
                var responses = await Task.WhenAll(Enumerable.Range(0, 6).Select(_ =>
                    SendAsync(HttpMethod.Post, "/api/admin/tournaments", adminToken, concurrentBody, concurrentKey)));
                try
                {
                    var statuses = string.Join(", ", responses.Select(row => (int)row.StatusCode));
                    Check($"Concurrent create retries yield one create and five replays ({statuses})",
                        responses.Count(row => row.StatusCode == HttpStatusCode.Created) == 1
                        && responses.Count(row => row.StatusCode == HttpStatusCode.OK) == 5);
                    var rows = await Task.WhenAll(responses.Select(row => row.Content.ReadFromJsonAsync<JsonElement>()));
                    Check("Concurrent create retries all return the same tournament",
                        rows.Select(row => row.GetProperty("id").GetInt32()).Distinct().Count() == 1);
                }
                finally
                {
                    foreach (var response in responses) response.Dispose();
                }
            }
        }
        var clientListBeforeOpen = await ReadSuccessAsync<JsonElement[]>(await SendAsync(
            HttpMethod.Get, "/api/client/tournaments", customerToken));
        Check("Client list does not expose Draft tournaments",
            clientListBeforeOpen.All(row => row.GetProperty("id").GetInt32() != tournamentId));
        await ExpectStatus("Manager can read admin tournament list", HttpStatusCode.OK,
            await SendAsync(HttpMethod.Get, "/api/admin/tournaments", staffTokens["Manager"]));
        await ExpectStatus("Admin can read a tournament by ID", HttpStatusCode.OK,
            await SendAsync(HttpMethod.Get, $"/api/admin/tournaments/{tournamentId}", adminToken));
        await ExpectStatus("Client cannot read a draft tournament by ID", HttpStatusCode.NotFound,
            await SendAsync(HttpMethod.Get, $"/api/client/tournaments/{tournamentId}", customerToken));

        var adminId = int.Parse(new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler()
            .ReadJwtToken(adminToken).Claims.Single(claim => claim.Type == "sub").Value);
        await SetAdminActiveAsync(false);
        try
        {
            foreach (var path in new[]
            {
                "/api/admin/games", $"/api/admin/games/{gameId}/ranks", "/api/admin/tournaments",
                $"/api/admin/tournaments/{tournamentId}", $"/api/admin/tournaments/{tournamentId}/registrations"
            })
                await ExpectStatus("Inactive staff cannot read tournament data with an old token", HttpStatusCode.Forbidden,
                    await SendAsync(HttpMethod.Get, path, adminToken));
            await ExpectStatus("Inactive staff cannot create a tournament with an old token", HttpStatusCode.Forbidden,
                await SendAsync(HttpMethod.Post, "/api/admin/tournaments", adminToken, CreateBody(gameId, 0)));
            await ExpectStatus("Inactive staff cannot edit a tournament with an old token", HttpStatusCode.Forbidden,
                await SendAsync(HttpMethod.Put, $"/api/admin/tournaments/{tournamentId}", adminToken,
                    EditBody(gameId, draft.GetProperty("version").GetString()!, 0)));
            await ExpectStatus("Inactive staff cannot open a tournament with an old token", HttpStatusCode.Forbidden,
                await SendAsync(HttpMethod.Post, $"/api/admin/tournaments/{tournamentId}/transitions", adminToken,
                    new { toStatus = "RegistrationOpen" }));
        }
        finally
        {
            await SetAdminActiveAsync(true);
        }

        var initialVersion = draft.GetProperty("version").GetString()!;
        var edit = await ReadSuccessAsync<JsonElement>(await SendAsync(HttpMethod.Put,
            $"/api/admin/tournaments/{tournamentId}", adminToken, EditBody(gameId, initialVersion, 0)));
        Check("Admin can edit a Draft tournament", edit.GetProperty("name").GetString() == "Updated tournament");
        await using (var scope = services.CreateAsyncScope())
        {
            if (scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Database.IsSqlServer())
            {
                await ExpectStatus("Stale tournament version cannot overwrite a newer edit", HttpStatusCode.Conflict,
                    await SendAsync(HttpMethod.Put, $"/api/admin/tournaments/{tournamentId}", adminToken,
                        EditBody(gameId, initialVersion, 0)));
                var currentVersion = edit.GetProperty("version").GetString()!;
                var writes = await Task.WhenAll(Enumerable.Range(0, 2).Select(_ =>
                    SendAsync(HttpMethod.Put, $"/api/admin/tournaments/{tournamentId}", adminToken,
                        EditBody(gameId, currentVersion, 0))));
                try
                {
                    Check($"Concurrent tournament edits yield one success and one conflict ({string.Join(", ", writes.Select(row => (int)row.StatusCode))})",
                        writes.Count(row => row.StatusCode == HttpStatusCode.OK) == 1
                        && writes.Count(row => row.StatusCode == HttpStatusCode.Conflict) == 1);
                }
                finally
                {
                    foreach (var response in writes) response.Dispose();
                }
            }
        }
        var opened = await ReadSuccessAsync<JsonElement>(await SendAsync(
            HttpMethod.Post, $"/api/admin/tournaments/{tournamentId}/transitions", adminToken,
            new { toStatus = "RegistrationOpen" }));
        Check("Admin opens registration and status is persisted", opened.GetProperty("status").GetString() == "RegistrationOpen");
        await ExpectStatus("Client can read an open tournament by ID", HttpStatusCode.OK,
            await SendAsync(HttpMethod.Get, $"/api/client/tournaments/{tournamentId}", customerToken));

        var ranks = await ReadSuccessAsync<JsonElement[]>(await SendAsync(
            HttpMethod.Get, $"/api/client/games/{gameId}/ranks", customerToken));
        Check("Client can choose ranks from the tournament's game", ranks.Any(row => row.GetProperty("rankId").GetInt32() == rankId));

        var createdRegistration = await ReadSuccessAsync<JsonElement>(await SendAsync(
            HttpMethod.Post, $"/api/client/tournaments/{tournamentId}/registrations", customerToken,
            new { customerId = customerId + 999, riotId = "Player#VN1", rankId, preferredRole = "Flex" }));
        Check("Registration uses the authenticated customer instead of a supplied customerId",
            createdRegistration.GetProperty("customerId").GetInt32() == customerId
            && createdRegistration.GetProperty("riotId").GetString() == "Player#VN1");
        await ExpectStatus("Duplicate registration is rejected", HttpStatusCode.Conflict,
            await SendAsync(HttpMethod.Post, $"/api/client/tournaments/{tournamentId}/registrations", customerToken,
                new { riotId = "Player#VN1", rankId, preferredRole = "Flex" }));
        await ExpectStatus("Invalid Riot ID is rejected", HttpStatusCode.BadRequest,
            await SendAsync(HttpMethod.Post, $"/api/client/tournaments/{tournamentId}/registrations", customerToken,
                new { riotId = "not-a-riot-id", rankId, preferredRole = "Flex" }));

        var publicParticipants = await ReadSuccessAsync<JsonElement[]>(await SendAsync(
            HttpMethod.Get, $"/api/client/tournaments/{tournamentId}/registrations", customerToken));
        Check("Client participant list reads the committed registration and marks own account",
            publicParticipants.Length == 1
            && publicParticipants[0].GetProperty("isCurrentCustomer").GetBoolean()
            && !publicParticipants[0].TryGetProperty("customerId", out _));
        var adminParticipants = await ReadSuccessAsync<JsonElement[]>(await SendAsync(
            HttpMethod.Get, $"/api/admin/tournaments/{tournamentId}/registrations", adminToken));
        Check("Admin and client participant lists share the SQL registration",
            adminParticipants.Length == 1
            && adminParticipants[0].GetProperty("customerId").GetInt32() == customerId);
        await using (var scope = services.CreateAsyncScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var persisted = await database.TournamentRegistrations.AsNoTracking()
                .SingleAsync(row => row.TournamentId == tournamentId);
            Check("Registration and Riot ID survive a fresh database context",
                persisted.CustomerId == customerId && persisted.RiotId == "Player#VN1" && persisted.Status == "Registered");
        }
        await ExpectStatus("Entry fee cannot be changed after registration", HttpStatusCode.Conflict,
            await SendAsync(HttpMethod.Put, $"/api/admin/tournaments/{tournamentId}", adminToken,
                EditBody(gameId, opened.GetProperty("version").GetString()!, 1000)));

        var paid = await ReadSuccessAsync<JsonElement>(await SendAsync(
            HttpMethod.Post, "/api/admin/tournaments", adminToken, CreateBody(gameId, 1000)));
        var paidId = paid.GetProperty("id").GetInt32();
        await ExpectStatus("Admin opens a paid tournament", HttpStatusCode.OK,
            await SendAsync(HttpMethod.Post, $"/api/admin/tournaments/{paidId}/transitions", adminToken,
                new { toStatus = "RegistrationOpen" }));
        await ExpectStatus("Paid registration is rejected until wallet settlement is implemented", HttpStatusCode.Conflict,
            await SendAsync(HttpMethod.Post, $"/api/client/tournaments/{paidId}/registrations", customerToken,
                new { riotId = "Player#VN2", rankId, preferredRole = "Flex" }));
        await using (var scope = services.CreateAsyncScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            Check("Rejected paid registration leaves no registration or wallet ledger row",
                !await database.TournamentRegistrations.AnyAsync(row => row.TournamentId == paidId)
                && !await database.WalletTransactions.AnyAsync(
                    row => row.CustomerId == customerId && row.Type == "TournamentFee"));
        }

        var closed = await ReadSuccessAsync<JsonElement>(await SendAsync(
            HttpMethod.Post, "/api/admin/tournaments", adminToken, CreateBody(gameId, 0)));
        var closedId = closed.GetProperty("id").GetInt32();
        await ExpectStatus("Admin opens tournament before closing it", HttpStatusCode.OK,
            await SendAsync(HttpMethod.Post, $"/api/admin/tournaments/{closedId}/transitions", adminToken,
                new { toStatus = "RegistrationOpen" }));
        await ExpectStatus("Admin can close registration", HttpStatusCode.OK,
            await SendAsync(HttpMethod.Post, $"/api/admin/tournaments/{closedId}/transitions", adminToken,
                new { toStatus = "RegistrationClosed" }));
        await ExpectStatus("Closed tournament rejects registrations", HttpStatusCode.Conflict,
            await SendAsync(HttpMethod.Post, $"/api/client/tournaments/{closedId}/registrations", customerToken,
                new { riotId = "Player#VN3", rankId, preferredRole = "Flex" }));

        return checks;

        async Task SetAdminActiveAsync(bool isActive)
        {
            await using var scope = services.CreateAsyncScope();
            var database = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var employee = await database.Employees.SingleAsync(row => row.EmployeeId == adminId);
            employee.IsActive = isActive;
            await database.SaveChangesAsync();
        }

        object CreateBody(int requestedGameId, decimal entryFee) => new
        {
            gameId = requestedGameId,
            name = $"Tournament {Guid.NewGuid():N}",
            registrationStartUtc = DateTime.UtcNow.AddMinutes(-2),
            registrationEndUtc = DateTime.UtcNow.AddDays(1),
            startTimeUtc = DateTime.UtcNow.AddDays(2),
            entryFee
        };

        object EditBody(int requestedGameId, string expectedVersion, decimal entryFee) => new
        {
            gameId = requestedGameId, name = "Updated tournament", registrationStartUtc = DateTime.UtcNow.AddMinutes(-2),
            registrationEndUtc = DateTime.UtcNow.AddDays(1), startTimeUtc = DateTime.UtcNow.AddDays(2),
            entryFee, expectedVersion
        };

        async Task<HttpResponseMessage> SendAsync(HttpMethod method, string path, string? token, object? body = null, string? key = null)
        {
            using var request = new HttpRequestMessage(method, path);
            if (token is not null) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            if (method == HttpMethod.Post && path == "/api/admin/tournaments")
                request.Headers.Add("Idempotency-Key", key ?? Guid.NewGuid().ToString());
            if (body is not null) request.Content = JsonContent.Create(body);
            return await client.SendAsync(request);
        }

        async Task<T> ReadSuccessAsync<T>(HttpResponseMessage response)
        {
            using (response)
            {
                if (!response.IsSuccessStatusCode)
                    throw new InvalidOperationException($"Expected success; received {(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
                checks++;
                return await response.Content.ReadFromJsonAsync<T>()
                    ?? throw new InvalidOperationException("Successful tournament response has no body.");
            }
        }

        async Task ExpectStatus(string name, HttpStatusCode expected, HttpResponseMessage response)
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
            if (!passed) throw new InvalidOperationException(name);
            checks++;
            Console.WriteLine($"PASS {name}");
        }
    }
}
