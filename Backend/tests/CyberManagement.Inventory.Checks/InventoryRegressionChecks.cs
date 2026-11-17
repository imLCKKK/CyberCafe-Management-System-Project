using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using CyberManagement.API.Controller;
using CyberManagement.Application.DTOs.Inventory;
using CyberManagement.Application.Exceptions;
using CyberManagement.Application.Interfaces.Repositories;
using CyberManagement.Application.Interfaces.Services;
using CyberManagement.Domain.Entities;
using CyberManagement.Infrastructure.Persistance;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

// Ca tái hiện lỗi chỉ chạy trên database GUID của bộ test.
internal static class InventoryRegressionChecks
{
    private sealed record Source(int OrderId, int DetailId, int ProductId, int Quantity);
    private sealed class ForcedFailure : Exception { }

    public static async Task RunAsync(IServiceProvider services, HttpClient client, Action<string, bool> check)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(90));
        var ct = timeout.Token;
        var admin = await Login("admin");
        var failures = new List<Exception>();
        foreach (var test in new (string Name, Func<Task> Run)[]
        {
            ("tracked product freshness", TrackedProduct),
            ("manual movement idempotency", ManualRetry)
        })
        {
            try { await test.Run(); }
            catch (Exception ex)
            {
                Console.WriteLine($"FAIL regression {test.Name}: {ex.Message}");
                failures.Add(ex);
            }
        }
        if (failures.Count > 0) throw new AggregateException("Inventory regressions failed.", failures);

        async Task TrackedProduct()
        {
            var stale = await NewSource(10, 5);
            await using (var scope = services.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var product = await db.Products.SingleAsync(x => x.ProductId == stale.ProductId, ct);
                product.ProductName = "PENDING stale name";
                db.ChangeTracker.DetectChanges();
                using var deduction = await Post(stale.ProductId, "Adjust", -8, "Other writer", Guid.NewGuid().ToString("D"));
                RequireStatus(deduction, HttpStatusCode.Created);
                await Expect<BusinessRuleException>(() => Sale(scope.ServiceProvider, stale));
            }
            var staleState = await Read(stale.ProductId);
            check("Tracked Modified product uses locked SQL stock: 10 minus 8 cannot sell 5",
                staleState.Stock == 2 && staleState.Movements.Count == 1
                && staleState.Movements[0] is { Before: 10, After: 2 });

            var valid = await NewSource(10, 1);
            await using (var scope = services.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var product = await db.Products.SingleAsync(x => x.ProductId == valid.ProductId, ct);
                product.ProductName = "PENDING retained name";
                product.Price = 12000;
                using var deduction = await Post(valid.ProductId, "Adjust", -8, "Other writer", Guid.NewGuid().ToString("D"));
                RequireStatus(deduction, HttpStatusCode.Created);
                await Sale(scope.ServiceProvider, valid);
            }
            var validState = await Read(valid.ProductId);
            check("Refreshing stock retains pending name and price and rebases history",
                validState is { Stock: 1, Name: "PENDING retained name", Price: 12000 }
                && validState.Movements.Count == 2
                && validState.Movements[1] is { Before: 2, After: 1 });

            var stopped = await NewSource(10, 1);
            await using (var scope = services.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var product = await db.Products.SingleAsync(x => x.ProductId == stopped.ProductId, ct);
                product.ProductName = "PENDING stopped name";
                using var request = new HttpRequestMessage(HttpMethod.Put, $"/api/admin/products/{stopped.ProductId}/status")
                { Content = JsonContent.Create(new { isActive = false }) };
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", admin);
                using var response = await client.SendAsync(request, ct);
                RequireStatus(response, HttpStatusCode.OK);
                await Expect<BusinessRuleException>(() => Sale(scope.ServiceProvider, stopped));
            }
            check("Tracked Modified product observes the current SQL inactive status",
                (await Read(stopped.ProductId)) is { Stock: 10, Active: false, Movements.Count: 0 });

            foreach (var changeStock in new[] { true, false })
            {
                var unsafeSource = await NewSource(10, 1);
                await using var scope = services.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var product = await db.Products.SingleAsync(x => x.ProductId == unsafeSource.ProductId, ct);
                db.ChangeTracker.AutoDetectChangesEnabled = false;
                if (changeStock) product.Stock = 99;
                else product.IsActive = false;
                await Expect<BusinessRuleException>(() => Sale(scope.ServiceProvider, unsafeSource));
                check($"Pre-lock {(changeStock ? "stock" : "sale-status")} changes are rejected even without automatic detection",
                    (await Read(unsafeSource.ProductId)) is { Stock: 10, Active: true, Movements.Count: 0 });
            }

            var reentrant = await NewSource(10, 1);
            await using (var scope = services.CreateAsyncScope())
            {
                var uow = scope.ServiceProvider.GetRequiredService<IInventoryUnitOfWork>();
                var repository = scope.ServiceProvider.GetRequiredService<IInventoryRepository>();
                await Expect<ForcedFailure>(() => uow.ExecuteAsync<int>(async () =>
                {
                    var product = (await repository.GetProductForUpdateAsync(reentrant.ProductId, ct))!;
                    product.Stock = 9;
                    product.ProductName = "PENDING under lock";
                    var again = await repository.GetProductForUpdateAsync(reentrant.ProductId, ct);
                    check("Repeated reads under the same held lock preserve pending changes",
                        ReferenceEquals(product, again) && again!.Stock == 9
                        && again.ProductName == "PENDING under lock");
                    throw new ForcedFailure();
                }, ct));
                await uow.ExecuteAsync(async () =>
                    (await repository.GetProductForUpdateAsync(reentrant.ProductId, ct))!.Stock, ct);
                using var deduction = await Post(reentrant.ProductId, "Adjust", -8, "New transaction", Guid.NewGuid().ToString("D"));
                RequireStatus(deduction, HttpStatusCode.Created);
                var stock = await uow.ExecuteAsync(async () =>
                    (await repository.GetProductForUpdateAsync(reentrant.ProductId, ct))!.Stock, ct);
                check("Product lock cache never carries stale stock into another transaction", stock == 2);
            }

            var first = await NewSource(10, 3);
            var second = await NewSource(10, 4, first.ProductId);
            await using (var scope = services.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var service = scope.ServiceProvider.GetRequiredService<IInventoryService>();
                await scope.ServiceProvider.GetRequiredService<IInventoryUnitOfWork>().ExecuteAsync(async () =>
                {
                    await LockOrder(db, first.OrderId);
                    await LockOrder(db, second.OrderId);
                    await service.ApplySaleAsync(first.DetailId, first.ProductId, 3, null, $"Sale:{first.DetailId}", ct);
                    return await service.ApplySaleAsync(second.DetailId, second.ProductId, 4, null, $"Sale:{second.DetailId}", ct);
                }, ct);
            }
            var combined = await Read(first.ProductId);
            check("Two details sharing a product in one transaction produce continuous history",
                combined.Stock == 3 && combined.Movements.Count == 2
                && combined.Movements[0] is { Before: 10, After: 7 }
                && combined.Movements[1] is { Before: 7, After: 3 });
        }

        async Task ManualRetry()
        {
            var source = await NewSource(10, 1);
            var key = Guid.NewGuid().ToString("D");
            using var first = await Post(source.ProductId, "Receive", 2, "  Same action  ", key);
            RequireStatus(first, HttpStatusCode.Created);
            var movement = (await first.Content.ReadFromJsonAsync<InventoryMovementDto>(ct))!;
            using var retry = await Post(source.ProductId, "Receive", 2, "Same action", key.ToUpperInvariant());
            RequireStatus(retry, HttpStatusCode.Created);
            var replay = (await retry.Content.ReadFromJsonAsync<InventoryMovementDto>(ct))!;
            check("Same manual key and normalized payload return the original movement ID",
                movement.Id == replay.Id && movement == replay);
            check("Manual retry changes SQL stock once: 10 to 12",
                (await Read(source.ProductId)) is { Stock: 12, Movements.Count: 1 });

            var adjustment = await NewSource(3, 1);
            var adjustmentKey = Guid.NewGuid().ToString("D");
            using var adjusted = await Post(adjustment.ProductId, "Adjust", -2, "Adjust once", adjustmentKey);
            RequireStatus(adjusted, HttpStatusCode.Created);
            using var adjustedRetry = await Post(adjustment.ProductId, "Adjust", -2, "Adjust once", adjustmentKey);
            RequireStatus(adjustedRetry, HttpStatusCode.Created);
            var adjustedMovement = (await adjusted.Content.ReadFromJsonAsync<InventoryMovementDto>(ct))!;
            var adjustedReplay = (await adjustedRetry.Content.ReadFromJsonAsync<InventoryMovementDto>(ct))!;
            check("Negative Adjust retry returns its old ID without deducting again",
                adjustedMovement.Id == adjustedReplay.Id
                && (await Read(adjustment.ProductId)) is { Stock: 1, Movements.Count: 1 });


            var other = await NewSource(10, 1);
            foreach (var changed in new[]
            {
                (source.ProductId, "Receive", 3, "Same action"),
                (source.ProductId, "Adjust", 2, "Same action"),
                (source.ProductId, "Receive", 2, "Different reason"),
                (other.ProductId, "Receive", 2, "Same action")
            })
            {
                using var response = await Post(changed.Item1, changed.Item2, changed.Item3, changed.Item4, key);
                RequireStatus(response, HttpStatusCode.Conflict);
                check("Reusing a manual key with a changed payload returns 409", true);
            }
            check("Conflicting manual retries do not change either product",
                (await Read(source.ProductId)) is { Stock: 12, Movements.Count: 1 }
                && (await Read(other.ProductId)) is { Stock: 10, Movements.Count: 0 });

            using var next = await Post(source.ProductId, "Receive", 2, "Same action", Guid.NewGuid().ToString("D"));
            RequireStatus(next, HttpStatusCode.Created);
            check("A new manual key represents a new intentional action",
                (await Read(source.ProductId)) is { Stock: 14, Movements.Count: 2 });

            var manager = await Login("manager01");
            using var managerResponse = await Post(source.ProductId, "Receive", 2, "Same action", key, manager);
            RequireStatus(managerResponse, HttpStatusCode.Created);
            var managerMovement = (await managerResponse.Content.ReadFromJsonAsync<InventoryMovementDto>(ct))!;
            check("Manual keys are scoped to the authenticated employee",
                managerMovement.Id != movement.Id && managerMovement.EmployeeId != movement.EmployeeId
                && (await Read(source.ProductId)).Stock == 16);

            foreach (var invalidKey in new string?[] { null, "", "not-a-uuid", Guid.Empty.ToString("D") })
            {
                using var response = await Post(source.ProductId, "Receive", 2, "Invalid key", invalidKey);
                RequireStatus(response, HttpStatusCode.BadRequest);
                check("Missing or invalid manual key returns 400", true);
            }
            using (var multiple = await Post(source.ProductId, "Receive", 2, "Multiple keys", key, duplicateHeader: true))
            {
                RequireStatus(multiple, HttpStatusCode.BadRequest);
                check("Multiple Idempotency-Key header values are rejected", true);
            }
            var cashier = await Login("cashier01");
            using (var denied = await Post(source.ProductId, "Receive", 2, "Same action", key, cashier))
            {
                RequireStatus(denied, HttpStatusCode.Forbidden);
                check("Replay still requires the movement permission", true);
            }

            var concurrent = await NewSource(10, 1);
            var sameKey = Guid.NewGuid().ToString("D");
            var responses = await Task.WhenAll(
                Post(concurrent.ProductId, "Receive", 2, "Concurrent", sameKey),
                Post(concurrent.ProductId, "Receive", 2, "Concurrent", sameKey));
            try
            {
                foreach (var response in responses) RequireStatus(response, HttpStatusCode.Created);
                var a = (await responses[0].Content.ReadFromJsonAsync<InventoryMovementDto>(ct))!;
                var b = (await responses[1].Content.ReadFromJsonAsync<InventoryMovementDto>(ct))!;
                check("Concurrent identical manual requests return one ID and one stock change",
                    a.Id == b.Id && (await Read(concurrent.ProductId)) is { Stock: 12, Movements.Count: 1 });
            }
            finally { foreach (var response in responses) response.Dispose(); }

            var raceA = await NewSource(10, 1);
            var raceB = await NewSource(10, 1);
            var collisionKey = Guid.NewGuid().ToString("D");
            responses = await Task.WhenAll(
                Post(raceA.ProductId, "Receive", 2, "Key collision", collisionKey),
                Post(raceB.ProductId, "Receive", 2, "Key collision", collisionKey));
            try
            {
                var states = await Task.WhenAll(Read(raceA.ProductId), Read(raceB.ProductId));
                check("Concurrent key reuse for different products returns one success and one conflict",
                    responses.Count(x => x.StatusCode == HttpStatusCode.Created) == 1
                    && responses.Count(x => x.StatusCode == HttpStatusCode.Conflict) == 1
                    && states.Sum(x => x.Stock) == 22 && states.Sum(x => x.Movements.Count) == 1);
            }
            finally { foreach (var response in responses) response.Dispose(); }
        }

        async Task<string> Login(string username)
        {
            using var response = await client.PostAsJsonAsync("/api/auth/login",
                new { username, password = "Inventory-check-only-2026!" }, ct);
            response.EnsureSuccessStatusCode();
            return (await response.Content.ReadFromJsonAsync<LoginResponse>(ct))!.AccessToken;
        }

        async Task<HttpResponseMessage> Post(int productId, string type, int delta, string reason,
            string? key, string? token = null, bool duplicateHeader = false)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "/api/admin/inventory/movements")
            { Content = JsonContent.Create(new CreateInventoryMovementRequest(productId, type, delta, reason)) };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token ?? admin);
            if (key is not null)
                request.Headers.TryAddWithoutValidation("Idempotency-Key",
                    duplicateHeader ? new[] { key, Guid.NewGuid().ToString("D") } : new[] { key });
            return await client.SendAsync(request, ct);
        }

        async Task<InventoryMovementDto> Sale(IServiceProvider scoped, Source source) =>
            await scoped.GetRequiredService<IInventoryUnitOfWork>().ExecuteAsync(async () =>
            {
                await LockOrder(scoped.GetRequiredService<ApplicationDbContext>(), source.OrderId);
                return await scoped.GetRequiredService<IInventoryService>().ApplySaleAsync(
                    source.DetailId, source.ProductId, source.Quantity, null, $"Sale:{source.DetailId}", ct);
            }, ct);

        async Task LockOrder(ApplicationDbContext db, int orderId) =>
            _ = await db.Orders.FromSqlInterpolated(
                $"SELECT * FROM [dbo].[Orders] WITH (UPDLOCK,HOLDLOCK) WHERE [OrderId] = {orderId}").SingleAsync(ct);

        async Task<Source> NewSource(int stock, int quantity, int? sharedProductId = null)
        {
            await using var scope = services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var product = sharedProductId.HasValue
                ? await db.Products.SingleAsync(x => x.ProductId == sharedProductId.Value, ct)
                : new Product
                {
                    ProductName = $"REGRESSION_{Guid.NewGuid():N}", Price = 10000, Stock = stock,
                    Category = new Category { CategoryName = $"REGRESSION_{Guid.NewGuid():N}" }
                };
            var detail = new OrderDetail
            {
                Product = product, Quantity = quantity, UnitPrice = 10000, ProductNameSnapshot = product.ProductName,
                Order = new Order
                {
                    CustomerId = await db.Customers.Where(x => x.Phone == "0900000013").Select(x => x.CustomerId).SingleAsync(ct),
                    EmployeeId = await db.Employees.Where(x => x.Username == "admin").Select(x => x.EmployeeId).SingleAsync(ct),
                    TotalAmount = quantity * 10000m
                }
            };
            db.OrderDetails.Add(detail);
            await db.SaveChangesAsync(ct);
            return new Source(detail.OrderId, detail.OrderDetailId, product.ProductId, quantity);
        }

        async Task<(int Stock, bool Active, string Name, decimal Price, List<InventoryTransaction> Movements)> Read(int productId)
        {
            await using var scope = services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var product = await db.Products.AsNoTracking().SingleAsync(x => x.ProductId == productId, ct);
            return (product.Stock, product.IsActive, product.ProductName, product.Price,
                await db.InventoryTransactions.AsNoTracking().Where(x => x.ProductId == productId)
                    .OrderBy(x => x.InvTransId).ToListAsync(ct));
        }
    }

    public static async Task CheckReplayAfterRestartAsync(
        IServiceProvider services, HttpClient client, Action<string, bool> check)
    {
        InventoryTransaction original;
        int stockBefore;
        int countBefore;
        await using (var scope = services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var employeeId = await db.Employees.Where(x => x.Username == "admin")
                .Select(x => x.EmployeeId).SingleAsync();
            var prefix = $"Manual:{employeeId}:";
            original = await db.InventoryTransactions.AsNoTracking()
                .Where(x => x.Type == "Receive" && x.OperationKey!.StartsWith(prefix))
                .OrderBy(x => x.InvTransId).FirstAsync();
            stockBefore = await db.Products.Where(x => x.ProductId == original.ProductId)
                .Select(x => x.Stock).SingleAsync();
            countBefore = await db.InventoryTransactions.CountAsync(x => x.ProductId == original.ProductId);
        }

        using var loginResponse = await client.PostAsJsonAsync("/api/auth/login",
            new { username = "admin", password = "Inventory-check-only-2026!" });
        loginResponse.EnsureSuccessStatusCode();
        var login = (await loginResponse.Content.ReadFromJsonAsync<LoginResponse>())!;
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/admin/inventory/movements")
        {
            Content = JsonContent.Create(new CreateInventoryMovementRequest(
                original.ProductId, original.Type, original.Quantity, original.Reason))
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", login.AccessToken);
        request.Headers.Add("Idempotency-Key", Guid.ParseExact(original.OperationKey!.Split(':')[2], "N").ToString("D"));
        using var replayResponse = await client.SendAsync(request);
        RequireStatus(replayResponse, HttpStatusCode.Created);
        var replay = (await replayResponse.Content.ReadFromJsonAsync<InventoryMovementDto>())!;

        await using var verification = services.CreateAsyncScope();
        var verificationDb = verification.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        check("Manual retry after a new HTTP host uses its durable SQL movement",
            replay.Id == original.InvTransId && replay.StockAfter == original.After
            && await verificationDb.Products.Where(x => x.ProductId == original.ProductId)
                .Select(x => x.Stock).SingleAsync() == stockBefore
            && await verificationDb.InventoryTransactions.CountAsync(x => x.ProductId == original.ProductId) == countBefore);
    }


    private static void RequireStatus(HttpResponseMessage response, HttpStatusCode expected)
    {
        if (response.StatusCode != expected)
            throw new InvalidOperationException($"Expected HTTP {(int)expected}, got {(int)response.StatusCode}.");
    }

    private static async Task Expect<T>(Func<Task> operation) where T : Exception
    {
        try { await operation(); }
        catch (T) { return; }
        throw new InvalidOperationException($"Expected {typeof(T).Name}; operation unexpectedly succeeded.");
    }
}
