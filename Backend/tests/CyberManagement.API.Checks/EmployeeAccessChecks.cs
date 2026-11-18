using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.IdentityModel.Tokens.Jwt;
using CyberManagement.Application.DTOs.Employees;
using CyberManagement.Domain.Entities;
using CyberManagement.Infrastructure.Persistance;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

internal static class EmployeeAccessChecks
{
    public static async Task<int> RunAsync(
        IServiceProvider services, HttpClient client, IReadOnlyDictionary<string, string> staffTokens,
        string customerToken, AdminInvariantLockBarrier adminInvariantLockBarrier)
    {
        var checks = 0;
        var roles = staffTokens.Keys.ToArray();
        Dictionary<string, int> employeeIds;
        Dictionary<string, string> usernames;
        int waiterRoleId, adminRoleId;
        string adminVersion;
        await using (var scope = services.CreateAsyncScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var employees = await database.Employees.Where(row => roles.Contains(row.Role.RoleName))
                .Select(row => new { row.Role.RoleName, row.RoleId, row.EmployeeId, row.Username, row.Version }).ToListAsync();
            employeeIds = employees.ToDictionary(row => row.RoleName, row => row.EmployeeId);
            usernames = employees.ToDictionary(row => row.RoleName, row => row.Username);
            waiterRoleId = await database.Roles.Where(row => row.RoleName == "Waiter").Select(row => row.RoleId).SingleAsync();
            adminVersion = Convert.ToBase64String(employees.Single(row => row.RoleName == "Admin").Version);
            adminRoleId = employees.Single(row => row.RoleName == "Admin").RoleId;
        }
        var adminPath = $"/api/employees/{employeeIds["Admin"]}";
        var passwordPath = "/api/auth/change-password";
        var configuration = services.GetRequiredService<IConfiguration>();
        Check("Customer fixture ID overlaps an employee ID, but must not grant employee access",
            int.TryParse(new JwtSecurityTokenHandler().ReadJwtToken(customerToken).Subject, out var customerId)
            && employeeIds.Values.Contains(customerId));
        var validCreate = new { username = "forbidden-employee", fullName = "Forbidden employee", roleId = waiterRoleId, password = "Forbidden-create-2026!" };
        var validUpdate = new { fullName = "Forbidden profile", expectedVersion = adminVersion };
        var validPassword = new { currentPassword = "Unknown-current-2026!", newPassword = "Valid-new-password-2026!", confirmNewPassword = "Valid-new-password-2026!" };
        var beforeDenied = await SnapshotAsync();

        foreach (var (token, status, label) in new[]
        {
            ((string?)null, HttpStatusCode.Unauthorized, "Anonymous"),
            ((string?)customerToken, HttpStatusCode.Forbidden, "Customer")
        })
        {
            foreach (var path in new[] { "/api/employees", adminPath })
                await ExpectStatusAsync($"{label} cannot read employee profiles", HttpMethod.Get, path, token, status);
            await ExpectStatusAsync($"{label} cannot create employees", HttpMethod.Post, "/api/employees", token, status, validCreate);
            await ExpectStatusAsync($"{label} cannot update employees", HttpMethod.Put, adminPath, token, status, validUpdate);
            foreach (var suffix in new[] { "/lock", "/unlock", "/change-password" })
                await ExpectStatusAsync($"{label} cannot use employee actions", HttpMethod.Post, adminPath + suffix, token, status, validPassword);
            await ExpectStatusAsync($"{label} cannot change an employee password through auth", HttpMethod.Post, passwordPath, token, status, validPassword);
        }

        foreach (var (role, token) in staffTokens)
        {
            var readStatus = role is "Admin" or "Manager" ? HttpStatusCode.OK : HttpStatusCode.Forbidden;
            foreach (var path in new[] { "/api/employees", adminPath })
                await ExpectStatusAsync($"{role} employee read permission", HttpMethod.Get, path, token, readStatus);
            if (role != "Admin")
            {
                await ExpectStatusAsync($"{role} cannot create employees", HttpMethod.Post, "/api/employees", token, HttpStatusCode.Forbidden, validCreate);
                await ExpectStatusAsync($"{role} cannot update employees", HttpMethod.Put, adminPath, token, HttpStatusCode.Forbidden, validUpdate);
                foreach (var suffix in new[] { "/lock", "/unlock" })
                    await ExpectStatusAsync($"{role} cannot manage employee status", HttpMethod.Post, adminPath + suffix, token, HttpStatusCode.Forbidden);
            }
            var victimId = employeeIds[role == "Admin" ? "Waiter" : "Admin"];
            await ExpectStatusAsync($"{role} cannot change another employee password via the legacy route",
                HttpMethod.Post, $"/api/employees/{victimId}/change-password", token, HttpStatusCode.Forbidden, validPassword);
        }
        Check("Denied reads/writes do not alter any employee, password, version or audit", beforeDenied == await SnapshotAsync());

        foreach (var (role, token) in staffTokens)
        {
            var employeeId = employeeIds[role];
            var password = configuration[$"SeedAccounts:{role}:Password"]
                ?? throw new InvalidOperationException("Missing fixture password.");
            var newPassword = $"Self-change-{role}-2026!";
            var before = await SnapshotAsync();
            await ExpectStatusAsync($"{role} wrong current password returns 400, not 403",
                HttpMethod.Post, passwordPath, token, HttpStatusCode.BadRequest, validPassword);
            await ExpectStatusAsync($"{role} rejects mismatched confirmation on the auth route",
                HttpMethod.Post, passwordPath, token, HttpStatusCode.BadRequest,
                new { currentPassword = password, newPassword, confirmNewPassword = password });
            Check($"{role} rejected password change does not persist", before == await SnapshotAsync());
            var otherEmployeesBefore = await SnapshotAsync(excludeEmployeeId: employeeId);
            var auditCount = await AuditCountAsync();
            await ExpectStatusAsync($"{role} can change their own password without employee.view/manage",
                HttpMethod.Post, passwordPath, token, HttpStatusCode.NoContent,
                new { currentPassword = password, newPassword, confirmNewPassword = newPassword, employeeId = employeeIds[role == "Admin" ? "Waiter" : "Admin"] });
            Check($"{role} auth route ignores a supplied target ID and leaves all other employees untouched",
                otherEmployeesBefore == await SnapshotAsync(excludeEmployeeId: employeeId));
            await CheckPasswordAuditAsync(employeeId, auditCount + 1);
            await ExpectStatusAsync($"{role} old password is no longer accepted at login",
                HttpMethod.Post, "/api/auth/login", null, HttpStatusCode.Unauthorized,
                new { username = usernames[role], password });
            await ExpectStatusAsync($"{role} can restore their own password through the legacy self-only alias",
                HttpMethod.Post, $"/api/employees/{employeeId}/change-password", token, HttpStatusCode.NoContent,
                new { currentPassword = newPassword, newPassword = password, confirmNewPassword = password });
            await CheckPasswordAuditAsync(employeeId, auditCount + 2);
        }

        var managerId = employeeIds["Manager"];
        await SetManagerActiveAsync(false);
        try
        {
            var beforeInactive = await SnapshotAsync();
            await ExpectStatusAsync("Inactive Manager cannot read employee profiles with an old token",
                HttpMethod.Get, "/api/employees", staffTokens["Manager"], HttpStatusCode.Forbidden);
            await ExpectStatusAsync("Inactive Manager cannot verify a session with an old token",
                HttpMethod.Get, "/api/auth/me", staffTokens["Manager"], HttpStatusCode.Forbidden);
            await ExpectStatusAsync("Inactive Manager cannot change password on auth route",
                HttpMethod.Post, passwordPath, staffTokens["Manager"], HttpStatusCode.Forbidden, validPassword);
            await ExpectStatusAsync("Inactive Manager cannot change password on legacy alias",
                HttpMethod.Post, $"/api/employees/{managerId}/change-password", staffTokens["Manager"], HttpStatusCode.Forbidden, validPassword);
            Check("Inactive account requests do not persist changes", beforeInactive == await SnapshotAsync());
        }
        finally
        {
            await SetManagerActiveAsync(true);
        }
        await CheckLockedAdminTokenAsync();
        await CheckAdminInvariantConcurrencyAsync();
        return checks;

        async Task CheckLockedAdminTokenAsync()
        {
            const string username = "employee-locked-admin";
            const string password = "Locked-admin-check-2026!";
            int lockedAdminId;
            using (var request = new HttpRequestMessage(HttpMethod.Post, "/api/employees"))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", staffTokens["Admin"]);
                request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString("D"));
                request.Content = JsonContent.Create(new { username, fullName = "Locked admin fixture", password, roleId = adminRoleId });
                using var response = await client.SendAsync(request);
                Check("Active Admin can create a second Admin for lock checks", response.StatusCode == HttpStatusCode.Created);
                var employee = await response.Content.ReadFromJsonAsync<EmployeeDto>()
                    ?? throw new InvalidOperationException("Missing locked-admin fixture.");
                lockedAdminId = employee.EmployeeId;
            }
            string token;
            using (var response = await client.PostAsJsonAsync("/api/auth/login", new { username, password }))
            {
                Check("Second Admin receives a real JWT before being locked", response.StatusCode == HttpStatusCode.OK);
                var login = await response.Content.ReadFromJsonAsync<JsonElement>();
                token = login.GetProperty("accessToken").GetString()
                    ?? throw new InvalidOperationException("Missing locked-admin token.");
            }
            var lockedAdminPath = $"/api/employees/{lockedAdminId}";
            var otherEmployeePath = $"/api/employees/{employeeIds["Waiter"]}";
            string targetVersion;
            await using (var scope = services.CreateAsyncScope())
            {
                var database = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                targetVersion = Convert.ToBase64String(await database.Employees
                    .Where(row => row.EmployeeId == employeeIds["Waiter"]).Select(row => row.Version).SingleAsync());
            }
            await ExpectStatusAsync("Second Admin's JWT can read employee lists before lock",
                HttpMethod.Get, "/api/employees", token, HttpStatusCode.OK);
            await ExpectStatusAsync("Second Admin's JWT can read the role catalog before lock",
                HttpMethod.Get, "/api/admin/roles", token, HttpStatusCode.OK);
            await ExpectStatusAsync("Active Admin locks the second Admin through the API",
                HttpMethod.Post, lockedAdminPath + "/lock", staffTokens["Admin"], HttpStatusCode.OK);
            var beforeBlockedRequests = await SnapshotAsync();
            foreach (var path in new[] { "/api/employees", lockedAdminPath, otherEmployeePath, "/api/admin/roles" })
                await ExpectStatusAsync("Locked Admin's unexpired JWT cannot read management data",
                    HttpMethod.Get, path, token, HttpStatusCode.Forbidden);
            var passwordChange = new
            {
                currentPassword = password, newPassword = "Blocked-new-password-2026!",
                confirmNewPassword = "Blocked-new-password-2026!"
            };
            foreach (var (name, method, path, body) in new (string, HttpMethod, string, object?)[]
            {
                ("create employees", HttpMethod.Post, "/api/employees", validCreate),
                ("edit another employee", HttpMethod.Put, otherEmployeePath,
                    new { fullName = "Blocked admin edit", expectedVersion = targetVersion }),
                ("lock another employee", HttpMethod.Post, otherEmployeePath + "/lock", null),
                ("unlock themselves", HttpMethod.Post, lockedAdminPath + "/unlock", null),
                ("change password through auth", HttpMethod.Post, passwordPath, passwordChange),
                ("change password through legacy alias", HttpMethod.Post, lockedAdminPath + "/change-password", passwordChange)
            })
                await ExpectStatusAsync($"Locked Admin's old JWT cannot {name}", method, path, token, HttpStatusCode.Forbidden, body);
            await ExpectStatusAsync("Locked Admin cannot log in again",
                HttpMethod.Post, "/api/auth/login", null, HttpStatusCode.Forbidden, new { username, password });
            Check("Locked Admin's rejected requests leave all employees, passwords, versions and audit unchanged",
                beforeBlockedRequests == await SnapshotAsync());
            await ExpectStatusAsync("Another active Admin retains employee management access",
                HttpMethod.Get, "/api/employees", staffTokens["Admin"], HttpStatusCode.OK);
            await ExpectStatusAsync("Only the active Admin can unlock the locked account",
                HttpMethod.Post, lockedAdminPath + "/unlock", staffTokens["Admin"], HttpStatusCode.OK);
            await ExpectStatusAsync("The same old JWT reads current DB state after another Admin unlocks the account",
                HttpMethod.Get, "/api/employees", token, HttpStatusCode.OK);
            await ExpectStatusAsync("The same old JWT can read its profile again after unlock",
                HttpMethod.Get, lockedAdminPath, token, HttpStatusCode.OK);
            await ExpectStatusAsync("Leave the extra Admin inactive for subsequent last-active-Admin checks",
                HttpMethod.Post, lockedAdminPath + "/lock", staffTokens["Admin"], HttpStatusCode.OK);
        }

        async Task CheckAdminInvariantConcurrencyAsync()
        {
            const string firstUsername = "admin-invariant-first";
            const string secondUsername = "admin-invariant-second";
            const string password = "Admin-invariant-check-2026!";
            var pairIds = new List<int>();
            string firstToken;
            string secondToken;
            int managerRoleId;

            foreach (var username in new[] { firstUsername, secondUsername })
            {
                using var createRequest = new HttpRequestMessage(HttpMethod.Post, "/api/employees");
                createRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", staffTokens["Admin"]);
                createRequest.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString("D"));
                createRequest.Content = JsonContent.Create(new
                {
                    username,
                    fullName = username,
                    password,
                    roleId = adminRoleId
                });
                using var createResponse = await client.SendAsync(createRequest);
                Check($"Create isolated Admin concurrency fixture {username}", createResponse.StatusCode == HttpStatusCode.Created);
                var created = await createResponse.Content.ReadFromJsonAsync<EmployeeDto>()
                    ?? throw new InvalidOperationException("Missing Admin concurrency fixture.");
                pairIds.Add(created.EmployeeId);
            }

            firstToken = await LoginAsync(firstUsername);
            secondToken = await LoginAsync(secondUsername);
            await using (var scope = services.CreateAsyncScope())
            {
                var database = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                managerRoleId = await database.Roles.Where(row => row.RoleName == "Manager")
                    .Select(row => row.RoleId).SingleAsync();
                (await database.Employees.SingleAsync(row => row.EmployeeId == employeeIds["Admin"])).IsActive = false;
                await database.SaveChangesAsync();
            }

            try
            {
                if (!await IsRelationalAsync())
                {
                    Console.WriteLine("SKIP concurrent Admin invariant race test: requires SQL Server application locks.");
                    return;
                }

                var beforeLocks = await SnapshotAsync();
                adminInvariantLockBarrier.Arm(2);
                HttpResponseMessage[] lockResponses;
                try
                {
                    lockResponses = await Task.WhenAll(
                        SendWithTokenAsync(HttpMethod.Post, $"/api/employees/{pairIds[1]}/lock", firstToken),
                        SendWithTokenAsync(HttpMethod.Post, $"/api/employees/{pairIds[0]}/lock", secondToken));
                }
                finally
                {
                    adminInvariantLockBarrier.Disarm();
                }
                try
                {
                    Check("Concurrent mutual Admin locks cannot both succeed",
                        lockResponses.Count(response => response.StatusCode == HttpStatusCode.OK) == 1
                        && lockResponses.Count(response => response.StatusCode == HttpStatusCode.Conflict) == 1);
                }
                finally
                {
                    foreach (var response in lockResponses)
                        response.Dispose();
                }
                await CheckExactlyOneActiveAdminAsync("After concurrent mutual locks");
                Check("Mutual lock race writes only one lock audit",
                    await EmployeeAuditCountAsync() == await EmployeeAuditCountFromSnapshotAsync(beforeLocks) + 1);

                await SetPairActiveAsync(true);
                var beforeDemotions = await SnapshotAsync();
                var firstVersion = await VersionForAsync(pairIds[0]);
                var secondVersion = await VersionForAsync(pairIds[1]);
                adminInvariantLockBarrier.Arm(2);
                HttpResponseMessage[] demotionResponses;
                try
                {
                    demotionResponses = await Task.WhenAll(
                        SendWithTokenAsync(HttpMethod.Put, $"/api/employees/{pairIds[1]}",
                            firstToken, new { roleId = managerRoleId, expectedVersion = secondVersion }),
                        SendWithTokenAsync(HttpMethod.Put, $"/api/employees/{pairIds[0]}",
                            secondToken, new { roleId = managerRoleId, expectedVersion = firstVersion }));
                }
                finally
                {
                    adminInvariantLockBarrier.Disarm();
                }
                try
                {
                    Check("Concurrent mutual Admin demotions cannot both succeed",
                        demotionResponses.Count(response => response.StatusCode == HttpStatusCode.OK) == 1
                        && demotionResponses.Count(response => response.StatusCode == HttpStatusCode.Conflict) == 1);
                }
                finally
                {
                    foreach (var response in demotionResponses)
                        response.Dispose();
                }
                await CheckExactlyOneActiveAdminAsync("After concurrent mutual demotions");
                Check("Mutual demotion race writes only one additional audit",
                    await EmployeeAuditCountAsync() == await EmployeeAuditCountFromSnapshotAsync(beforeDemotions) + 1);
            }
            finally
            {
                adminInvariantLockBarrier.Disarm();
                await using var scope = services.CreateAsyncScope();
                var database = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var auditRows = await database.AuditLogs
                    .Where(row => row.EmployeeId.HasValue && pairIds.Contains(row.EmployeeId.Value)
                        || row.EntityType == "Employee" && pairIds.Contains(row.EntityId))
                    .ToListAsync();
                database.AuditLogs.RemoveRange(auditRows);
                var employees = await database.Employees.Where(row => pairIds.Contains(row.EmployeeId)
                    || row.EmployeeId == employeeIds["Admin"]).ToListAsync();
                foreach (var employee in employees)
                {
                    if (employee.EmployeeId == employeeIds["Admin"])
                        employee.IsActive = true;
                }
                database.Employees.RemoveRange(employees.Where(row => pairIds.Contains(row.EmployeeId)));
                await database.SaveChangesAsync();
            }

            async Task<string> LoginAsync(string username)
            {
                using var response = await client.PostAsJsonAsync("/api/auth/login", new { username, password });
                Check($"{username} obtains JWT before invariant race", response.StatusCode == HttpStatusCode.OK);
                var login = await response.Content.ReadFromJsonAsync<JsonElement>();
                return login.GetProperty("accessToken").GetString()
                    ?? throw new InvalidOperationException("Missing Admin concurrency token.");
            }

            async Task<HttpResponseMessage> SendWithTokenAsync(
                HttpMethod method, string path, string token, object? body = null)
            {
                using var request = new HttpRequestMessage(method, path);
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString("D"));
                if (body is not null)
                    request.Content = JsonContent.Create(body);
                return await client.SendAsync(request);
            }

            async Task<string> VersionForAsync(int employeeId)
            {
                await using var scope = services.CreateAsyncScope();
                var database = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                return Convert.ToBase64String(await database.Employees.Where(row => row.EmployeeId == employeeId)
                    .Select(row => row.Version).SingleAsync());
            }

            async Task SetPairActiveAsync(bool active)
            {
                await using var scope = services.CreateAsyncScope();
                var database = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var employees = await database.Employees.Where(row => pairIds.Contains(row.EmployeeId)).ToListAsync();
                foreach (var employee in employees)
                    employee.IsActive = active;
                await database.SaveChangesAsync();
            }

            async Task CheckExactlyOneActiveAdminAsync(string scenario)
            {
                await using var scope = services.CreateAsyncScope();
                var database = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var activeAdminCount = await database.Employees.CountAsync(row =>
                    row.IsActive && row.Role.RoleName == "Admin");
                Check($"{scenario} preserves exactly one active Admin", activeAdminCount == 1);
            }

            async Task<int> EmployeeAuditCountAsync()
            {
                await using var scope = services.CreateAsyncScope();
                return await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().AuditLogs
                    .CountAsync(row => row.EntityType == "Employee");
            }

            async Task<int> EmployeeAuditCountFromSnapshotAsync(string snapshot)
            {
                using var document = JsonDocument.Parse(snapshot);
                return document.RootElement.GetProperty("audits").GetArrayLength();
            }

            async Task<bool> IsRelationalAsync()
            {
                await using var scope = services.CreateAsyncScope();
                return scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Database.IsRelational();
            }
        }

        async Task ExpectStatusAsync(string name, HttpMethod method, string path, string? token, HttpStatusCode status, object? body = null)
        {
            using var request = new HttpRequestMessage(method, path);
            if (token is not null) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            var requiresIdempotency = method == HttpMethod.Put
                || (method == HttpMethod.Post && path.StartsWith("/api/employees", StringComparison.Ordinal))
                || path == "/api/auth/change-password";
            if (requiresIdempotency)
                request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString("D"));
            if (body is not null) request.Content = JsonContent.Create(body);
            using var response = await client.SendAsync(request);
            Check($"{name} ({(int)response.StatusCode})", response.StatusCode == status);
        }

        async Task<int> AuditCountAsync()
        {
            await using var scope = services.CreateAsyncScope();
            return await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().AuditLogs.CountAsync();
        }

        async Task CheckPasswordAuditAsync(int employeeId, int count)
        {
            await using var scope = services.CreateAsyncScope();
            var database = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var audit = await database.AuditLogs.AsNoTracking().OrderByDescending(row => row.LogId).FirstAsync();
            Check("Self password change records exactly one audit with actor equal to target",
                await database.AuditLogs.CountAsync() == count && audit.EmployeeId == employeeId
                && audit.EntityType == "Employee" && audit.EntityId == employeeId && audit.Action == "PasswordChanged");
        }

        async Task<string> SnapshotAsync(int? excludeEmployeeId = null)
        {
            await using var scope = services.CreateAsyncScope();
            var database = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var employees = await database.Employees.AsNoTracking()
                .Where(row => !excludeEmployeeId.HasValue || row.EmployeeId != excludeEmployeeId.Value)
                .OrderBy(row => row.EmployeeId)
                .Select(row => new { row.EmployeeId, row.Username, row.FullName, row.Phone, row.RoleId, row.IsActive, row.PasswordHash, row.Version })
                .ToListAsync();
            var audits = excludeEmployeeId.HasValue ? null : await database.AuditLogs.AsNoTracking().OrderBy(row => row.LogId).ToListAsync();
            return JsonSerializer.Serialize(new { employees, audits });
        }

        async Task SetManagerActiveAsync(bool active)
        {
            await using var scope = services.CreateAsyncScope();
            var database = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            (await database.Employees.SingleAsync(row => row.EmployeeId == managerId)).IsActive = active;
            await database.SaveChangesAsync();
        }

        void Check(string name, bool condition)
        {
            if (!condition) throw new InvalidOperationException(name);
            checks++;
            Console.WriteLine($"PASS {name}");
        }
    }
}
