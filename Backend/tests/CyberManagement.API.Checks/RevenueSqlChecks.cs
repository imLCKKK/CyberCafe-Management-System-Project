using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Data.SqlClient;
using CyberManagement.Application;
using CyberManagement.Application.DTOs.Identity;
using CyberManagement.Application.DTOs.Revenue;
using CyberManagement.Application.Interfaces.Repositories;
using CyberManagement.Application.Interfaces.Services;
using CyberManagement.Application.Security;
using CyberManagement.Domain.Entities;
using CyberManagement.Infrastructure.Persistance;
using CyberManagement.Infrastructure.Repositories;
using CyberManagement.Infrastructure.Security;
using CyberManagement.Application.Wallets;
using CyberManagement.Infrastructure.Wallets;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

internal static class RevenueSqlChecks
{
    internal sealed record SeedData(DateTimeOffset Day, IReadOnlyDictionary<string,int> Employees,
        int CustomerId, int CashOrderId, int CashPaymentId, int GatewayRefundId, decimal Balance);

    internal static void Configure(IServiceCollection services, AuthCheckDatabase fixture, ReadProbe? probe = null)
    {
        services.AddDbContext<ApplicationDbContext>(options =>
        {
            fixture.Configure(options);
            if (probe is not null) options.AddInterceptors(probe);
        });
        services.AddApplicationServices();
        services.AddScoped<IRevenueQueries, RevenueQueries>();
        services.AddScoped<IIdentityRepository, IdentityRepository>();
        services.AddScoped<IEmployeePasswordHasher, EmployeePasswordHasher>();
        services.AddScoped<ICustomerIdentityRepository, CustomerIdentityRepository>();
        services.AddScoped<IComputerRepository, ComputerRepository>();
        services.AddScoped<ICatalogRepository, CatalogRepository>();
        services.AddScoped<ICustomerRepository, CustomerRepository>();
        services.AddScoped<ITournamentRepository, TournamentRepository>();
        services.AddScoped<ICounterTopUpRepository, CounterTopUpRepository>();
        services.AddScoped<IInventoryQueries, InventoryQueries>();
        services.AddScoped<IInventoryRepository, InventoryRepository>();
        services.AddScoped<IInventoryMovementRepository, InventoryMovementRepository>();
        services.AddScoped<IInventoryUnitOfWork, InventoryUnitOfWork>();
        services.AddScoped<ICustomerPasswordHasher, CustomerPasswordHasher>();
        services.AddScoped<IWalletLedgerWriter, WalletLedgerWriter>();
        services.AddScoped<SessionCommitEvents>();
        services.AddScoped<ISessionRepository, SessionRepository>();
        services.AddScoped<IOrderRepository, OrderRepository>();
        services.AddScoped<ISessionSettlement, SessionSettlement>();
        services.AddScoped<IVoucherRepository, VoucherRepository>();
        services.AddScoped<IPromotionUsageRepository, PromotionUsageRepository>();
        services.AddScoped<IVoucherUnitOfWork, VoucherUnitOfWork>();
        services.AddScoped<IInvoiceRepository, InvoiceRepository>();
        services.AddScoped<IInvoiceUnitOfWork, InvoiceUnitOfWork>();
    }

