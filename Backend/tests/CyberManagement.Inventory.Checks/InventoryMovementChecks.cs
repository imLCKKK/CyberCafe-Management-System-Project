using CyberManagement.Application.DTOs.Inventory;
using CyberManagement.Application.Exceptions;
using CyberManagement.Application.Interfaces.Repositories;
using CyberManagement.Application.Interfaces.Services;
using CyberManagement.Domain.Entities;
using CyberManagement.Infrastructure.Persistance;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

// Chỉ chạy trên database SQL riêng do InventoryCheckDatabase tạo.
internal static class InventoryMovementChecks
{
    private sealed record Source(int OrderId, int DetailId, int ProductId, int Quantity);
    private sealed class ForcedFailure : Exception { }

    public static async Task RunAsync(IServiceProvider services, Action<string, bool> check)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(45));
        var ct = timeout.Token;
        int employeeId, customerId, categoryId;

        await using (var scope = services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            if (!db.Database.IsSqlServer())
                throw new InvalidOperationException("Movement checks require real SQL Server.");

            employeeId = await db.Employees.Where(x => x.Username == "admin")
                .Select(x => x.EmployeeId).SingleAsync(ct);
            customerId = await db.Customers.Where(x => x.Phone == "0900000013")
                .Select(x => x.CustomerId).SingleAsync(ct);
            var category = new Category { CategoryName = "SCRUM30_MOVEMENT_CHECKS", IsActive = true };
            db.Categories.Add(category);
            await db.SaveChangesAsync(ct);
            categoryId = category.CategoryId;
        }

        var normal = await NewSource(10, 3);
        var insufficient = await NewSource(1, 2);
        var rollback = await NewSource(10, 2);
        var noSale = await NewSource(5, 1);
        var inactive = await NewSource(5, 1, active: false);
        var raceA = await NewSource(5, 4);
        var raceB = await NewSource(5, 4, sharedProductId: raceA.ProductId);
        var repeated = await NewSource(6, 2);
        var saleKey = $"Sale:Order:{normal.OrderId}:Item:{normal.DetailId}";
        var returnKey = $"Return:Order:{normal.OrderId}:Item:{normal.DetailId}";

        await using (var scope = services.CreateAsyncScope())
        {
            var service = scope.ServiceProvider.GetRequiredService<IInventoryService>();
            await Expect<InvalidOperationException>(() => service.ApplySaleAsync(
                normal.DetailId, normal.ProductId, normal.Quantity, null, saleKey, ct));
        }
        check("Sale requires an outer order transaction", true);

        var sale = await Apply(normal, false, saleKey);
        var state = await Read(normal.ProductId);
        check("Sale persists stock, signed quantity, source and system actor",
            sale.Id > 0 && sale.QuantityDelta == -3 && state.Stock == 7
            && state.Movements.Count == 1 && state.Movements[0] is
                { Type: "Sale", Before: 10, After: 7, EmployeeId: null }
            && state.Movements[0].OrderId == normal.OrderId
            && state.Movements[0].OrderDetailId == normal.DetailId
            && state.Movements[0].OperationKey == saleKey);

        var retry = await Apply(normal, false, saleKey);
        var changedKey = await Apply(normal, false, saleKey + ":retry");
        state = await Read(normal.ProductId);
        check("Sale retry and a changed key return the original movement",
            retry.Id == sale.Id && changedKey.Id == sale.Id
            && state.Stock == 7 && state.Movements.Count == 1);

        await Expect<BusinessRuleException>(() => Apply(insufficient, false, saleKey));
        check("An operation key cannot be reused for another source",
            (await Read(insufficient.ProductId)) is { Stock: 1, Movements.Count: 0 });

        await Expect<BusinessRuleException>(() =>
            Apply(normal, false, saleKey + ":bad-quantity", quantityOverride: 2));
        check("Movement quantity must match the entire order detail",
            (await Read(normal.ProductId)) is { Stock: 7, Movements.Count: 1 });

        var returned = await Apply(normal, true, returnKey);
        var returnRetry = await Apply(normal, true, returnKey);
        var returnOtherKey = await Apply(normal, true, returnKey + ":retry");
        state = await Read(normal.ProductId);
        check("Return restores stock once and retains the original Sale",
            returned.QuantityDelta == 3 && returned.Id == returnRetry.Id
            && returned.Id == returnOtherKey.Id && state.Stock == 10
            && state.Movements.Count == 2
            && state.Movements.Single(x => x.Type == "Return") is { Before: 7, After: 10 });

        var lateSale = await Apply(normal, false, saleKey);
        check("A late Sale retry after Return does not reserve stock again",
            lateSale.Id == sale.Id
            && (await Read(normal.ProductId)) is { Stock: 10, Movements.Count: 2 });

        await Expect<BusinessRuleException>(() =>
            Apply(noSale, true, $"Return:Item:{noSale.DetailId}"));
        check("A detail without Sale cannot return stock",
            (await Read(noSale.ProductId)) is { Stock: 5, Movements.Count: 0 });

        await Expect<BusinessRuleException>(() =>
            Apply(insufficient, false, $"Sale:Item:{insufficient.DetailId}"));
        check("Insufficient stock leaves stock and history unchanged",
            (await Read(insufficient.ProductId)) is { Stock: 1, Movements.Count: 0 });

        await Expect<BusinessRuleException>(() =>
            Apply(inactive, false, $"Sale:Item:{inactive.DetailId}"));
        check("A new Sale rejects an inactive product",
            (await Read(inactive.ProductId)) is { Stock: 5, Movements.Count: 0 });

        await Expect<ForcedFailure>(() =>
            Apply(rollback, false, $"Sale:Item:{rollback.DetailId}", forceFailure: true));
        await using (var scope = services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var orderStatus = await db.Orders.Where(x => x.OrderId == rollback.OrderId)
                .Select(x => x.Status).SingleAsync(ct);
            check("An outer failure rolls back the order, saved stock and saved history",
                orderStatus == "Pending"
                && (await Read(rollback.ProductId)) is { Stock: 10, Movements.Count: 0 });
        }

        // Hai DbContext riêng cùng bắt đầu xuất trên một sản phẩm chỉ đủ cho một đơn.
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var readyA = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var readyB = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var contenderA = Compete(raceA, readyA);
        var contenderB = Compete(raceB, readyB);
        await Task.WhenAll(readyA.Task, readyB.Task).WaitAsync(ct);
        gate.SetResult();
        var outcomes = await Task.WhenAll(contenderA, contenderB);
        state = await Read(raceA.ProductId);
        check("Two concurrent orders cannot oversell the same product",
            outcomes.Count(x => x) == 1 && outcomes.Count(x => !x) == 1
            && state.Stock == 1 && state.Movements.Count == 1
            && state.Movements[0] is { Before: 5, After: 1, Quantity: -4 });

        var repeatedKey = $"Sale:Item:{repeated.DetailId}";
        var duplicates = await Task.WhenAll(
            Apply(repeated, false, repeatedKey),
            Apply(repeated, false, repeatedKey));
        state = await Read(repeated.ProductId);
        check("Concurrent duplicate Sale calls persist one movement",
            duplicates[0].Id == duplicates[1].Id
            && state.Stock == 4 && state.Movements.Count == 1);

        var repeatedReturnKey = $"Return:Item:{repeated.DetailId}";
        duplicates = await Task.WhenAll(
            Apply(repeated, true, repeatedReturnKey),
            Apply(repeated, true, repeatedReturnKey));
        check("Concurrent duplicate Return calls restore stock once",
            duplicates[0].Id == duplicates[1].Id
            && (await Read(repeated.ProductId)) is { Stock: 6, Movements.Count: 2 });

        async Task<bool> Compete(Source item, TaskCompletionSource ready)
        {
            try
            {
                await Apply(item, false, $"Race:Item:{item.DetailId}", beforeMovement: async () =>
                {
                    ready.TrySetResult();
                    await gate.Task.WaitAsync(ct);
                });
                return true;
            }
            catch (BusinessRuleException) { return false; }
            finally { ready.TrySetResult(); }
        }

        async Task<InventoryMovementDto> Apply(
            Source item, bool isReturn, string key,
            int? quantityOverride = null, bool forceFailure = false,
            Func<Task>? beforeMovement = null)
        {
            await using var scope = services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var uow = scope.ServiceProvider.GetRequiredService<IInventoryUnitOfWork>();
            var service = scope.ServiceProvider.GetRequiredService<IInventoryService>();
            return await uow.ExecuteAsync(async () =>
            {
                // Mô phỏng bên đặt/hủy món: khóa Order trước Product.
                var order = await db.Orders.FromSqlInterpolated(
                    $"SELECT * FROM [dbo].[Orders] WITH (UPDLOCK, HOLDLOCK) WHERE [OrderId] = {item.OrderId}")
                    .SingleAsync(ct);
                if (beforeMovement is not null) await beforeMovement();
                if (forceFailure) order.Status = "Cancelled";
                var quantity = quantityOverride ?? item.Quantity;
                var result = isReturn
                    ? await service.ApplyReturnAsync(item.DetailId, item.ProductId, quantity, null, key, ct)
                    : await service.ApplySaleAsync(item.DetailId, item.ProductId, quantity, null, key, ct);
                if (forceFailure) throw new ForcedFailure();
                return result;
            }, ct);
        }

        async Task<Source> NewSource(int stock, int quantity,
            bool active = true, int? sharedProductId = null)
        {
            await using var scope = services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var productId = sharedProductId ?? 0;
            if (sharedProductId is null)
            {
                var product = new Product
                {
                    CategoryId = categoryId, ProductName = $"TEST_{Guid.NewGuid():N}",
                    Price = 10000, Stock = stock, IsActive = active
                };
                db.Products.Add(product);
                await db.SaveChangesAsync(ct);
                productId = product.ProductId;
            }
            var order = new Order
            {
                CustomerId = customerId, EmployeeId = employeeId,
                TotalAmount = quantity * 10000m, Status = "Pending"
            };
            var detail = new OrderDetail
            {
                Order = order, ProductId = productId, Quantity = quantity,
                UnitPrice = 10000, ProductNameSnapshot = "TEST kho SQL"
            };
            db.OrderDetails.Add(detail);
            await db.SaveChangesAsync(ct);
            return new Source(order.OrderId, detail.OrderDetailId, productId, quantity);
        }

        async Task<(int Stock, List<InventoryTransaction> Movements)> Read(int productId)
        {
            // Đọc bằng DbContext mới để chứng minh dữ liệu đã lưu thật.
            await using var scope = services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            return (
                await db.Products.Where(x => x.ProductId == productId).Select(x => x.Stock).SingleAsync(ct),
                await db.InventoryTransactions.AsNoTracking().Where(x => x.ProductId == productId)
                    .OrderBy(x => x.InvTransId).ToListAsync(ct));
        }
    }

    private static async Task Expect<T>(Func<Task> operation) where T : Exception
    {
        try { await operation(); }
        catch (T) { return; }
        throw new InvalidOperationException($"Expected {typeof(T).Name}.");
    }
}
