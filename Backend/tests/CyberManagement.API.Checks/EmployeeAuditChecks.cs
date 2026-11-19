using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using CyberManagement.Application.DTOs.Employees;
using CyberManagement.Infrastructure.Persistance;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

internal static class EmployeeAuditChecks
{
    public static async Task<int> RunAsync(
        IServiceProvider services, HttpClient client, IReadOnlyDictionary<string, string> staffTokens,
        EmployeeAuditSaveFailure failure, EmployeeMutationReadBarrier mutationReadBarrier)
    {
        var checks = 0;
        string? passwordToken = null;
        var startedAt = DateTime.UtcNow;
        int actorId, waiterRoleId, managerRoleId, initialAuditCount;
        bool relational, sqlServer;
        await using (var scope = services.CreateAsyncScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            actorId = await database.Employees.Where(row => row.Username == "admin").Select(row => row.EmployeeId).SingleAsync();
            waiterRoleId = await database.Roles.Where(row => row.RoleName == "Waiter").Select(row => row.RoleId).SingleAsync();
            managerRoleId = await database.Roles.Where(row => row.RoleName == "Manager").Select(row => row.RoleId).SingleAsync();
            initialAuditCount = await database.AuditLogs.CountAsync();
            relational = database.Database.IsRelational();
            sqlServer = database.Database.IsSqlServer();
        }

        const string originalPassword = "Employee-audit-original-2026!";
        const string changedPassword = "Employee-audit-changed-2026!";
        var create = new { username = "employee-audit-target", fullName = "Audit target", phone = "0900000199", roleId = waiterRoleId, password = originalPassword };
        var createKey = Guid.NewGuid().ToString("D");
        int targetId;
        string createdVersion;
        using (var response = await SendAsync(HttpMethod.Post, "/api/employees", create, idempotencyKey: createKey))
        {
            Check("Employee creation succeeds", response.StatusCode == HttpStatusCode.Created);
            var employee = await response.Content.ReadFromJsonAsync<EmployeeDto>()
                ?? throw new InvalidOperationException("Missing created employee.");
            targetId = employee.EmployeeId;
            createdVersion = employee.Version;
        }
        using (var response = await SendAsync(HttpMethod.Post, "/api/employees", create, idempotencyKey: createKey))
        {
            Check("Repeated employee create replays with 200 and replay marker",
                response.StatusCode == HttpStatusCode.OK
                && response.Headers.TryGetValues("Idempotency-Replayed", out var replayHeader)
                && replayHeader.Single() == "true");
            var replay = await response.Content.ReadFromJsonAsync<EmployeeDto>()
                ?? throw new InvalidOperationException("Missing replayed employee.");
            Check("Employee create replay returns the original ID and version",
                replay.EmployeeId == targetId && replay.Version == createdVersion);
        }
        using (var response = await SendAsync(HttpMethod.Post, "/api/employees",
            new
            {
                username = create.username,
                fullName = "Different payload",
                phone = create.phone,
                roleId = create.roleId,
                password = originalPassword
            }, idempotencyKey: createKey))
        {
            Check("Reusing an employee create key with another payload returns 409",
                response.StatusCode == HttpStatusCode.Conflict);
        }
        var targetPath = $"/api/employees/{targetId}";
        using (var loginResponse = await SendAsync(HttpMethod.Post, "/api/auth/login",
            new { username = create.username, password = originalPassword }))
        {
            loginResponse.EnsureSuccessStatusCode();
            var login = await loginResponse.Content.ReadFromJsonAsync<JsonElement>();
            passwordToken = login.GetProperty("accessToken").GetString();
        }
        await ExpectAuditAsync("Created");
        var initialVersion = (await ReadEmployeeAsync()).Version;
        Check("Employee list exposes the current profile version",
            (await ReadEmployeesAsync()).Single(row => row.EmployeeId == targetId).Version == initialVersion);
        Check("Employee version is an 8-byte Base64 token", Convert.FromBase64String(initialVersion).Length == 8);
        var updateKey = $"employee-update:{targetId}";
        var updateBody = new { fullName = "Updated audit target", phone = "0900000188", expectedVersion = initialVersion };
        await ExpectSuccessAsync(HttpMethod.Put, targetPath, updateBody, idempotencyKey: updateKey);
        await ExpectAuditAsync("Updated");
        var afterFirstUpdate = await ReadEmployeeAsync();
        using (var replay = await SendAsync(HttpMethod.Put, targetPath, updateBody, idempotencyKey: updateKey))
        {
            var replayed = await replay.Content.ReadFromJsonAsync<EmployeeDto>();
            Check("Employee update replay returns the original response before stale-version validation",
                replay.StatusCode == HttpStatusCode.OK
                && replay.Headers.TryGetValues("Idempotency-Replayed", out var values)
                && values.Single() == "true"
                && replayed == afterFirstUpdate);
        }
        using (var conflict = await SendAsync(HttpMethod.Put, targetPath,
            new { fullName = "Different idempotent payload", phone = "0900000188", expectedVersion = initialVersion },
            idempotencyKey: updateKey))
            Check("Employee update key cannot be reused with another payload", conflict.StatusCode == HttpStatusCode.Conflict);
        await ExpectSuccessAsync(HttpMethod.Put, targetPath, new { roleId = managerRoleId, expectedVersion = afterFirstUpdate.Version });
        await ExpectAuditAsync("Updated");
        var lockKey = Guid.NewGuid().ToString("D");
        await ExpectSuccessAsync(HttpMethod.Post, targetPath + "/lock", idempotencyKey: lockKey);
        await ExpectAuditAsync("Locked");
        using (var replay = await SendAsync(HttpMethod.Post, targetPath + "/lock", idempotencyKey: lockKey))
        {
            var replayed = await replay.Content.ReadFromJsonAsync<EmployeeDto>();
            Check("Employee lock replay returns the original inactive response",
                replay.StatusCode == HttpStatusCode.OK
                && replay.Headers.TryGetValues("Idempotency-Replayed", out var values)
                && values.Single() == "true"
                && replayed?.IsActive == false);
        }
        var unlockKey = Guid.NewGuid().ToString("D");
        await ExpectSuccessAsync(HttpMethod.Post, targetPath + "/unlock", idempotencyKey: unlockKey);
        await ExpectAuditAsync("Unlocked");
        using (var replay = await SendAsync(HttpMethod.Post, targetPath + "/unlock", idempotencyKey: unlockKey))
            Check("Employee unlock replay returns the original active response",
                replay.StatusCode == HttpStatusCode.OK
                && replay.Headers.TryGetValues("Idempotency-Replayed", out var values)
                && values.Single() == "true"
                && (await replay.Content.ReadFromJsonAsync<EmployeeDto>())?.IsActive == true);
        var passwordKey = Guid.NewGuid().ToString("D");
        var passwordBody = new { currentPassword = originalPassword, newPassword = changedPassword, confirmNewPassword = changedPassword };
        await ExpectSuccessAsync(HttpMethod.Post, targetPath + "/change-password",
            passwordBody, HttpStatusCode.NoContent, passwordKey);
        await ExpectAuditAsync("PasswordChanged");
        using (var replay = await SendAsync(HttpMethod.Post, "/api/auth/change-password",
            passwordBody, idempotencyKey: passwordKey))
            Check("Password change replay returns 204 before verifying the old password",
                replay.StatusCode == HttpStatusCode.NoContent
                && replay.Headers.TryGetValues("Idempotency-Replayed", out var values)
                && values.Single() == "true");

        var beforeRejections = await SnapshotAsync();
        using (var response = await SendAsync(HttpMethod.Post, targetPath + "/change-password",
            new { currentPassword = originalPassword, newPassword = "Should-not-be-saved-2026!", confirmNewPassword = "Should-not-be-saved-2026!" }))
            Check("Wrong current password is rejected", response.StatusCode == HttpStatusCode.BadRequest);
        using (var response = await SendAsync(HttpMethod.Post, "/api/employees", create))
            Check("Duplicate employee creation is rejected", response.StatusCode == HttpStatusCode.BadRequest);
        foreach (var invalidCreate in new object[]
        {
            new { username = "   ", fullName = "Valid name", roleId = waiterRoleId, password = originalPassword },
            new { username = "valid-name", fullName = "   ", roleId = waiterRoleId, password = originalPassword },
            new { username = new string('u', 51), fullName = "Valid name", roleId = waiterRoleId, password = originalPassword },
            new { username = "long-name", fullName = new string('n', 151), roleId = waiterRoleId, password = originalPassword }
        })
        {
            using var response = await SendAsync(HttpMethod.Post, "/api/employees", invalidCreate);
            Check("Invalid employee username/name is rejected with 400", response.StatusCode == HttpStatusCode.BadRequest);
        }
        using (var response = await SendAsync(HttpMethod.Put, targetPath, new { fullName = "Forbidden change" }, staffTokens["Waiter"]))
            Check("Unauthorized employee mutation is rejected", response.StatusCode == HttpStatusCode.Forbidden);
        Check("Rejected requests leave employees and audit unchanged", beforeRejections == await SnapshotAsync());

        if (sqlServer)
        {
            var sameKey = Guid.NewGuid().ToString("D");
            var sameUpdate = new { fullName = "Identical concurrent edit", expectedVersion = (await ReadEmployeeAsync()).Version };
            mutationReadBarrier.Arm(sameKey);
            HttpResponseMessage[] sameKeyResponses;
            try
            {
                sameKeyResponses = await Task.WhenAll(
                    SendAsync(HttpMethod.Put, targetPath, sameUpdate, idempotencyKey: sameKey),
                    SendAsync(HttpMethod.Put, targetPath, sameUpdate, idempotencyKey: sameKey));
            }
            finally
            {
                mutationReadBarrier.Disarm();
            }
            try
            {
                Check("Identical PUTs replay even when both receipts miss and the second profile read follows commit",
                    mutationReadBarrier.ProfileReadAfterCommitObserved
                    && sameKeyResponses.All(response => response.StatusCode == HttpStatusCode.OK));
                Check("Exactly one concurrent PUT is marked as a replay",
                    sameKeyResponses.Count(response => response.Headers.TryGetValues("Idempotency-Replayed", out var values)
                        && values.Single() == "true") == 1);
                var results = await Task.WhenAll(sameKeyResponses.Select(async response =>
                    await response.Content.ReadFromJsonAsync<EmployeeDto>()));
                Check("Concurrent PUT replay returns the original DTO and persists only that version",
                    results[0] == results[1] && results[0] == await ReadEmployeeAsync());
                await ExpectAuditAsync("Updated");
            }
            finally
            {
                foreach (var response in sameKeyResponses) response.Dispose();
            }

            var concurrentCreate = new
            {
                username = "employee-audit-idempotent-race",
                fullName = "Idempotent race target",
                roleId = waiterRoleId,
                password = originalPassword
            };
            var concurrentKey = Guid.NewGuid().ToString("D");
            var concurrentResponses = await Task.WhenAll(
                SendAsync(HttpMethod.Post, "/api/employees", concurrentCreate, idempotencyKey: concurrentKey),
                SendAsync(HttpMethod.Post, "/api/employees", concurrentCreate, idempotencyKey: concurrentKey));
            try
            {
                Check("Concurrent identical employee creates return one 201 and one 200",
                    concurrentResponses.Count(response => response.StatusCode == HttpStatusCode.Created) == 1
                    && concurrentResponses.Count(response => response.StatusCode == HttpStatusCode.OK) == 1);
                var ids = await Task.WhenAll(concurrentResponses.Select(async response =>
                    (await response.Content.ReadFromJsonAsync<EmployeeDto>()
                        ?? throw new InvalidOperationException("Missing concurrent employee response.")).EmployeeId));
                Check("Concurrent employee create replay returns the same employee ID", ids[0] == ids[1]);
                await ExpectAuditAsync("Created", ids[0]);
            }
            finally
            {
                foreach (var response in concurrentResponses) response.Dispose();
            }
        }

        beforeRejections = await SnapshotAsync();
        foreach (var invalid in new object[]
        {
            new { fullName = "Missing version" },
            new { expectedVersion = "" },
            new { expectedVersion = "not-base64" },
            new { expectedVersion = Convert.ToBase64String(new byte[7]) },
            new { expectedVersion = Convert.ToBase64String(new byte[9]) }
        })
        {
            using var response = await SendAsync(HttpMethod.Put, targetPath, invalid);
            Check("Missing or malformed expectedVersion is rejected with 400", response.StatusCode == HttpStatusCode.BadRequest);
        }
        Check("Invalid versions do not change employee or audit", beforeRejections == await SnapshotAsync());

        foreach (var weak in new string?[] { null, "", "a", new string('a', 10) + "1", new string('a', 72) + "1", new string('a', 12), new string('1', 12), new string('!', 12) })
        {
            using (var response = await SendAsync(HttpMethod.Post, "/api/employees",
                new { username = "weak-password-target", fullName = "Weak password target", roleId = waiterRoleId, password = weak }))
                Check("Create rejects missing, short, long or missing-letter/digit passwords", response.StatusCode == HttpStatusCode.BadRequest);
            using (var response = await SendAsync(HttpMethod.Post, targetPath + "/change-password",
                new { currentPassword = changedPassword, newPassword = weak, confirmNewPassword = weak }))
                Check("Password change rejects missing, short, long or missing-letter/digit passwords", response.StatusCode == HttpStatusCode.BadRequest);
        }
        foreach (var invalidChange in new object[]
        {
            new { currentPassword = changedPassword, newPassword = changedPassword, confirmNewPassword = changedPassword },
            new { currentPassword = changedPassword, newPassword = originalPassword },
            new { currentPassword = changedPassword, newPassword = originalPassword, confirmNewPassword = "" },
            new { currentPassword = changedPassword, newPassword = originalPassword, confirmNewPassword = "Does-not-match-2026!" },
            new { currentPassword = changedPassword, newPassword = originalPassword, confirmNewPassword = originalPassword.ToUpperInvariant() },
            new { currentPassword = "", newPassword = originalPassword, confirmNewPassword = originalPassword }
        })
        {
            using var response = await SendAsync(HttpMethod.Post, targetPath + "/change-password", invalidChange);
            Check("Password change rejects reuse, missing current password or mismatched confirmation", response.StatusCode == HttpStatusCode.BadRequest);
        }
        Check("Invalid password requests leave all employees, hashes, versions and audits unchanged", beforeRejections == await SnapshotAsync());

        var currentPassword = changedPassword;
        foreach (var boundaryPassword in new[] { new string('a', 11) + "1", new string('b', 71) + "2" })
        {
            using (var response = await SendAsync(HttpMethod.Post, "/api/employees",
                new { username = $"boundary-password-{boundaryPassword.Length}", fullName = "Boundary password target", roleId = waiterRoleId, password = boundaryPassword }))
            {
                Check($"Create accepts a valid {boundaryPassword.Length}-character password", response.StatusCode == HttpStatusCode.Created);
                var created = await response.Content.ReadFromJsonAsync<EmployeeDto>()
                    ?? throw new InvalidOperationException("Missing boundary employee.");
                await ExpectAuditAsync("Created", created.EmployeeId);
            }
            await ExpectSuccessAsync(HttpMethod.Post, targetPath + "/change-password",
                new { currentPassword, newPassword = boundaryPassword, confirmNewPassword = boundaryPassword }, HttpStatusCode.NoContent);
            await ExpectAuditAsync("PasswordChanged");
            currentPassword = boundaryPassword;
        }
        using (var response = await SendAsync(HttpMethod.Post, "/api/auth/login",
            new { username = create.username, password = originalPassword }))
            Check("Previous password no longer authenticates after a valid password change", response.StatusCode == HttpStatusCode.Unauthorized);
        using (var response = await SendAsync(HttpMethod.Post, "/api/auth/login",
            new { username = create.username, password = currentPassword }))
            Check("The full 72-character password authenticates after a valid change", response.StatusCode == HttpStatusCode.OK);
        await ExpectSuccessAsync(HttpMethod.Post, targetPath + "/change-password",
            new { currentPassword, newPassword = changedPassword, confirmNewPassword = changedPassword }, HttpStatusCode.NoContent);
        await ExpectAuditAsync("PasswordChanged");

        if (relational)
        {
            Check("SQL rowversion changes after an update", initialVersion != afterFirstUpdate.Version);
            var beforeStale = await SnapshotAsync();
            using (var response = await SendAsync(HttpMethod.Put, targetPath,
                new { fullName = "Stale overwrite", expectedVersion = initialVersion }))
            {
                Check("A stale profile version returns 409", response.StatusCode == HttpStatusCode.Conflict);
                Check("Version conflict uses a clear ProblemDetails response",
                    response.Content.Headers.ContentType?.MediaType == "application/problem+json"
                    && (await response.Content.ReadAsStringAsync()).Contains("VersionConflict"));
            }
            Check("Stale PUT preserves profile, role, password, version and audit", beforeStale == await SnapshotAsync());
            var versionBeforeRace = (await ReadEmployeeAsync()).Version;
            var writes = await Task.WhenAll(new[] { "Concurrent first", "Concurrent second" }.Select(fullName =>
                SendAsync(HttpMethod.Put, targetPath, new { fullName, expectedVersion = versionBeforeRace })));
            try
            {
                Check("Two PUTs with the same version yield exactly one 200 and one 409",
                    writes.Count(response => response.StatusCode == HttpStatusCode.OK) == 1
                    && writes.Count(response => response.StatusCode == HttpStatusCode.Conflict) == 1);
                var winner = await writes.Single(response => response.StatusCode == HttpStatusCode.OK).Content.ReadFromJsonAsync<EmployeeDto>()
                    ?? throw new InvalidOperationException("Missing successful update.");
                var persisted = await ReadEmployeeAsync();
                Check("The successful edit persists and returns its new version", persisted == winner && winner.Version != versionBeforeRace);
            }
            finally
            {
                foreach (var response in writes) response.Dispose();
            }
            await ExpectAuditAsync("Updated");
            var latestVersion = (await ReadEmployeeAsync()).Version;
            await ExpectSuccessAsync(HttpMethod.Put, targetPath, new { fullName = "Reloaded edit", expectedVersion = latestVersion });
            await ExpectAuditAsync("Updated");
            await ExpectAtomicFailureAsync("Create", HttpMethod.Post, "/api/employees",
                new { username = "employee-audit-rollback", fullName = "Rollback target", roleId = waiterRoleId, password = originalPassword });
            await ExpectAtomicFailureAsync("Update", HttpMethod.Put, targetPath,
                new { fullName = "Must roll back", roleId = waiterRoleId, expectedVersion = (await ReadEmployeeAsync()).Version });
            await ExpectAtomicFailureAsync("Lock", HttpMethod.Post, targetPath + "/lock");
            await ExpectAtomicFailureAsync("Password", HttpMethod.Post, targetPath + "/change-password",
                new { currentPassword = changedPassword, newPassword = originalPassword, confirmNewPassword = originalPassword });
            await ExpectSuccessAsync(HttpMethod.Post, targetPath + "/lock");
            await ExpectAuditAsync("Locked");
            await ExpectAtomicFailureAsync("Unlock", HttpMethod.Post, targetPath + "/unlock");
            await ExpectSuccessAsync(HttpMethod.Post, targetPath + "/unlock");
            await ExpectAuditAsync("Unlocked");
        }
        else
        {
            Console.WriteLine("SKIP employee rowversion races and audit transaction rollback checks: requires SQL Server.");
        }

        await using (var scope = services.CreateAsyncScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var logs = await database.AuditLogs.AsNoTracking().Where(row => row.EntityType == "Employee" && row.EntityId == targetId).ToListAsync();
            var hash = await database.Employees.Where(row => row.EmployeeId == targetId).Select(row => row.PasswordHash).SingleAsync();
            var serialized = JsonSerializer.Serialize(logs);
            Check("Employee audits contain metadata only, never passwords or hashes",
                !serialized.Contains(originalPassword) && !serialized.Contains(changedPassword) && !serialized.Contains(hash)
                && logs.All(row => row.Action is "Created" or "Updated" or "Locked" or "Unlocked" or "PasswordChanged"));
        }
        return checks;

        async Task<HttpResponseMessage> SendAsync(
            HttpMethod method, string path, object? body = null, string? token = null, string? idempotencyKey = null)
        {
            using var request = new HttpRequestMessage(method, path);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer",
                token ?? (path.EndsWith("/change-password", StringComparison.Ordinal) ? passwordToken : staffTokens["Admin"]));
            var requiresIdempotency = method == HttpMethod.Put
                || (method == HttpMethod.Post && path.StartsWith("/api/employees", StringComparison.Ordinal))
                || path == "/api/auth/change-password";
            if (requiresIdempotency)
                request.Headers.Add("Idempotency-Key", idempotencyKey ?? Guid.NewGuid().ToString("D"));
            if (body is not null) request.Content = JsonContent.Create(body);
            return await client.SendAsync(request);
        }

