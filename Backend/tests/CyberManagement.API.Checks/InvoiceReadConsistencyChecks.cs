using System.Data.Common;
using CyberManagement.Application.DTOs.Invoices;
using CyberManagement.Application.Interfaces.Repositories;
using CyberManagement.Application.Interfaces.Services;
using CyberManagement.Domain.Entities;
using CyberManagement.Infrastructure.Persistance;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

// Dừng giữa hai truy vấn thật để kiểm tra đọc hóa đơn khi hoàn tiền đồng thời.
internal static class InvoiceReadConsistencyChecks
{
    public static async Task<int> RunAsync(IServiceProvider services, InvoiceReadGate gate,
        int employeeId, int customerId, int productId)
    {
        var checks = 0;
        foreach (var mode in new[] { "admin-list", "admin-detail", "customer-list", "customer-detail" })
        {
            int orderId, originalPaymentId, detailId, invoiceId;
            await using (var seedScope = services.CreateAsyncScope())
            {
                var db = seedScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var order = new Order { CustomerId = customerId, EmployeeId = employeeId, TotalAmount = 100000 };
                order.Details.Add(new OrderDetail { ProductId = productId, ProductNameSnapshot = "Read race",
                    Quantity = 1, UnitPrice = 100000 });
                db.Orders.Add(order);
                await db.SaveChangesAsync();
                orderId = order.OrderId; detailId = order.Details.Single().OrderDetailId;
                var payment = new Payment { OrderId = orderId, Amount = 100000, Method = "Cash",
                    Status = "Succeeded", OperationKey = "read-paid:" + mode,
                    PaymentTime = DateTimeOffset.UtcNow, ConfirmedAt = DateTimeOffset.UtcNow, PaymentTimeBasis = "Source" };
                db.Payments.Add(payment);
                await db.SaveChangesAsync();
                originalPaymentId = payment.PaymentId;
                invoiceId = (await seedScope.ServiceProvider.GetRequiredService<IInvoiceService>().IssueAsync(
                    new IssueInvoiceRequest { Kind = "Orders", OrderIds = [orderId] }, employeeId, "read-issue:" + mode)).Id;
            }

            await using var readerScope = services.CreateAsyncScope();
            var readerDb = readerScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(25));
            using var pause = gate.Arm(readerDb);
            var reading = ReadAsync(readerScope.ServiceProvider.GetRequiredService<IInvoiceService>(), timeout.Token);
            Task<InvoiceDto?>? writing = null;
            try
            {
                await pause.Arrived.Task.WaitAsync(timeout.Token);
                // NOWAIT chứng minh khóa đã giữ, không dựa vào độ nhanh/chậm của máy chạy test.
                await using (var probeScope = services.CreateAsyncScope())
                {
                    var probeDb = probeScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                    await using var probeTransaction = await probeDb.Database.BeginTransactionAsync(timeout.Token);
                    var blocked = false;
                    try
                    {
                        await probeDb.Customers.FromSqlInterpolated(
                            $"SELECT * FROM [dbo].[Customers] WITH (UPDLOCK, HOLDLOCK, NOWAIT) WHERE [CustomerId] = {customerId}")
                            .AsNoTracking().SingleAsync(timeout.Token);
                    }
                    catch (SqlException ex) when (ex.Number == 1222) { blocked = true; }
                    await probeTransaction.RollbackAsync(timeout.Token);
                    Check(mode + " reader holds customer lock across money queries", blocked);
                }
                writing = RefundAsync(timeout.Token);
                pause.Resume.TrySetResult();
                var before = await reading.WaitAsync(timeout.Token);
                var after = await writing.WaitAsync(timeout.Token);
                Check(mode + " read remains wholly before refund", before.Status == "Paid" && before.PaidAmount == 100000
                    && before.CancelledAmount == 0 && before.RefundAmount == 0 && before.AmountDue == 0);
                Check(mode + " writer completes without deadlock", after is { Status: "Cancelled", PaidAmount: 0,
                    CancelledAmount: 100000, RefundAmount: 100000, AmountDue: 0 });
                Check(mode + " read-owned transaction is released", readerDb.Database.CurrentTransaction is null);
                var fresh = await ReadAsync(readerScope.ServiceProvider.GetRequiredService<IInvoiceService>(), timeout.Token);
                Check(mode + " fresh read has no false debt", fresh.Status == "Cancelled" && fresh.AmountDue == 0
                    && fresh.OverpaidAmount == 0 && fresh.CancelledAmount == 100000 && fresh.RefundAmount == 100000);
            }
            finally
            {
                pause.Resume.TrySetResult();
                try { await reading.WaitAsync(TimeSpan.FromSeconds(30)); } catch { }
                if (writing is not null) try { await writing.WaitAsync(TimeSpan.FromSeconds(30)); } catch { }
            }

            async Task<InvoiceDto> ReadAsync(IInvoiceService service, CancellationToken ct) => mode switch
            {
                "admin-list" => (await service.GetAllAsync(employeeId, ct)).Single(x => x.Id == invoiceId),
                "admin-detail" => await service.GetByIdAsync(invoiceId, employeeId, ct),
                "customer-list" => (await service.GetOwnAsync(customerId, ct)).Single(x => x.Id == invoiceId),
                _ => await service.GetOwnByIdAsync(invoiceId, customerId, ct)
            };
            async Task<InvoiceDto?> RefundAsync(CancellationToken ct)
            {
                await using var scope = services.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                return await scope.ServiceProvider.GetRequiredService<IInvoiceUnitOfWork>().ExecuteAsync(async () =>
                {
                    await db.Customers.FromSqlInterpolated(
                        $"SELECT * FROM [dbo].[Customers] WITH (UPDLOCK, HOLDLOCK) WHERE [CustomerId] = {customerId}")
                        .AsNoTracking().SingleAsync(ct);
                    var order = await db.Orders.Include(x => x.Details).SingleAsync(x => x.OrderId == orderId, ct);
                    order.Status = "Cancelled"; order.Details.Single().Status = "Cancelled";
                    db.Payments.Add(new Payment { OrderId = orderId, OrderDetailId = detailId, RefundOfPaymentId = originalPaymentId,
                        Type = "Refund", Amount = 100000, Method = "Cash", Status = "Succeeded", OperationKey = "read-refund:" + mode,
                        Reason = "Verified cancellation", PaymentTime = DateTimeOffset.UtcNow,
                        ConfirmedAt = DateTimeOffset.UtcNow, PaymentTimeBasis = "Source" });
                    await db.SaveChangesAsync(ct);
                    return await scope.ServiceProvider.GetRequiredService<IInvoiceService>().ApplyFinancialChangeAsync(orderId, null, ct);
                }, ct);
            }
        }
        // Đọc trong transaction chung không được tự commit hoặc rollback thay caller.
        await using (var scope = services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var repository = scope.ServiceProvider.GetRequiredService<IInvoiceRepository>();
            await using var transaction = await db.Database.BeginTransactionAsync();
            await repository.ReadAsync(customerId, null, default);
            Check("Read preserves caller-owned transaction", ReferenceEquals(db.Database.CurrentTransaction, transaction));
            var customer = await db.Customers.SingleAsync(x => x.CustomerId == customerId);
            var oldName = customer.FullName; customer.FullName = "Read must not commit this";
            await db.SaveChangesAsync();
            await repository.ReadAsync(customerId, null, default);
            await transaction.RollbackAsync();
            await using var verifyScope = services.CreateAsyncScope();
            Check("Caller can roll back after read", await verifyScope.ServiceProvider.GetRequiredService<ApplicationDbContext>()
                .Customers.AsNoTracking().Where(x => x.CustomerId == customerId).Select(x => x.FullName).SingleAsync() == oldName);
        }
        // Request bị hủy giữa chừng phải thả khóa và transaction do nó mở.
        await using (var scope = services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            using var pause = gate.Arm(db);
            using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(20));
            var reading = scope.ServiceProvider.GetRequiredService<IInvoiceRepository>().ReadAsync(customerId, null, cancellation.Token);
            try
            {
                await pause.Arrived.Task.WaitAsync(cancellation.Token);
                cancellation.Cancel();
                var cancelled = false;
                try { await reading; } catch (OperationCanceledException) { cancelled = true; }
                Check("Cancelled read releases its transaction", cancelled && db.Database.CurrentTransaction is null);
                await using var probeScope = services.CreateAsyncScope();
                var probeDb = probeScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                await using var transaction = await probeDb.Database.BeginTransactionAsync();
                await probeDb.Customers.FromSqlInterpolated(
                    $"SELECT * FROM [dbo].[Customers] WITH (UPDLOCK, HOLDLOCK, NOWAIT) WHERE [CustomerId] = {customerId}")
                    .AsNoTracking().SingleAsync();
                Check("Cancelled read releases customer lock", true);
            }
            finally { pause.Resume.TrySetResult(); try { await reading; } catch (OperationCanceledException) { } }
        }
        return checks;
        void Check(string name, bool passed)
        {
            if (!passed) throw new Exception("Invoice read consistency failed: " + name);
            checks++; Console.WriteLine("PASS Invoice consistency: " + name);
        }
    }
}

internal sealed class InvoiceReadGate : DbCommandInterceptor
{
    private InvoiceReadPause? current;
    public InvoiceReadPause Arm(ApplicationDbContext context) => current = new(context);
    public override async ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command,
        CommandEventData data, InterceptionResult<DbDataReader> result, CancellationToken ct = default)
    {
        var pause = current;
        if (pause is not null && ReferenceEquals(data.Context, pause.Context)
            && command.CommandText.Contains("FROM [Payments]", StringComparison.Ordinal)
            && Interlocked.CompareExchange(ref pause.Paused, 1, 0) == 0)
        {
            pause.Arrived.TrySetResult();
            await pause.Resume.Task.WaitAsync(TimeSpan.FromSeconds(25), ct);
        }
        return result;
    }
}

internal sealed class InvoiceReadPause(ApplicationDbContext context) : IDisposable
{
    public ApplicationDbContext Context { get; } = context;
    public int Paused;
    public TaskCompletionSource Arrived { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public TaskCompletionSource Resume { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public void Dispose() => Resume.TrySetResult();
}
