using System.Reflection;
using CyberManagement.Application.DTOs.Realtime;
using CyberManagement.Application.Interfaces.Repositories;
using CyberManagement.Application.Interfaces.Services;
using CyberManagement.Domain.Entities;
using CyberManagement.Infrastructure.Persistance;
using CyberManagement.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using CyberManagement.Infrastructure;


internal static class RevenueQueryChecks
{
    // Kiểm tra phần đọc dữ liệu và DI; không thay kiểm thử khóa/transaction SQL.
    public static async Task<int> RunAsync()
    {
        var from = new DateTimeOffset(2026, 11, 16, 0, 0, 0, TimeSpan.FromHours(7));
        var to = from.AddDays(1);
        var failures = new List<string>();
        await Check("Historical composite refund remains available for invoice reconciliation", async db =>
        {
            AddOrder(db);
            AddInvoice(db);
            AddDebit(db, "Order", "11");
            AddRefund(db, "11:111", from.AddDays(-1));
            await db.SaveChangesAsync(); db.ChangeTracker.Clear();
            var data = await Read(db);
            Require(data.WalletTransactions.Any(x => x.TransactionId == 102), "Missing historical OrderRefund with SourceId 11:111");
            Require(data.WalletTransactions.Any(x => x.TransactionId == 101), "Missing original debit");
            Require(data.Orders.Single().OrderId == 11, "Missing invoice order");
        });
        await Check("Composite refund finds source when original debit has legacy metadata", async db =>
        {
            AddOrder(db);
            AddDebit(db, null, null);
            AddRefund(db, "11:111", from.AddHours(1));
            await db.SaveChangesAsync(); db.ChangeTracker.Clear();
            var data = await Read(db);
            Require(data.Orders.Any(x => x.OrderId == 11), "Composite refund did not resolve source order");
            Require(data.WalletTransactions.Count == 2, "Refund and original debit must remain available");
        });
        await Check("Mismatched composite source does not attach another order", async db =>
        {
            AddOrder(db);
            db.Orders.Add(new Order { OrderId=12, CustomerId=1, OrderTime=from.AddDays(-3).UtcDateTime });
            AddDebit(db, null, null);
            AddRefund(db, "12:111", from.AddHours(1));
            await db.SaveChangesAsync(); db.ChangeTracker.Clear();
            var data = await Read(db);
            Require(data.Orders.Count == 0, "Mismatched order/detail pair must not resolve a source");
            Require(data.WalletTransactions.Count == 2, "Keep unresolved money for the service to flag");
        });
        await Check("Payment wallet link and refund original payment survive date filtering", async db =>
        {
            AddOrder(db); AddInvoice(db); AddDebit(db, null, null);
            db.Payments.AddRange(
                new Payment { PaymentId=201, OrderId=11, Method="Wallet", Type="Payment", Status="Succeeded", Amount=100, OperationKey="original", WalletTransactionId=101, CreatedAt=from.AddDays(-2), PaymentTime=from.AddDays(-2) },
                new Payment { PaymentId=202, OrderId=11, Method="Wallet", Type="Refund", Status="Pending", Amount=20, OperationKey="refund", RefundOfPaymentId=201, CreatedAt=from.AddHours(1) });
            await db.SaveChangesAsync(); db.ChangeTracker.Clear();
            var data = await Read(db);
            Require(data.Payments.Select(x => x.PaymentId).SequenceEqual(new[] {201,202}), "Keep original payment outside period");
            Require(data.WalletTransactions.Single().TransactionId == 101, "Keep ledger through Payment.WalletTransactionId");
        });
        await Check("OriginalDebitId keeps refunds with unresolved legacy source", async db =>
        {
            AddOrder(db); AddInvoice(db); AddDebit(db, "Order", "11");
            AddRefund(db, "legacy-line", from.AddDays(-1));
            await db.SaveChangesAsync(); db.ChangeTracker.Clear();
            var data = await Read(db);
            Require(data.WalletTransactions.Any(x => x.TransactionId == 102), "Keep refund through its original debit even when source text is invalid");
        });
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IRealtimePublisher, NoOpRealtimePublisher>();
        services.AddInfrastructureServices(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>
        {
            ["ConnectionStrings:DefaultConnection"] = "Server=localhost;Database=RevenueDiOnly;Integrated Security=True;TrustServerCertificate=True"
        }).Build());
        using (var provider = services.BuildServiceProvider())
        using (var first = provider.CreateScope())
        using (var second = provider.CreateScope())
        {
            var firstQuery = first.ServiceProvider.GetService<IRevenueQueries>();
            var sameQuery = first.ServiceProvider.GetService<IRevenueQueries>();
            var otherQuery = second.ServiceProvider.GetService<IRevenueQueries>();
            if (firstQuery is not RevenueQueries || !ReferenceEquals(firstQuery,sameQuery) || ReferenceEquals(firstQuery,otherQuery))
            {
                failures.Add("Scoped DI registration"); Console.WriteLine("FAIL Scoped DI registration for IRevenueQueries");
            }
            else Console.WriteLine("PASS Scoped DI registration for IRevenueQueries");
        }
        Console.WriteLine($"{6-failures.Count}/6 InMemory read-data checks passed; SQL locks/transactions not exercised.");
        if (failures.Count > 0) throw new InvalidOperationException(string.Join("; ", failures));
        return 6;