        async Task<EmployeeDto> ReadEmployeeAsync()
        {
            using var response = await SendAsync(HttpMethod.Get, targetPath);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<EmployeeDto>()
                ?? throw new InvalidOperationException("Missing employee profile.");
        }

        async Task<EmployeeDto[]> ReadEmployeesAsync()
        {
            using var response = await SendAsync(HttpMethod.Get, "/api/employees");
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<EmployeeDto[]>()
                ?? throw new InvalidOperationException("Missing employee list.");
        }

        async Task ExpectSuccessAsync(
            HttpMethod method,
            string path,
            object? body = null,
            HttpStatusCode expected = HttpStatusCode.OK,
            string? idempotencyKey = null)
        {
            using var response = await SendAsync(method, path, body, idempotencyKey: idempotencyKey);
            Check($"Employee {method} {path} succeeds", response.StatusCode == expected);
        }

        async Task ExpectAuditAsync(string action, int? entityId = null)
        {
            await using var scope = services.CreateAsyncScope();
            var database = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var logs = await database.AuditLogs.AsNoTracking().OrderBy(row => row.LogId).ToListAsync();
            Check($"{action} persists exactly one employee audit", logs.Count == ++initialAuditCount);
            var audit = logs[^1];
            var expectedActorId = action == "PasswordChanged" ? targetId : actorId;
            Check($"{action} audit has authenticated actor, SQL target ID, action and UTC timestamp",
                audit.EmployeeId == expectedActorId && audit.EntityType == "Employee" && audit.EntityId == (entityId ?? targetId)
                && audit.Action == action
                && audit.CreatedAt >= startedAt && audit.CreatedAt <= DateTime.UtcNow);
        }

        async Task<string> SnapshotAsync()
        {
            await using var scope = services.CreateAsyncScope();
            var database = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var employees = await database.Employees.AsNoTracking().OrderBy(row => row.EmployeeId)
                .Select(row => new { row.EmployeeId, row.Username, row.FullName, row.Phone, row.RoleId, row.IsActive, row.PasswordHash, row.CreatedAt, row.Version })
                .ToListAsync();
            var audits = await database.AuditLogs.AsNoTracking().OrderBy(row => row.LogId).ToListAsync();
            return JsonSerializer.Serialize(new { employees, audits });
        }

        async Task ExpectAtomicFailureAsync(string action, HttpMethod method, string path, object? body = null)
        {
            var before = await SnapshotAsync();
            failure.FailNextAuditSave = 1;
            using var response = await SendAsync(method, path, body);
            Check($"{action} returns failure when audit cannot be saved",
                response.StatusCode == HttpStatusCode.BadRequest && failure.FailNextAuditSave == 0);
            Check($"{action} rolls back employee and audit together on SQL", before == await SnapshotAsync());
        }

        void Check(string name, bool condition)
        {
            if (!condition) throw new InvalidOperationException(name);
            checks++;
            Console.WriteLine($"PASS {name}");
        }
    }
}
