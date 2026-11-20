using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using CyberManagement.Application.DTOs.Computers;
using CyberManagement.Application.DTOs.Realtime;
using CyberManagement.Application.Interfaces.Services;
using CyberManagement.Infrastructure.Persistance;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

internal static class ComputerManagementChecks
{
    public static async Task<int> RunAsync(WebApplication app, HttpClient client,
        IReadOnlyDictionary<string, string> tokens, string customerToken, RealtimeSaveFailure saveFailure)
    {
        var checks = 0;
        var admin = tokens["Admin"];
        var adminId = int.Parse(new JwtSecurityTokenHandler().ReadJwtToken(admin).Subject);
        var waiterId = int.Parse(new JwtSecurityTokenHandler().ReadJwtToken(tokens["Waiter"]).Subject);
        var technicianId = int.Parse(new JwtSecurityTokenHandler().ReadJwtToken(tokens["Technician"]).Subject);
        var create = new { computerName = "CRUD-PC", roomId = 1, computerTypeId = 1 };
        foreach (var (name, token, status) in new (string, string?, HttpStatusCode)[]
        {
            ("Anonymous", null, HttpStatusCode.Unauthorized), ("Customer", customerToken, HttpStatusCode.Forbidden),
            ("Cashier", tokens["Cashier"], HttpStatusCode.Forbidden), ("Waiter", tokens["Waiter"], HttpStatusCode.Forbidden),
            ("Technician", tokens["Technician"], HttpStatusCode.Forbidden)
        })
        {
            foreach (var (method, endpoint, body) in new (HttpMethod, string, object?)[]
            {
                (HttpMethod.Post, "/api/admin/computers", create), (HttpMethod.Put, "/api/admin/computers/1", create),
                (HttpMethod.Delete, "/api/admin/computers/1", null),
                (HttpMethod.Post, "/api/admin/computer-types", new { typeName = "Denied", pricePerHour = 10000 }),
                (HttpMethod.Put, "/api/admin/computer-types/1", new { typeName = "Denied", pricePerHour = 10000 }),
                (HttpMethod.Delete, "/api/admin/computer-types/1", null)
            })
                await Expect($"{name} cannot {method} {endpoint}", status, await Send(method, endpoint, token, body));
        }
        foreach (var token in new[] { customerToken, tokens["Cashier"], tokens["Waiter"] })
        {
            await Expect("Maintenance history requires maintenance.view", HttpStatusCode.Forbidden,
                await Send(HttpMethod.Get, "/api/admin/computers/1/maintenances", token));
            foreach (var (method, suffix, body) in new (HttpMethod, string, object?)[]
            {
                (HttpMethod.Post, "", new { description = "Denied" }),
                (HttpMethod.Put, "/1", new { description = "Denied" }), (HttpMethod.Post, "/1/complete", null)
            })
                await Expect("Maintenance mutation requires maintenance.manage", HttpStatusCode.Forbidden,
                    await Send(method, "/api/admin/computers/1/maintenances" + suffix, token, body));
        }

        foreach (var body in new object[]
        {
            new { typeName = " ", pricePerHour = 10000m }, new { typeName = "Invalid", pricePerHour = 0m },
            new { typeName = "Invalid", pricePerHour = -1m }, new { typeName = "Invalid", pricePerHour = 1.5m },
            new { typeName = "Invalid", pricePerHour = 1000000000000000000m },
            new { typeName = "Invalid", pricePerHour = 10000m, ram = new string('x', 51) }
        })
            await Expect("Invalid computer type is rejected", HttpStatusCode.BadRequest,
                await Send(HttpMethod.Post, "/api/admin/computer-types", admin, body));

        await using (var peer = await RealtimeTestPeer.ConnectAsync(app.GetTestServer(), tokens["Waiter"]))
        {
            var response = Send(HttpMethod.Post, "/api/admin/computer-types", admin,
                new { typeName = " CRUD Type ", pricePerHour = 12345, cpu = " CPU " });
            var message = await peer.NextAsync();
            var id = EventId(message, RealtimeEvent.ComputerTypesChanged);
            await using var scope = app.Services.CreateAsyncScope();
            Check("Type invalidation is visible only after persistence in a fresh DbContext",
                await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().ComputerTypes
                    .AnyAsync(row => row.ComputerTypeId == id && row.TypeName == "CRUD Type" && row.CPU == "CPU"));
            await Expect("Create type", HttpStatusCode.Created, await response);
        }
        var types = await Read<ComputerTypeDto[]>("/api/admin/computer-types");
        var type = types.Single(row => row.TypeName == "CRUD Type");
        var rooms = await Read<ComputerRoomDto[]>("/api/admin/computer-rooms");
        var room = rooms[0];
        await Expect("Update type with nullable specifications", HttpStatusCode.OK,
            await Send(HttpMethod.Put, $"/api/admin/computer-types/{type.ComputerTypeId}", tokens["Manager"],
                new { typeName = "CRUD Type", pricePerHour = 20000, cpu = (string?)null }));
        foreach (var body in new object[]
        {
            new { computerName = " ", roomId = room.RoomId, computerTypeId = type.ComputerTypeId },
            new { computerName = new string('x', 101), roomId = room.RoomId, computerTypeId = type.ComputerTypeId },
            new { computerName = "Bad", roomId = 0, computerTypeId = type.ComputerTypeId }
        })
            await Expect("Invalid computer is rejected", HttpStatusCode.BadRequest,
                await Send(HttpMethod.Post, "/api/admin/computers", admin, body));
        await Expect("Unknown room does not create a computer", HttpStatusCode.NotFound,
            await Send(HttpMethod.Post, "/api/admin/computers", admin,
                new { computerName = "Bad", roomId = int.MaxValue, computerTypeId = type.ComputerTypeId }));
        await Expect("Unknown type does not create a computer", HttpStatusCode.NotFound,
            await Send(HttpMethod.Post, "/api/admin/computers", admin,
                new { computerName = "Bad", roomId = room.RoomId, computerTypeId = int.MaxValue }));
        var computer = await CreateComputer(" CRUD-PC ");
        Check("Create trims the name and ignores injected state/session flags", computer.ComputerName == "CRUD-PC"
            && computer.Status == "Available" && !computer.HasOpenSession && !computer.HasOpenMaintenance);
        await Expect("Duplicate trimmed computer name conflicts", HttpStatusCode.Conflict,
            await Send(HttpMethod.Post, "/api/admin/computers", admin,
                new { computerName = " CRUD-PC ", roomId = room.RoomId, computerTypeId = type.ComputerTypeId }));
        await Expect("Used type cannot be deleted", HttpStatusCode.Conflict,
            await Send(HttpMethod.Delete, $"/api/admin/computer-types/{type.ComputerTypeId}", admin));
        await Expect("Computer metadata can be updated without changing status", HttpStatusCode.OK,
            await Send(HttpMethod.Put, $"/api/admin/computers/{computer.ComputerId}", admin,
                new { computerName = "CRUD-PC-renamed", roomId = room.RoomId, computerTypeId = type.ComputerTypeId, status = "Occupied" }));

        var existing = await Read<ComputerDto[]>("/api/admin/computers");
        foreach (var machine in existing.Where(row => row.HasOpenSession || row.HasOpenMaintenance || row.Status == "LegacyOffline"))
        {
            await Expect("Unsafe computer metadata cannot change", HttpStatusCode.Conflict,
                await Send(HttpMethod.Put, $"/api/admin/computers/{machine.ComputerId}", admin,
                    new { computerName = "Overridden", roomId = room.RoomId, computerTypeId = type.ComputerTypeId }));
            await Expect("Unsafe computer cannot enter maintenance", HttpStatusCode.Conflict,
                await Send(HttpMethod.Post, $"/api/admin/computers/{machine.ComputerId}/maintenances", admin, new { description = "Unsafe" }));
        }
        foreach (var name in new[] { "PC-02", "PC-07" })
            await Expect("Historical computer cannot be deleted", HttpStatusCode.Conflict,
                await Send(HttpMethod.Delete, $"/api/admin/computers/{existing.Single(row => row.ComputerName == name).ComputerId}", admin));

        var path = $"/api/admin/computers/{computer.ComputerId}/maintenances";
        await using (var peer = await RealtimeTestPeer.ConnectAsync(app.GetTestServer(), tokens["Waiter"]))
        {
            Interlocked.Exchange(ref saveFailure.FailNextSave, 1);
            await Expect("Injected maintenance save failure", HttpStatusCode.InternalServerError,
                await Send(HttpMethod.Post, path, admin, new { description = "Must not persist" }));
            await using var scope = app.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            Check("Failed maintenance saves neither state nor history",
                await db.Computers.AsNoTracking().AnyAsync(row => row.ComputerId == computer.ComputerId && row.Status == "Available")
                && !await db.ComputerMaintenances.AnyAsync(row => row.ComputerId == computer.ComputerId));
            await Barrier(peer, waiterId, "Failed maintenance emits no computer event");
        }

        ComputerMaintenanceDto maintenance;
        await using (var viewer = await RealtimeTestPeer.ConnectAsync(app.GetTestServer(), tokens["Waiter"]))
        await using (var technician = await RealtimeTestPeer.ConnectAsync(app.GetTestServer(), tokens["Technician"]))
        {
            var response = Send(HttpMethod.Post, path, tokens["Technician"],
                new { description = " Clean fans ", employeeId = adminId, status = "Completed", endTime = DateTime.UtcNow });
            Check("Maintenance invalidates computer viewers", EventId(await viewer.NextAsync(), RealtimeEvent.ComputerChanged) == computer.ComputerId);
            Check("Technician receives machine and maintenance invalidations",
                EventId(await technician.NextAsync(), RealtimeEvent.ComputerChanged) == computer.ComputerId
                && EventId(await technician.NextAsync(), RealtimeEvent.MaintenanceChanged) == computer.ComputerId);
            using var result = await response;
            Check("Technician starts maintenance", result.StatusCode == HttpStatusCode.Created);
            maintenance = (await result.Content.ReadFromJsonAsync<ComputerMaintenanceDto>())!;
            Check("Maintenance actor/time/status come from the server", maintenance.EmployeeId == technicianId
                && maintenance.StartTime.Kind == DateTimeKind.Utc && maintenance.EndTime is null
                && maintenance.Status == "InProgress" && maintenance.Description == "Clean fans");
            await Barrier(viewer, waiterId, "Computer-only viewer receives no maintenance event");
        }
        await Expect("Open maintenance cannot be duplicated", HttpStatusCode.Conflict,
            await Send(HttpMethod.Post, path, admin, new { description = "Duplicate" }));
        await Expect("Open maintenance blocks direct availability override", HttpStatusCode.Conflict,
            await Send(HttpMethod.Patch, $"/api/admin/computers/{computer.ComputerId}/status", admin,
                new { status = "Available", expectedStatus = "Maintenance" }));
        await Expect("Description can be edited", HttpStatusCode.OK,
            await Send(HttpMethod.Put, path + $"/{maintenance.MaintenanceId}", tokens["Technician"], new { description = "Done cleaning" }));
        await Expect("Description length is validated", HttpStatusCode.BadRequest,
            await Send(HttpMethod.Put, path + $"/{maintenance.MaintenanceId}", admin, new { description = new string('x', 256) }));
        await Expect("Record cannot be addressed through another computer", HttpStatusCode.NotFound,
            await Send(HttpMethod.Post, $"/api/admin/computers/{existing.First(row => row.ComputerId != computer.ComputerId).ComputerId}/maintenances/{maintenance.MaintenanceId}/complete", admin));
        var completed = await Complete(maintenance.MaintenanceId);
        Check("Completion records UTC end time", completed.Status == "Completed" && completed.EndTime >= completed.StartTime
            && completed.EndTime.Value.Kind == DateTimeKind.Utc);
        await Expect("Completed maintenance history is immutable", HttpStatusCode.Conflict,
            await Send(HttpMethod.Put, path + $"/{maintenance.MaintenanceId}", admin, new { description = "Rewrite history" }));
        await using (var peer = await RealtimeTestPeer.ConnectAsync(app.GetTestServer(), tokens["Waiter"]))
        {
            var retry = await Complete(maintenance.MaintenanceId);
            Check("Completion retry preserves original end time", retry.EndTime == completed.EndTime);
            await Barrier(peer, waiterId, "Completion retry emits no duplicate event");
        }
        using (var response = await Send(HttpMethod.Post, path, admin, new { description = "Second maintenance" }))
        {
            response.EnsureSuccessStatusCode();
            var second = (await response.Content.ReadFromJsonAsync<ComputerMaintenanceDto>())!;
            await Complete(maintenance.MaintenanceId);
            Check("Retrying an old completion cannot free a new maintenance", (await Read<ComputerDto[]>("/api/admin/computers"))
                .Single(row => row.ComputerId == computer.ComputerId) is { Status: "Maintenance", HasOpenMaintenance: true });
            await Complete(second.MaintenanceId);
        }
        Check("History retains both records and saved description", (await Read<ComputerMaintenanceDto[]>(path))
            is { Length: 2 } history && history.Single(row => row.MaintenanceId == maintenance.MaintenanceId).Description == "Done cleaning");
        await Expect("Maintenance history prevents computer deletion", HttpStatusCode.Conflict,
            await Send(HttpMethod.Delete, $"/api/admin/computers/{computer.ComputerId}", admin));

        await using (var scope = app.Services.CreateAsyncScope())
        {
            if (scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Database.IsSqlServer())
            {
                var responses = await Task.WhenAll(Enumerable.Range(0, 6).Select(_ => Send(HttpMethod.Post, path, admin, new { description = "Race" })));
                Check("SQL concurrent maintenance creates exactly one record", responses.Count(row => row.StatusCode == HttpStatusCode.Created) == 1
                    && responses.Count(row => row.StatusCode == HttpStatusCode.Conflict) == 5);
                foreach (var response in responses) response.Dispose();
                var open = (await Read<ComputerMaintenanceDto[]>(path)).Single(row => row.EndTime is null);
                var completions = await Task.WhenAll(Enumerable.Range(0, 6).Select(_ => Complete(open.MaintenanceId)));
                Check("SQL concurrent completion preserves one end time", completions.Select(row => row.EndTime).Distinct().Count() == 1);
            }
            else Console.WriteLine("SKIP management SQL concurrency/rollback: InMemory does not prove database locking or transactions.");
        }
        var removable = await CreateComputer("CRUD-removable");
        await Expect("Unused computer can be deleted", HttpStatusCode.NoContent,
            await Send(HttpMethod.Delete, $"/api/admin/computers/{removable.ComputerId}", admin));
        Check("Deleted computer stays absent on fresh API read", !(await Read<ComputerDto[]>("/api/admin/computers"))
            .Any(row => row.ComputerId == removable.ComputerId));
        using (var response = await Send(HttpMethod.Post, "/api/admin/computer-types", admin, new { typeName = "Unused type", pricePerHour = 10000 }))
        {
            response.EnsureSuccessStatusCode();
            var unused = (await response.Content.ReadFromJsonAsync<ComputerTypeDto>())!;
            await Expect("Unused type can be deleted", HttpStatusCode.NoContent,
                await Send(HttpMethod.Delete, $"/api/admin/computer-types/{unused.ComputerTypeId}", admin));
        }
        await Expect("Missing computer returns 404", HttpStatusCode.NotFound,
            await Send(HttpMethod.Delete, "/api/admin/computers/2147483647", admin));
        await Expect("Missing type returns 404", HttpStatusCode.NotFound,
            await Send(HttpMethod.Put, "/api/admin/computer-types/2147483647", admin, new { typeName = "Missing", pricePerHour = 10000 }));
        return checks;

        async Task<ComputerDto> CreateComputer(string name)
        {
            using var response = await Send(HttpMethod.Post, "/api/admin/computers", admin,
                new { computerName = name, roomId = room.RoomId, computerTypeId = type.ComputerTypeId, status = "Occupied", hasOpenSession = true });
            Check("Create computer", response.StatusCode == HttpStatusCode.Created);
            return (await response.Content.ReadFromJsonAsync<ComputerDto>())!;
        }
        async Task<ComputerMaintenanceDto> Complete(int id)
        {
            using var response = await Send(HttpMethod.Post, path + $"/{id}/complete", admin);
            Check("Complete maintenance", response.StatusCode == HttpStatusCode.OK);
            return (await response.Content.ReadFromJsonAsync<ComputerMaintenanceDto>())!;
        }
        async Task<T> Read<T>(string url)
        {
            using var response = await Send(HttpMethod.Get, url, admin);
            response.EnsureSuccessStatusCode();
            return (await response.Content.ReadFromJsonAsync<T>())!;
        }
        async Task<HttpResponseMessage> Send(HttpMethod method, string url, string? token, object? body = null)
        {
            using var request = new HttpRequestMessage(method, url);
            if (token is not null) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            if (body is not null) request.Content = JsonContent.Create(body);
            return await client.SendAsync(request);
        }
        async Task Expect(string name, HttpStatusCode status, HttpResponseMessage response)
        {
            using (response) Check($"{name}: expected {(int)status}, got {(int)response.StatusCode}", response.StatusCode == status);
            await Task.CompletedTask;
        }
        int EventId(JsonElement message, string target)
        {
            Check($"Receives {target}", message.GetProperty("target").GetString() == target);
            return message.GetProperty("arguments")[0].GetProperty("entityId").GetInt32();
        }
        async Task Barrier(RealtimeTestPeer peer, int accountId, string name)
        {
            await using var scope = app.Services.CreateAsyncScope();
            var marker = new RealtimeEvent(Guid.NewGuid(), "CheckBarrier", 1, DateTimeOffset.UtcNow);
            await scope.ServiceProvider.GetRequiredService<IRealtimePublisher>()
                .PublishToAccountAsync(RealtimeAccountType.Staff, accountId, marker, CancellationToken.None);
            var message = await peer.NextAsync();
            Check(name, message.GetProperty("target").GetString() == marker.Type
                && message.GetProperty("arguments")[0].GetProperty("eventId").GetGuid() == marker.EventId);
        }
        void Check(string name, bool passed)
        {
            if (!passed) throw new InvalidOperationException(name);
            checks++;
            Console.WriteLine($"PASS {name}");
        }
    }
}