        async Task Check(string name, Func<ApplicationDbContext,Task> test)
        {
            await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
            db.Customers.Add(new Customer { CustomerId=1, FullName="Test", Phone="0900000000" });
            try { await test(db); Console.WriteLine("PASS " + name); }
            catch(Exception ex) { failures.Add(name); Console.WriteLine("FAIL " + name + ": " + ex.Message); }
        }
        void AddOrder(ApplicationDbContext db)
        {
            db.Orders.Add(new Order { OrderId=11, CustomerId=1, OrderTime=from.AddDays(-3).UtcDateTime });
            db.OrderDetails.Add(new OrderDetail { OrderDetailId=111, OrderId=11, ProductId=1, Quantity=1, UnitPrice=100, ProductNameSnapshot="Test" });
        }
        void AddInvoice(ApplicationDbContext db)
        {
            db.Invoices.Add(new Invoice { InvoiceId=1, CustomerId=1, InvoiceDate=from.AddHours(2).UtcDateTime });
            db.InvoiceOrders.Add(new InvoiceOrder { InvoiceId=1, OrderId=11 });
        }
        void AddDebit(ApplicationDbContext db, string? sourceType, string? sourceId) => db.WalletTransactions.Add(new WalletTransaction
        {
            TransactionId=101, CustomerId=1, Type="OrderPayment", Kind=sourceType is null ? null : "Debit",
            SourceType=sourceType, SourceId=sourceId, Amount=-100, CreatedAt=from.AddDays(-3).UtcDateTime
        });
        void AddRefund(ApplicationDbContext db, string sourceId, DateTimeOffset time) => db.WalletTransactions.Add(new WalletTransaction
        {
            TransactionId=102, CustomerId=1, Type="OrderRefund", Kind="Refund", SourceType="OrderDetail", SourceId=sourceId,
            Amount=20, OriginalDebitId=101, CreatedAt=time.UtcDateTime
        });
        async Task<RevenueReadSet> Read(ApplicationDbContext db)
        {
            var method=typeof(RevenueQueries).GetMethod("ReadDataAsync", BindingFlags.NonPublic | BindingFlags.Instance)!;
            return await (Task<RevenueReadSet>)method.Invoke(new RevenueQueries(db), new object[] { new[]{1}, from, to, CancellationToken.None })!;
        }
        static void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    }

    private sealed class NoOpRealtimePublisher : IRealtimePublisher
    {
        public Task PublishToPermissionAsync(string permission, RealtimeEvent message, CancellationToken ct) => Task.CompletedTask;
        public Task PublishToAccountAsync(RealtimeAccountType type, int id, RealtimeEvent message, CancellationToken ct) => Task.CompletedTask;
        public Task PublishTournamentChangedAsync(RealtimeEvent message, CancellationToken ct) => Task.CompletedTask;
    }
}
