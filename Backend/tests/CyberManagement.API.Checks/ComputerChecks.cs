using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using CyberManagement.Application.DTOs.Computers;
using CyberManagement.Domain.Entities;
using CyberManagement.Infrastructure.Persistance;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

internal static class ComputerChecks
{
    public static async Task<int> RunAsync(
        IServiceProvider services, HttpClient client, IReadOnlyDictionary<string, string> staffTokens,
        string customerToken)
    {
        var checks = 0;
        var adminToken = staffTokens["Admin"];
        var listPaths = new[] { "/api/admin/computers", "/api/admin/computer-types", "/api/admin/computer-rooms" };

        foreach (var path in listPaths)
        {
            await ExpectStatus($"Anonymous cannot read {path}", HttpStatusCode.Unauthorized,
                await SendAsync(HttpMethod.Get, path, null));
            await ExpectStatus($"Customer cannot read {path}", HttpStatusCode.Forbidden,
                await SendAsync(HttpMethod.Get, path, customerToken));
            foreach (var (role, token) in staffTokens)
                await ExpectStatus($"{role} can read {path}", HttpStatusCode.OK,
                    await SendAsync(HttpMethod.Get, path, token));

            using var response = await SendAsync(HttpMethod.Get, path, adminToken);
            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            Check($"Empty {path} returns an array", document.RootElement.ValueKind == JsonValueKind.Array
                && document.RootElement.GetArrayLength() == 0);
        }

        var validChange = new { status = "Maintenance", expectedStatus = "Available" };
        await ExpectStatus("Anonymous cannot update computers", HttpStatusCode.Unauthorized,
            await PatchAsync(null, "1", validChange));
        await ExpectStatus("Customer cannot update computers", HttpStatusCode.Forbidden,
            await PatchAsync(customerToken, "1", validChange));
        foreach (var role in new[] { "Cashier", "Waiter" })
            await ExpectStatus($"{role} cannot update computers", HttpStatusCode.Forbidden,
                await PatchAsync(staffTokens[role], "1", validChange));

        var (stations, rooms, types) = await SeedAsync(services);
        var free = stations["Free"];
        var listed = await ReadAsync<ComputerDto[]>(listPaths[0]);
        Check("Computer list contains persisted rows in room/name/ID order",
            listed.Select(row => row.ComputerId).SequenceEqual(stations.Values
                .OrderBy(row => row.RoomId).ThenBy(row => row.ComputerName).ThenBy(row => row.ComputerId)
                .Select(row => row.ComputerId)));
        Check("Closed sessions do not mark a machine as open",
            !listed.Single(row => row.ComputerId == free.ComputerId).HasOpenSession);
        foreach (var key in new[] { "Occupied", "ActiveEnded", "EndedOpen" })
            Check($"Open-session flag handles {key}", listed.Single(
                row => row.ComputerId == stations[key].ComputerId).HasOpenSession);
        Check("Open maintenance is flagged independently of its status text",
            listed.Single(row => row.ComputerId == stations["OpenMaintenance"].ComputerId).HasOpenMaintenance);
        Check("Closed maintenance is not flagged",
            !listed.Single(row => row.ComputerId == stations["ClosedMaintenance"].ComputerId).HasOpenMaintenance);
        Check("Unknown machine states are preserved in queries",
            listed.Single(row => row.ComputerId == stations["Unknown"].ComputerId).Status == "LegacyOffline");

        using (var response = await SendAsync(HttpMethod.Get, listPaths[0], staffTokens["Technician"]))
        {
            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            var allowedProperties = new HashSet<string>([
                "computerId", "computerName", "roomId", "computerTypeId", "status", "hasOpenSession", "hasOpenMaintenance"
            ], StringComparer.Ordinal);
            Check("Computer JSON exposes only the agreed fields, without session/customer/financial data",
                document.RootElement.EnumerateArray().All(row =>
                    row.EnumerateObject().Select(property => property.Name).ToHashSet().SetEquals(allowedProperties)));
        }

        var listedTypes = await ReadAsync<ComputerTypeDto[]>(listPaths[1]);
        Check("Computer types retain SQL IDs, prices and nullable specs",
            listedTypes.Select(row => row.ComputerTypeId).SequenceEqual(types.Select(row => row.ComputerTypeId))
            && listedTypes[0].TypeName == types[0].TypeName && listedTypes[0].PricePerHour == 10001m
            && listedTypes[0].CPU is null && listedTypes[0].RAM is null && listedTypes[0].GPU is null
            && listedTypes[0].Description is null && listedTypes[1].CPU == "Test CPU");
        using (var response = await SendAsync(HttpMethod.Get, listPaths[1], adminToken))
        {
            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            Check("Type specification names use camelCase JSON",
                document.RootElement[1].GetProperty("cpu").GetString() == "Test CPU"
                && document.RootElement[1].GetProperty("ram").GetString() == "32 GB"
                && document.RootElement[1].GetProperty("gpu").GetString() == "Test GPU");
        }
        var listedRooms = await ReadAsync<ComputerRoomDto[]>(listPaths[2]);
        Check("Rooms retain SQL IDs, floors and nullable descriptions",
            listedRooms.Select(row => row.RoomId).SequenceEqual(rooms.Select(row => row.RoomId))
            && listedRooms[0].RoomName == rooms[0].RoomName && listedRooms[0].Floor == 1
            && listedRooms[0].Description is null && listedRooms[1].Floor == 2);

        foreach (var (role, status, expected) in new[]
        {
            ("Admin", "Maintenance", "Available"),
            ("Manager", "Available", "Maintenance"),
            ("Technician", "Maintenance", "Available")
        })
        {
            using var response = await PatchAsync(staffTokens[role], free.ComputerId.ToString(),
                new { status, expectedStatus = expected });
            var updated = await ReadSuccessAsync<ComputerDto>(response);
            Check($"{role} updates state and receives the committed record", updated.Status == status
                && updated.ComputerId == free.ComputerId && updated.ComputerName == free.ComputerName
                && !updated.HasOpenSession && !updated.HasOpenMaintenance);
            await using var scope = services.CreateAsyncScope();
            Check($"{role} change survives a fresh DbContext", await scope.ServiceProvider
                .GetRequiredService<ApplicationDbContext>().Computers.AsNoTracking()
                .AnyAsync(row => row.ComputerId == free.ComputerId && row.Status == status));
        }

        foreach (var id in new[] { "0", "-1", "not-an-int", "2147483648" })
            await ExpectStatus($"Invalid computer ID {id}", HttpStatusCode.BadRequest,
                await PatchAsync(adminToken, id, validChange));
        await ExpectStatus("Unknown computer returns 404", HttpStatusCode.NotFound,
            await PatchAsync(adminToken, int.MaxValue.ToString(), validChange));

        var invalidChanges = new object?[]
        {
            null, new { }, new { status = "Available" }, new { expectedStatus = "Available" },
            new { status = (string?)null, expectedStatus = "Available" },
            new { status = "Maintenance", expectedStatus = (string?)null },
            new { status = "Occupied", expectedStatus = "Available" },
            new { status = "Maintenance", expectedStatus = "Occupied" },
            new { status = "maintenance", expectedStatus = "Available" },
            new { status = "Maintenance", expectedStatus = "available" },
            new { status = " Available ", expectedStatus = "Available" },
            new { status = 1, expectedStatus = "Available" }
        };
        foreach (var payload in invalidChanges)
            await ExpectStatus($"Invalid status payload {JsonSerializer.Serialize(payload)}", HttpStatusCode.BadRequest,
                await PatchAsync(adminToken, free.ComputerId.ToString(), payload));

        foreach (var key in new[] { "Occupied", "ActiveEnded", "EndedOpen", "OpenMaintenance", "Unknown" })
            await ExpectStatus($"Unsafe state {key} cannot be overridden", HttpStatusCode.Conflict,
                await PatchAsync(adminToken, stations[key].ComputerId.ToString(),
                    key == "OpenMaintenance" ? new { status = "Available", expectedStatus = "Maintenance" } : validChange));
        await ExpectStatus("An idempotent target still checks inconsistent open-session data", HttpStatusCode.Conflict,
            await PatchAsync(adminToken, stations["ActiveEnded"].ComputerId.ToString(),
                new { status = "Available", expectedStatus = "Maintenance" }));

        await ExpectStatus("Stale expected state returns a conflict", HttpStatusCode.Conflict,
            await PatchAsync(adminToken, free.ComputerId.ToString(),
                new { status = "Available", expectedStatus = "Available" }));
        await using (var scope = services.CreateAsyncScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            Check("Rejected requests leave persisted machine states intact",
                await database.Computers.AnyAsync(row => row.ComputerId == free.ComputerId && row.Status == "Maintenance")
                && (await database.Computers.Where(row => row.ComputerId != free.ComputerId).AsNoTracking().ToListAsync())
                    .All(row => row.Status == stations.Values.Single(original => original.ComputerId == row.ComputerId).Status));
        }

        await ExpectStatus("A fresh expected state allows recovery after conflict", HttpStatusCode.OK,
            await PatchAsync(adminToken, free.ComputerId.ToString(),
                new { status = "Available", expectedStatus = "Maintenance" }));
        await ExpectStatus("Retrying the committed transition is safe", HttpStatusCode.OK,
            await PatchAsync(adminToken, free.ComputerId.ToString(),
                new { status = "Available", expectedStatus = "Maintenance" }));
        await ExpectStatus("Completed maintenance permits returning to service", HttpStatusCode.OK,
            await PatchAsync(adminToken, stations["ClosedMaintenance"].ComputerId.ToString(),
                new { status = "Available", expectedStatus = "Maintenance" }));

        var first = stations["First"];
        await ExpectStatus("Status request cannot overwrite other machine fields", HttpStatusCode.OK,
            await PatchAsync(adminToken, first.ComputerId.ToString(), new
            {
                status = "Maintenance", expectedStatus = "Available", computerName = "Injected name",
                roomId = rooms[1].RoomId, computerTypeId = types[1].ComputerTypeId,
                hasOpenSession = false, pricePerHour = 1
            }));
        var reloaded = await ReadAsync<ComputerDto[]>(listPaths[0]);
        var reloadedFirst = reloaded.Single(row => row.ComputerId == first.ComputerId);
        Check("GET reload retains the change and ignores injected fields", reloadedFirst.Status == "Maintenance"
            && reloadedFirst.ComputerName == first.ComputerName && reloadedFirst.RoomId == first.RoomId
            && reloadedFirst.ComputerTypeId == first.ComputerTypeId);

        await using (var scope = services.CreateAsyncScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            Check("Status changes do not create maintenance or money records",
                await database.ComputerMaintenances.CountAsync() == 2 && await database.GamingSessions.CountAsync() == 4
                && !await database.SessionCharges.AnyAsync() && !await database.WalletTransactions.AnyAsync()
                && !await database.Invoices.AnyAsync());
            if (database.Database.IsSqlServer())
            {
                var responses = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ =>
                    PatchAsync(adminToken, free.ComputerId.ToString(), validChange)));
                foreach (var response in responses)
                    await ExpectStatus("Concurrent SQL retries serialize without duplicate transitions or deadlocks",
                        HttpStatusCode.OK, response);
                Check("Concurrent writes persist a single final state", await database.Computers.AsNoTracking()
                    .AnyAsync(row => row.ComputerId == free.ComputerId && row.Status == "Maintenance"));
            }
            else
                Console.WriteLine("SKIP SQL concurrency/locking: InMemory cannot verify relational transactions.");
        }

        return checks;

        async Task<HttpResponseMessage> SendAsync(HttpMethod method, string path, string? token, object? body = null)
        {
            using var request = new HttpRequestMessage(method, path);
            if (token is not null)
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            if (method == HttpMethod.Patch)
                request.Content = JsonContent.Create(body);
            return await client.SendAsync(request);
        }

        Task<HttpResponseMessage> PatchAsync(string? token, string id, object? body) =>
            SendAsync(HttpMethod.Patch, $"/api/admin/computers/{id}/status", token, body);

        async Task<T> ReadAsync<T>(string path)
        {
            using var response = await SendAsync(HttpMethod.Get, path, adminToken);
            return await ReadSuccessAsync<T>(response);
        }

        static async Task<T> ReadSuccessAsync<T>(HttpResponseMessage response)
        {
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<T>()
                ?? throw new InvalidOperationException("Computer API returned no JSON result.");
        }

        async Task ExpectStatus(string name, HttpStatusCode expected, HttpResponseMessage response)
        {
            using (response)
                Check($"{name}: expected {(int)expected}, got {(int)response.StatusCode}; "
                    + (response.StatusCode == expected ? "" : await response.Content.ReadAsStringAsync()),
                    response.StatusCode == expected);
        }

        void Check(string name, bool passed)
        {
            if (!passed)
                throw new InvalidOperationException(name);
            checks++;
            Console.WriteLine($"PASS {name}");
        }
    }

    private static async Task<(Dictionary<string, Computer> Stations, ComputerRoom[] Rooms, ComputerType[] Types)>
        SeedAsync(IServiceProvider services)
    {
        await using var scope = services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var rooms = new[]
        {
            new ComputerRoom { RoomName = "Test Room 1", Floor = 1 },
            new ComputerRoom { RoomName = "Test Room 2", Floor = 2, Description = "Second floor" }
        };
        var types = new[]
        {
            new ComputerType { TypeName = "Standard", PricePerHour = 10001m },
            new ComputerType { TypeName = "Premium", PricePerHour = 20000, CPU = "Test CPU", RAM = "32 GB", GPU = "Test GPU" }
        };
        database.AddRange(rooms);
        database.AddRange(types);
        await database.SaveChangesAsync();
        var stations = new Dictionary<string, Computer>
        {
            ["Free"] = Station("PC-02"),
            ["First"] = Station("PC-01"),
            ["OtherRoom"] = Station("PC-01", room: rooms[1], type: types[1]),
            ["Occupied"] = Station("PC-03", "Occupied"),
            ["ActiveEnded"] = Station("PC-04"),
            ["EndedOpen"] = Station("PC-05"),
            ["OpenMaintenance"] = Station("PC-06", "Maintenance"),
            ["ClosedMaintenance"] = Station("PC-07", "Maintenance"),
            ["Unknown"] = Station("PC-08", "LegacyOffline")
        };
        database.Computers.AddRange(stations.Values);
        var customerId = await database.Customers.Where(row => row.Phone == "0900000001")
            .Select(row => row.CustomerId).SingleAsync();
        var employeeId = await database.Employees.Where(row => row.Username == "admin")
            .Select(row => row.EmployeeId).SingleAsync();
        var start = new DateTime(2026, 11, 3, 1, 0, 0, DateTimeKind.Utc);
        foreach (var (key, status, end) in new (string, string, DateTime?)[]
        {
            ("Free", "Ended", start.AddMinutes(10)), ("Occupied", "Active", null),
            ("ActiveEnded", "Active", start.AddMinutes(10)), ("EndedOpen", "Ended", null)
        })
            database.GamingSessions.Add(new GamingSession
            {
                Computer = stations[key], CustomerId = customerId, Status = status, StartTime = start, EndTime = end
            });
        database.ComputerMaintenances.AddRange(
            new ComputerMaintenance
            {
                Computer = stations["OpenMaintenance"], EmployeeId = employeeId, StartTime = start, Status = "Completed"
            },
            new ComputerMaintenance
            {
                Computer = stations["ClosedMaintenance"], EmployeeId = employeeId, StartTime = start,
                EndTime = start.AddMinutes(10), Status = "Completed"
            });
        await database.SaveChangesAsync();
        return (stations, rooms, types);

        Computer Station(string name, string status = "Available", ComputerRoom? room = null, ComputerType? type = null) =>
            new() { ComputerName = name, Status = status, Room = room ?? rooms[0], ComputerType = type ?? types[0] };
    }
}