    public static async Task RunAsync()
    {
        var connection = Environment.GetEnvironmentVariable("AUTH_CHECKS_SQL_CONNECTION");
        if (string.IsNullOrWhiteSpace(connection)) throw new InvalidOperationException("AUTH_CHECKS_SQL_CONNECTION required; no InMemory fallback.");
        var fixture = new AuthCheckDatabase(connection);
        var probe = new ReadProbe();
        var services = new ServiceCollection(); Configure(services,fixture,probe);
        await using var provider = services.BuildServiceProvider();
        try
        {
            SeedData seed;
            await using(var scope=provider.CreateAsyncScope())
            {
                var db=scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                await fixture.InitializeAsync(db); seed=await SeedAsync(scope.ServiceProvider);
            }
            await using var read=provider.CreateAsyncScope();
            var service=read.ServiceProvider.GetRequiredService<IRevenueService>();
            var date=DateOnly.FromDateTime(seed.Day.DateTime);
            var report=await service.GetRevenueAsync(new(){From=date,To=date},seed.Employees["Admin"]);
            Check(report.CollectedAmount==440 && report.RefundedAmount==50 && report.NetRevenue==390 && report.TopUpCollectedAmount==50000,"SQL source money and counter bonus");
            Check(report.Sources.Single(x=>x.Source=="Gaming").NetRevenue==40,"SQL gaming/food source split");
            var reconciliation=await service.GetReconciliationAsync(new(){From=date,To=date},seed.Employees["Manager"]);
            Check(reconciliation.ConfirmationSummary.TransactionCount==1 && reconciliation.ConfirmationSummary.RefundedAmount==50,"SQL confirmation-basis refund");
            var query=read.ServiceProvider.GetRequiredService<IRevenueQueries>();
            var data=await query.ReadAsync(seed.Day,seed.Day.AddDays(1));
            Check(data.Payments.Any(x=>x.PaymentTime<seed.Day && x.Type=="Payment") && data.Payments.Any(x=>x.PaymentId==seed.GatewayRefundId),"SQL original payment outside reporting period");
            var dbRead=read.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            await using(var transaction=await dbRead.Database.BeginTransactionAsync())
            {
                var rejected=false;
                try { await query.ReadAsync(seed.Day,seed.Day.AddDays(1)); } catch(InvalidOperationException) { rejected=true; }
                Check(rejected,"Reporting rejects caller write transaction");
                await transaction.RollbackAsync();
            }
            Check(await dbRead.Customers.Where(x=>x.CustomerId==seed.CustomerId).Select(x=>x.WalletBalance).SingleAsync()==seed.Balance,"Report leaves stored balance unchanged");
            // Dừng sau khi SQL lấy khóa khách; writer khác phải chờ đến khi kết thúc đọc.
            probe.Arm();
            await using (var readerScope = provider.CreateAsyncScope())
            {
                var reading = readerScope.ServiceProvider.GetRequiredService<IRevenueQueries>().ReadAsync(seed.Day,seed.Day.AddDays(1));
                try
                {
                    await probe.Locked.Task.WaitAsync(TimeSpan.FromSeconds(10));
                    Check(await WriterBlockedAsync(), "Customer writer waits while report holds SQL locks");
                }
                finally { probe.Release.TrySetResult(); await reading; }
            }
            Check(!await WriterBlockedAsync(), "Customer locks released after successful report");
            probe.Arm();
            await using (var readerScope = provider.CreateAsyncScope())
            using (var cancelled = new CancellationTokenSource())
            {
                var reading = readerScope.ServiceProvider.GetRequiredService<IRevenueQueries>().ReadAsync(seed.Day,seed.Day.AddDays(1),cancelled.Token);
                var didCancel = false;
                try
                {
                    await probe.Locked.Task.WaitAsync(TimeSpan.FromSeconds(10));
                    cancelled.Cancel();
                    try { await reading; } catch (OperationCanceledException) { didCancel=true; }
                    Check(didCancel, "Cancellation interrupts report under SQL lock");
                }
                finally { probe.Release.TrySetResult(); try { await reading; } catch(OperationCanceledException) { } }
            }
            Check(!await WriterBlockedAsync(), "Customer locks released after cancellation");
            var integrationChecks=await RevenueInvoiceIntegrationChecks.RunAsync(provider,seed);
            Console.WriteLine($"{10+integrationChecks} revenue SQL checks passed; isolated fixture data and existing service hooks, no provider call.");

            async Task<bool> WriterBlockedAsync()
            {
                await using var writerScope=provider.CreateAsyncScope();
                var writer=writerScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                await writer.Database.OpenConnectionAsync();
                try
                {
                    await writer.Database.ExecuteSqlRawAsync("SET LOCK_TIMEOUT 200;");
                    try
                    {
                        await writer.Database.ExecuteSqlInterpolatedAsync($"UPDATE [Customers] SET [WalletBalance] = [WalletBalance] WHERE [CustomerId] = {seed.CustomerId}");
                        return false;
                    }
                    catch(SqlException ex) when(ex.Number==1222) { return true; }
                }
                finally { await writer.Database.ExecuteSqlRawAsync("SET LOCK_TIMEOUT -1;"); }
            }
        }
        finally { await fixture.CleanupAsync(); }
    }

