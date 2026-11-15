using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using CyberManagement.Application.Interfaces.Services;
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
                    new { toStatus = "RegistrationOpen", expectedVersion = draft.GetProperty("version").GetString() }));
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
        var current = await ReadSuccessAsync<JsonElement>(await SendAsync(
            HttpMethod.Get, $"/api/admin/tournaments/{tournamentId}", adminToken));
        var opened = await ReadSuccessAsync<JsonElement>(await SendAsync(
            HttpMethod.Post, $"/api/admin/tournaments/{tournamentId}/transitions", adminToken,
            new { toStatus = "RegistrationOpen", expectedVersion = current.GetProperty("version").GetString() }));
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
        var paidOpen = await ReadSuccessAsync<JsonElement>(await SendAsync(
            HttpMethod.Post, $"/api/admin/tournaments/{paidId}/transitions", adminToken,
            new { toStatus = "RegistrationOpen", expectedVersion = paid.GetProperty("version").GetString() }));
        var paidVersion = paidOpen.GetProperty("version").GetString()!;
        await using (var scope = services.CreateAsyncScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            if (database.Database.IsSqlServer())
            {
                await ExpectStatus("Legacy inconsistent sessions block paid registration", HttpStatusCode.Conflict,
                    await SendAsync(HttpMethod.Post, $"/api/client/tournaments/{paidId}/registrations", customerToken,
                        new { riotId = "Player#VN2", rankId, preferredRole = "Flex", expectedVersion = paidVersion }));
                Check("Rejected legacy session leaves no registration or fee",
                    !await database.TournamentRegistrations.AnyAsync(row => row.TournamentId == paidId)
                    && !await database.WalletTransactions.AnyAsync(row => row.CustomerId == customerId && row.Type == "TournamentFee"));
                await CheckPaidRegistrationsAsync();
            }
            else Console.WriteLine("SKIP paid tournament registration requires SQL Server.");
        }

        var closed = await ReadSuccessAsync<JsonElement>(await SendAsync(
            HttpMethod.Post, "/api/admin/tournaments", adminToken, CreateBody(gameId, 0)));
        var closedId = closed.GetProperty("id").GetInt32();
        var openBody = new { toStatus = "RegistrationOpen", expectedVersion = closed.GetProperty("version").GetString() };
        var openKey = Guid.NewGuid().ToString();
        await ExpectStatus("Transition rejects malformed idempotency key", HttpStatusCode.BadRequest,
            await SendAsync(HttpMethod.Post, $"/api/admin/tournaments/{closedId}/transitions", adminToken, openBody, "bad"));
        await ExpectStatus("Transition requires a version", HttpStatusCode.BadRequest,
            await SendAsync(HttpMethod.Post, $"/api/admin/tournaments/{closedId}/transitions", adminToken,
                new { toStatus = "RegistrationOpen" }));
        var firstOpen = await ReadSuccessAsync<JsonElement>(await SendAsync(
            HttpMethod.Post, $"/api/admin/tournaments/{closedId}/transitions", adminToken, openBody, openKey));
        var closeResult = await ReadSuccessAsync<JsonElement>(await SendAsync(
            HttpMethod.Post, $"/api/admin/tournaments/{closedId}/transitions", adminToken,
            new { toStatus = "RegistrationClosed", expectedVersion = firstOpen.GetProperty("version").GetString() }));
        var oldReplayResponse = await SendAsync(HttpMethod.Post, $"/api/admin/tournaments/{closedId}/transitions",
            adminToken, openBody, openKey);
        Check("Old open command replays with header after close", oldReplayResponse.StatusCode == HttpStatusCode.OK
            && oldReplayResponse.Headers.TryGetValues("Idempotency-Replayed", out var transitionReplay)
            && transitionReplay.Single() == "true");
        var oldReplay = await ReadSuccessAsync<JsonElement>(oldReplayResponse);
        Check("Transition replay returns original response", oldReplay.ToString() == firstOpen.ToString());
        var currentClosed = await ReadSuccessAsync<JsonElement>(await SendAsync(
            HttpMethod.Get, $"/api/admin/tournaments/{closedId}", adminToken));
        Check("Old open retry leaves tournament closed",
            currentClosed.GetProperty("status").GetString() == "RegistrationClosed"
            && currentClosed.GetProperty("version").GetString() == closeResult.GetProperty("version").GetString());
        await ExpectStatus("Transition key cannot be reused with changed payload", HttpStatusCode.Conflict,
            await SendAsync(HttpMethod.Post, $"/api/admin/tournaments/{closedId}/transitions", adminToken,
                new { toStatus = "RegistrationClosed", expectedVersion = openBody.expectedVersion }, openKey));
        await ExpectStatus("Transition key cannot be reused with changed version", HttpStatusCode.Conflict,
            await SendAsync(HttpMethod.Post, $"/api/admin/tournaments/{closedId}/transitions", adminToken,
                new { toStatus = "RegistrationOpen", expectedVersion = openBody.expectedVersion == "AAAAAAAAAAA=" ? "AAAAAAAAAAE=" : "AAAAAAAAAAA=" }, openKey));
        await using (var scope = services.CreateAsyncScope())
        {
            if (scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Database.IsSqlServer())
            {
                await ExpectStatus("New transition key with stale version is rejected", HttpStatusCode.Conflict,
                    await SendAsync(HttpMethod.Post, $"/api/admin/tournaments/{closedId}/transitions", adminToken, openBody));
                var reopenBody = new { toStatus = "RegistrationOpen", expectedVersion = closeResult.GetProperty("version").GetString() };
                var concurrentKey = Guid.NewGuid().ToString();
                var concurrent = await Task.WhenAll(Enumerable.Range(0, 6).Select(_ =>
                    SendAsync(HttpMethod.Post, $"/api/admin/tournaments/{closedId}/transitions", adminToken, reopenBody, concurrentKey)));
                try
                {
                    Check("Concurrent identical transitions execute once and replay five times",
                        concurrent.Count(response => response.StatusCode == HttpStatusCode.OK
                            && !response.Headers.Contains("Idempotency-Replayed")) == 1
                        && concurrent.Count(response => response.StatusCode == HttpStatusCode.OK
                            && response.Headers.TryGetValues("Idempotency-Replayed", out var values)
                            && values.Single() == "true") == 5);
                }
                finally { foreach (var response in concurrent) response.Dispose(); }
                var reopened = await ReadSuccessAsync<JsonElement>(await SendAsync(
                    HttpMethod.Get, $"/api/admin/tournaments/{closedId}", adminToken));
                var closedAgain = await ReadSuccessAsync<JsonElement>(await SendAsync(HttpMethod.Post,
                    $"/api/admin/tournaments/{closedId}/transitions", adminToken,
                    new { toStatus = "RegistrationClosed", expectedVersion = reopened.GetProperty("version").GetString() }));
                var competingBody = new { toStatus = "RegistrationOpen", expectedVersion = closedAgain.GetProperty("version").GetString() };
                var competing = await Task.WhenAll(Enumerable.Range(0, 2).Select(_ =>
                    SendAsync(HttpMethod.Post, $"/api/admin/tournaments/{closedId}/transitions", adminToken, competingBody)));
                try
                {
                    Check("Different transition keys with the same version yield one success and one conflict",
                        competing.Count(response => response.StatusCode == HttpStatusCode.OK) == 1
                        && competing.Count(response => response.StatusCode == HttpStatusCode.Conflict) == 1);
                }
                finally { foreach (var response in competing) response.Dispose(); }
                var finalOpen = await ReadSuccessAsync<JsonElement>(await SendAsync(
                    HttpMethod.Get, $"/api/admin/tournaments/{closedId}", adminToken));
                await ReadSuccessAsync<JsonElement>(await SendAsync(HttpMethod.Post,
                    $"/api/admin/tournaments/{closedId}/transitions", adminToken,
                    new { toStatus = "RegistrationClosed", expectedVersion = finalOpen.GetProperty("version").GetString() }));
            }
        }
        await ExpectStatus("Closed tournament rejects registrations", HttpStatusCode.Conflict,
            await SendAsync(HttpMethod.Post, $"/api/client/tournaments/{closedId}/registrations", customerToken,
                new { riotId = "Player#VN3", rankId, preferredRole = "Flex" }));

        return checks;

        async Task CheckPaidRegistrationsAsync()
        {
            var plain = await NewCustomerAsync("0790001001", 5000, false);
            var withSession = await NewCustomerAsync("0790001002", 5000, true);
            var insufficient = await NewCustomerAsync("0790001003", 1050, true);
            var legacySession = await NewCustomerAsync("0790001004", 5000, true, null);
            var request = new { riotId = "Player#VN2", rankId, preferredRole = "Flex", expectedVersion = paidVersion };
            var path = $"/api/client/tournaments/{paidId}/registrations";
            await ExpectStatus("Paid registration requires the displayed version", HttpStatusCode.Conflict,
                await SendAsync(HttpMethod.Post, path, plain.Token,
                    new { riotId = "Player#VN2", rankId, preferredRole = "Flex" }));
            await ExpectStatus("Paid registration rejects stale displayed tournament version", HttpStatusCode.Conflict,
                await SendAsync(HttpMethod.Post, path, plain.Token,
                    new { riotId = "Player#VN2", rankId, preferredRole = "Flex", expectedVersion = "AAAAAAAAAAA=" }));
            await ExpectStatus("Session without historical price snapshot cannot spend", HttpStatusCode.Conflict,
                await SendAsync(HttpMethod.Post, path, legacySession.Token, request));

            var concurrent = await Task.WhenAll(Enumerable.Range(0, 2).Select(_ =>
                SendAsync(HttpMethod.Post, path, plain.Token, request)));
            try
            {
                Check("Concurrent paid registrations yield one success and one conflict",
                    concurrent.Count(row => row.StatusCode == HttpStatusCode.Created) == 1
                    && concurrent.Count(row => row.StatusCode == HttpStatusCode.Conflict) == 1);
            }
            finally { foreach (var response in concurrent) response.Dispose(); }

            await ExpectStatus("Active session is settled before registration fee", HttpStatusCode.Created,
                await SendAsync(HttpMethod.Post, path, withSession.Token, request));
            await ExpectStatus("Insufficient balance after session settlement rejects registration", HttpStatusCode.Conflict,
                await SendAsync(HttpMethod.Post, path, insufficient.Token, request));

            await using var verifyScope = services.CreateAsyncScope();
            var db = verifyScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var plainRegistration = await db.TournamentRegistrations.AsNoTracking()
                .SingleAsync(row => row.CustomerId == plain.Id && row.TournamentId == paidId);
            var plainLedger = await db.WalletTransactions.AsNoTracking()
                .SingleAsync(row => row.CustomerId == plain.Id && row.Type == "TournamentFee");
            Check("Concurrent retry debits once and stores the fee snapshot and ledger link",
                plainRegistration.FeeCharged == 1000 && plainRegistration.ChargeTransactionId == plainLedger.TransactionId
                && plainLedger.Amount == -1000 && plainLedger.BalanceAfter == 4000
                && (await db.Customers.FindAsync(plain.Id))!.WalletBalance == 4000);
            var session = await db.GamingSessions.AsNoTracking().SingleAsync(row => row.CustomerId == withSession.Id);
            var charge = await db.SessionCharges.AsNoTracking().SingleAsync(row => row.GamingSessionId == session.GamingSessionId);
            var gamingLedger = await db.WalletTransactions.AsNoTracking()
                .SingleAsync(row => row.CustomerId == withSession.Id && row.Type == "GamingCharge");
            var feeLedger = await db.WalletTransactions.AsNoTracking()
                .SingleAsync(row => row.CustomerId == withSession.Id && row.Type == "TournamentFee");
            var paidRegistration = await db.TournamentRegistrations.AsNoTracking()
                .SingleAsync(row => row.CustomerId == withSession.Id && row.TournamentId == paidId);
            Check("Session charge, payment and tournament debit commit atomically",
                charge.Amount == (decimal)(charge.EndTime - session.StartTime).TotalSeconds
                && session.TotalCost == charge.Amount && gamingLedger.Amount == -charge.Amount
                && gamingLedger.BalanceAfter == 5000 - charge.Amount && feeLedger.Amount == -1000
                && feeLedger.BalanceAfter == 4000 - charge.Amount && paidRegistration.FeeCharged == 1000
                && paidRegistration.ChargeTransactionId == feeLedger.TransactionId
                && await db.Payments.AnyAsync(row => row.SessionChargeId == charge.ChargeId
                    && row.WalletTransactionId == gamingLedger.TransactionId && row.Status == "Succeeded"));
            var failedSession = await db.GamingSessions.AsNoTracking().SingleAsync(row => row.CustomerId == insufficient.Id);
            Check("Insufficient balance rolls back flushed session charge, ledger and registration",
                failedSession.TotalCost == 0 && failedSession.LastChargedAt == null
                && (await db.Customers.FindAsync(insufficient.Id))!.WalletBalance == 1050
                && !await db.SessionCharges.AnyAsync(row => row.GamingSessionId == failedSession.GamingSessionId)
                && !await db.WalletTransactions.AnyAsync(row => row.CustomerId == insufficient.Id)
                && !await db.TournamentRegistrations.AnyAsync(row => row.CustomerId == insufficient.Id));
            Check("Missing session price does not alter wallet or create a registration",
                (await db.Customers.FindAsync(legacySession.Id))!.WalletBalance == 5000
                && !await db.WalletTransactions.AnyAsync(row => row.CustomerId == legacySession.Id)
                && !await db.TournamentRegistrations.AnyAsync(row => row.CustomerId == legacySession.Id));
        }

        async Task<(int Id, string Token)> NewCustomerAsync(string phone, decimal balance, bool session, decimal? price = 3600)
        {
            const string password = "Tournament-check-password-2026!";
            int id;
            await using (var scope = services.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var customer = new Customer { FullName = "Tournament wallet check", Phone = phone, Status = "Active", WalletBalance = balance };
                customer.PasswordHash = scope.ServiceProvider.GetRequiredService<ICustomerPasswordHasher>().HashPassword(customer, password);
                db.Customers.Add(customer);
                if (session)
                {
                    var room = new ComputerRoom { RoomName = $"Wallet check {phone}", Floor = 1 };
                    var type = new ComputerType { TypeName = $"Wallet check {phone}", PricePerHour = 7200 };
                    var computer = new Computer { ComputerName = $"Wallet check {phone}", Room = room, ComputerType = type, Status = "Occupied" };
                    db.GamingSessions.Add(new GamingSession
                    {
                        Customer = customer, Computer = computer, Status = "Active",
                        StartTime = DateTime.UtcNow.AddSeconds(-120), PricePerHourSnapshot = price
                    });
                }
                await db.SaveChangesAsync();
                id = customer.CustomerId;
            }
            var login = await ReadSuccessAsync<JsonElement>(await SendAsync(
                HttpMethod.Post, "/api/auth/customer/login", null, new { phone, password }));
            return (id, login.GetProperty("accessToken").GetString()!);
        }

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
            if (method == HttpMethod.Post && (path == "/api/admin/tournaments" || path.EndsWith("/transitions", StringComparison.Ordinal)))
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