    internal static async Task<SeedData> SeedAsync(IServiceProvider services)
    {
        var db=services.GetRequiredService<ApplicationDbContext>();
        await services.GetRequiredService<IIdentitySeeder>().SeedAsync(new IdentitySeedOptions(true,
            RolePermissionCatalog.Roles.ToDictionary(x=>x.Name,x=>(string?)$"{x.Name}-Revenue-fixture-2026!")),CancellationToken.None);
        var employees=await db.Employees.Include(x=>x.Role).ToDictionaryAsync(x=>x.Role.RoleName,x=>x.EmployeeId);
        var day=new DateTimeOffset(2026,11,16,0,0,0,TimeSpan.FromHours(7));
        var customer=new Customer {FullName="Revenue SQL",Phone="0900033000",WalletBalance=54660};
        var product=new Product {ProductName="Current price must not replace snapshot",Price=999,Stock=7,Category=new Category {CategoryName="Revenue SQL"}};
        var session=new GamingSession {Customer=customer,StartTime=day.UtcDateTime,TotalCost=40,
            Computer=new Computer {ComputerName="Revenue-PC",Room=new ComputerRoom {RoomName="Revenue",Floor=1},ComputerType=new ComputerType {TypeName="Revenue",PricePerHour=40}}};
        db.AddRange(customer,product,session); await db.SaveChangesAsync();
        Order Order(decimal amount,DateTimeOffset time) => new() {CustomerId=customer.CustomerId,EmployeeId=employees["Admin"],OrderTime=time.UtcDateTime,TotalAmount=amount,
            Details=[new OrderDetail {ProductId=product.ProductId,Quantity=1,UnitPrice=amount,ProductNameSnapshot="Original product snapshot"}]};
        var cash=Order(100,day); var gateway=Order(200,day.AddDays(-1)); var walletOrder=Order(300,day);
        var charge=new SessionCharge {GamingSessionId=session.GamingSessionId,StartTime=day.UtcDateTime,EndTime=day.AddMinutes(1).UtcDateTime,Amount=40};
        var request=new CounterTopUpRequest {CustomerId=customer.CustomerId,RequestKey=Guid.NewGuid(),Amount=50000,BonusAmount=5000,TotalAmount=55000,CreatedAt=day.UtcDateTime};
        db.AddRange(cash,gateway,walletOrder,charge,request); await db.SaveChangesAsync();
        WalletTransaction Ledger(string type,string kind,decimal amount,string source,string id,string key,decimal balance) => new()
        {CustomerId=customer.CustomerId,Type=type,Kind=kind,Amount=amount,SourceType=source,SourceId=id,OperationKey=key,ActorType="Staff",ActorEmployeeId=employees["Admin"],BalanceAfter=balance,CreatedAt=day.UtcDateTime};
        var topup=Ledger("TopUp","Credit",55000,"CounterTopUp",request.RequestId.ToString(),"CounterTopUp:"+request.RequestId,55000);
        var debit=Ledger("OrderPayment","Debit",-300,"Order",walletOrder.OrderId.ToString(),"OrderPayment:"+walletOrder.OrderId,54700);
        var gaming=Ledger("GamingCharge","Debit",-40,"SessionCharge",charge.ChargeId.ToString(),"SessionCharge:"+charge.ChargeId,54660);
        db.AddRange(topup,debit,gaming); await db.SaveChangesAsync();
        request.Status="Approved";request.ApprovedAt=day.UtcDateTime;request.ApprovedByEmployeeId=employees["Admin"];request.CashAmountReceived=50000;request.WalletTransactionId=topup.TransactionId;
        var invoice=new Invoice {CustomerId=customer.CustomerId,EmployeeId=employees["Admin"],InvoiceDate=day.UtcDateTime,Subtotal=100,TotalAmount=100,Status="Paid",
            InvoiceOrders=[new InvoiceOrder {OrderId=cash.OrderId}],Details=[new InvoiceDetail {OrderDetailId=cash.Details.Single().OrderDetailId,ItemType="Food",Description="Original snapshot",Quantity=1,UnitPrice=100,Amount=100}]};
        db.Invoices.Add(invoice); await db.SaveChangesAsync();
        Payment Pay(Order order,decimal amount,string method,DateTimeOffset time) => new() {OrderId=order.OrderId,Type="Payment",Method=method,Status="Succeeded",Amount=amount,
            OperationKey="Revenue:"+order.OrderId,CreatedAt=time,PaymentTime=time,ConfirmedAt=time,PaymentTimeBasis="Source"};
        var cashPayment=Pay(cash,100,"Cash",day);cashPayment.InvoiceId=invoice.InvoiceId;
        var gatewayPayment=Pay(gateway,200,"ZaloPay",day.AddDays(-1));gatewayPayment.MerchantTransactionId="fixture-old-order";gatewayPayment.GatewayTransactionId="fixture-provider-payment";gatewayPayment.ProviderOccurredAt=gatewayPayment.PaymentTime;
        var walletPayment=Pay(walletOrder,300,"Wallet",day);walletPayment.WalletTransactionId=debit.TransactionId;
        var gamingPayment=new Payment {SessionChargeId=charge.ChargeId,WalletTransactionId=gaming.TransactionId,Type="Payment",Method="Wallet",Status="Succeeded",Amount=40,OperationKey="Revenue:Gaming",CreatedAt=day,PaymentTime=day,ConfirmedAt=day,PaymentTimeBasis="Source"};
        db.AddRange(cashPayment,gatewayPayment,walletPayment,gamingPayment);
        var before=10;
        foreach(var order in new[]{cash,gateway,walletOrder})
        {
            db.InventoryTransactions.Add(new InventoryTransaction {ProductId=product.ProductId,OrderId=order.OrderId,OrderDetailId=order.Details.Single().OrderDetailId,
                Type="Sale",Quantity=-1,ReferenceType="Order",OperationKey="Revenue:Sale:"+order.OrderId,Reason="Fixture sale",Before=before,After=before-1,CreatedAt=day});before--;
        }
        await db.SaveChangesAsync();
        var refund=new Payment {OrderId=gateway.OrderId,RefundOfPaymentId=gatewayPayment.PaymentId,Type="Refund",Method="ZaloPay",Status="Succeeded",Amount=50,
            OperationKey="Revenue:Refund",Reason="Verified fixture refund",CreatedAt=day,PaymentTime=day,ConfirmedAt=day,PaymentTimeBasis="Confirmation"};
        db.Payments.Add(refund);await db.SaveChangesAsync();
        return new(day,employees,customer.CustomerId,cash.OrderId,cashPayment.PaymentId,refund.PaymentId,customer.WalletBalance);
    }
    internal sealed class ReadProbe : DbCommandInterceptor
    {
        private int armed;
        internal TaskCompletionSource Locked {get;private set;} = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource Release {get;private set;} = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal void Arm()
        {
            Locked=new(TaskCreationOptions.RunContinuationsAsynchronously);
            Release=new(TaskCreationOptions.RunContinuationsAsynchronously);
            Interlocked.Exchange(ref armed,1);
        }
        public override async ValueTask<DbDataReader> ReaderExecutedAsync(DbCommand command, CommandExecutedEventData eventData,
            DbDataReader result, CancellationToken cancellationToken=default)
        {
            if(command.CommandText.Contains("[dbo].[Customers] WITH (UPDLOCK, HOLDLOCK)",StringComparison.Ordinal)
                && Interlocked.CompareExchange(ref armed,0,1)==1)
            {
                Locked.TrySetResult();
                // Interceptor chưa chuyển quyền sở hữu reader cho EF khi đang dừng test.
                try { await Release.Task.WaitAsync(cancellationToken); }
                catch { await result.DisposeAsync(); throw; }
            }
            return result;
        }
    }
    private static void Check(bool result,string name) { if(!result) throw new InvalidOperationException(name); Console.WriteLine("PASS "+name); }
}
