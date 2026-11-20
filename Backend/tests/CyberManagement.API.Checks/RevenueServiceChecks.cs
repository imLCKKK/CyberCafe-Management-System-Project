using System.ComponentModel.DataAnnotations;
using CyberManagement.Application;
using CyberManagement.Application.DTOs.Revenue;
using CyberManagement.Application.Exceptions;
using CyberManagement.Application.Interfaces.Repositories;
using CyberManagement.Application.Interfaces.Services;
using CyberManagement.Domain.Entities;
using Microsoft.Extensions.DependencyInjection;

internal static class RevenueServiceChecks
{
    public static async Task<int> RunAsync()
    {
        var count = 0;
        var failed = new List<string>();
        var filter = new RevenueFilterRequest { From = new(2026,11,16), To = new(2026,11,16) };
        var day = new DateTimeOffset(2026,11,16,0,0,0,TimeSpan.FromHours(7));
        await Case("Wallet debit/refund and mirror payments count once", async () =>
        {
            var f = new Fixture(day); f.Order(1,100); f.Wallet(1,1,100); f.WalletRefund(2,1,1,20);
            var r = await Report(f);
            Assert(r.CollectedAmount==100 && r.RefundedAmount==20 && r.NetRevenue==80);
        });
        await Case("Cash/ZaloPay, pending/failed and before-invoice collections", async () =>
        {
            var f=new Fixture(day); f.Order(1,100); f.Order(2,50);
            f.Pay(1,1,100,"Cash"); f.Pay(2,2,50,"ZaloPay");
            f.Pay(3,1,90,"Cash",status:"Pending"); f.Pay(4,1,90,"Cash",status:"Failed");
            var r=await Report(f); Assert(r.NetRevenue==150 && r.CollectedAmount==150);
        });
        await Case("Refund uses current period; original collection remains outside period", async () =>
        {
            var f=new Fixture(day); f.Order(1,100); f.Pay(1,1,100,"ZaloPay",at:day.AddDays(-1));
            f.Pay(2,1,20,"ZaloPay",type:"Refund",original:1);
            var r=await Report(f); Assert(r.CollectedAmount==0 && r.RefundedAmount==20 && r.NetRevenue==-20);
        });
        await Case("Vietnam date boundary includes start and excludes next day", async () =>
        {
            var f=new Fixture(day); f.Order(1,100);
            f.Pay(1,1,10,"Cash",at:day.AddTicks(-1)); f.Pay(2,1,20,"Cash",at:day);
            f.Pay(3,1,30,"Cash",at:day.AddDays(1).AddTicks(-1)); f.Pay(4,1,40,"Cash",at:day.AddDays(1));
            var repo=new FakeQueries(f.Data); var r=await Report(f,repo);
            Assert(r.CollectedAmount==50 && r.Daily.Single().Date==filter.From);
            Assert(repo.From==day.ToUniversalTime() && repo.To==day.AddDays(1).ToUniversalTime());
        });
        await Case("Gaming ledger source contributes once and keeps Food/Gaming split", async () =>
        {
            var f=new Fixture(day); f.Charges.Add(new SessionCharge { ChargeId=1, Amount=40, GamingSessionId=1, GamingSession=new GamingSession { GamingSessionId=1,CustomerId=1 },EndTime=day.UtcDateTime });
            f.Wallets.Add(new WalletTransaction { TransactionId=1, CustomerId=1,Kind="Debit",Type="GamingCharge",SourceType="SessionCharge",SourceId="1",Amount=-40,CreatedAt=day.UtcDateTime });
            f.Payments.Add(new Payment { PaymentId=1,SessionChargeId=1,WalletTransactionId=1,Method="Wallet",Type="Payment",Status="Succeeded",Amount=40,PaymentTime=day,ConfirmedAt=day,PaymentTimeBasis="Source" });
            var r=await Report(f); Assert(r.NetRevenue==40 && r.Sources.Single(x=>x.Source=="Gaming").NetRevenue==40);
        });
        await Case("Counter bonus excluded and top-ups separated from service revenue", async () =>
        {
            var f=new Fixture(day);
            f.TopUps.Add(new CounterTopUpRequest { RequestId=1,CustomerId=1,Amount=50000,BonusAmount=5000,TotalAmount=55000,Status="Approved",CashAmountReceived=50000,WalletTransactionId=1,ApprovedAt=day.UtcDateTime });
            f.Wallets.Add(new WalletTransaction { TransactionId=1,CustomerId=1,Kind="Credit",Type="TopUp",SourceType="CounterTopUp",SourceId="1",Amount=55000,CreatedAt=day.UtcDateTime });
            f.Wallets.Add(new WalletTransaction { TransactionId=2,CustomerId=1,Kind="Credit",Type="TopUp",SourceType="ZaloPayTopUp",SourceId="merchant-2",Amount=30000,CreatedAt=day.UtcDateTime });
            f.TopUps.Add(new CounterTopUpRequest { RequestId=2,Amount=100000,Status="Pending",CreatedAt=day.UtcDateTime });
            var r=await Report(f); Assert(r.TopUpCollectedAmount==80000 && r.NetRevenue==0);
        });
        await Case("Confirmation-basis refund keeps metadata and summary", async () =>
        {
            var f=new Fixture(day); f.Order(1,100); f.Pay(1,1,100,"ZaloPay",at:day.AddDays(-1));
            f.Pay(2,1,20,"ZaloPay",type:"Refund",original:1);
            var payment=f.Payments.Last(); payment.PaymentTimeBasis="Confirmation"; payment.ProviderOccurredAt=null;
            var r=await Reconcile(f);
            Assert(r.ConfirmationSummary.TransactionCount==1 && r.ConfirmationSummary.RefundedAmount==20);
            var t=r.Transactions.Single(x=>x.EntityType=="Payment" && x.EntityId==2);
            Assert(t.PaymentTime==day && t.ConfirmedAt==day && t.ProviderOccurredAt==null && t.PaymentTimeBasis=="Confirmation");
            payment.ProviderOccurredAt=day.AddDays(-1);
            var later=await Report(f); Assert(later.RefundedAmount==20);
        });
        await Case("Missing source is reported and prevents a misleading revenue total", async () =>
        {
            var f=new Fixture(day); f.Pay(1,99,100,"Cash");
            Assert((await Reconcile(f)).Issues.Any(x=>x.Code=="PaymentSourceInvalid"));
            await Throws<BusinessRuleException>(()=>Report(f));
        });
        await Case("Succeeded payment without time evidence is not silently zero", async () =>
        {
            var f=new Fixture(day); f.Order(1,100); f.Pay(1,1,100,"Cash"); f.Payments[0].PaymentTime=null;
            Assert((await Reconcile(f)).Issues.Any(x=>x.Code=="PaymentTimeInvalid"));
            await Throws<BusinessRuleException>(()=>Report(f));
        });
        await Case("Wallet mirror with no ledger is visible", async () =>
        {
            var f=new Fixture(day); f.Order(1,100); f.Pay(1,1,100,"Wallet"); f.Payments[0].WalletTransactionId=99;
            Assert((await Reconcile(f)).Issues.Any(x=>x.Code=="WalletLinkInvalid"));
            await Throws<BusinessRuleException>(()=>Report(f));
        });
        await Case("Unknown historical ledger source is visible", async () =>
        {
            var f=new Fixture(day); f.Wallets.Add(new WalletTransaction { TransactionId=1,CustomerId=1,Amount=-100,Type="OrderPayment",CreatedAt=day.UtcDateTime });
            Assert((await Reconcile(f)).Issues.Any(x=>x.Code=="WalletSourceInvalid"));
        });
        await Case("Cross-customer wallet refund is rejected", async () =>
        {
            var f=new Fixture(day); f.Order(1,100); f.Wallet(1,1,100); f.WalletRefund(2,1,1,20); f.Wallets.Last().CustomerId=2;
            Assert((await Reconcile(f)).Issues.Any(x=>x.Code=="WalletSourceInvalid"));
        });
        await Case("Known wallet source can be counted while missing mirror is reconciled", async () =>
        {
            var f=new Fixture(day); f.Order(1,100); f.Wallet(1,1,100); f.Payments.Clear();
            Assert((await Report(f)).NetRevenue==100);
            Assert((await Reconcile(f)).Issues.Any(x=>x.Code=="WalletMirrorMissing"));
        });
        await Case("Refund reservations do not reduce revenue and excess reservation is visible", async () =>
        {
            var f=new Fixture(day); f.Order(1,100); f.Pay(1,1,100,"Cash");
            f.Pay(2,1,60,"Cash",type:"Refund",status:"Pending",original:1); f.Pay(3,1,50,"Cash",type:"Refund",status:"Pending",original:1);
            Assert((await Report(f)).NetRevenue==100);
            Assert((await Reconcile(f)).Issues.Any(x=>x.Code=="RefundLimitExceeded"));
        });
        await Case("Missing sale, broken stock arithmetic and duplicate return are visible", async () =>
        {
            var f=new Fixture(day); f.Order(1,100);
            Assert((await Reconcile(f)).Issues.Any(x=>x.Code=="StockSaleMismatch"));
            f.Movements.Add(new InventoryTransaction { InvTransId=1,OrderId=1,OrderDetailId=10,ProductId=1,Type="Sale",Quantity=-1,Before=10,After=8 });
            f.Movements.AddRange(new InventoryTransaction { InvTransId=2,OrderId=1,OrderDetailId=10,ProductId=1,Type="Return",Quantity=1,Before=8,After=9 },new InventoryTransaction { InvTransId=3,OrderId=1,OrderDetailId=10,ProductId=1,Type="Return",Quantity=1,Before=9,After=10 });
            var r=await Reconcile(f); Assert(r.Issues.Any(x=>x.Code=="StockValuesInvalid") && r.Issues.Any(x=>x.Code=="StockReturnMismatch"));
        });
        await Case("Expired order must return stock and release voucher", async () =>
        {
            var f=new Fixture(day); f.Order(1,100); var o=f.Orders.Single(); o.Status="Expired"; o.VoucherCodeSnapshot="CODE";
            f.Usages.Add(new PromotionUsage { PromotionUsageId=1,OrderId=1,CustomerId=1,UsedAt=day });
            f.Movements.Add(new InventoryTransaction { InvTransId=1,OrderId=1,OrderDetailId=10,ProductId=1,Type="Sale",Quantity=-1,Before=10,After=9 });
            var r=await Reconcile(f); Assert(r.Issues.Any(x=>x.Code=="VoucherReleaseMismatch") && r.Issues.Any(x=>x.Code=="StockReturnMismatch"));
        });
        await Case("Invoice snapshot mismatch does not change the actual revenue", async () =>
        {
            var f=new Fixture(day); f.Order(1,100); f.Pay(1,1,100,"Cash");
            f.Invoices.Add(new Invoice { InvoiceId=1,CustomerId=1,InvoiceDate=day.UtcDateTime,Subtotal=100,TotalAmount=90,Status="Paid",InvoiceOrders=[new InvoiceOrder { InvoiceId=1,OrderId=1 }],Details=[new InvoiceDetail { InvoiceDetailId=1,InvoiceId=1,OrderDetailId=10,Quantity=1,UnitPrice=100,Amount=90 }] });
            var r=await Reconcile(f); Assert(r.Issues.Any(x=>x.Code=="InvoiceSnapshotMismatch"));
            Assert((await Report(f)).NetRevenue==100);
        });
        await Case("Balanced cash order, stock and invoice reconcile without changes", async () =>
        {
            var f=new Fixture(day); f.Order(1,100); f.Pay(1,1,100,"Cash"); f.Payments[0].InvoiceId=1;
            f.Movements.Add(new InventoryTransaction { InvTransId=1,OrderId=1,OrderDetailId=10,ProductId=1,Type="Sale",Quantity=-1,Before=10,After=9 });
            f.Invoices.Add(new Invoice { InvoiceId=1,CustomerId=1,InvoiceDate=day.UtcDateTime,Subtotal=100,TotalAmount=100,Status="Paid",InvoiceOrders=[new InvoiceOrder { InvoiceId=1,OrderId=1 }],Details=[new InvoiceDetail { InvoiceDetailId=1,InvoiceId=1,OrderDetailId=10,Quantity=1,UnitPrice=100,Amount=100 }] });
            var r=await Reconcile(f); Assert(r.IsBalanced && r.CheckedOrders==1 && r.CheckedInvoices==1);
            Assert(f.Movements.Single().After==9 && f.Orders.Single().TotalAmount==100);
        });
        await Case("Current permission, active account and employee ID checked before data read", async () =>
        {
            foreach(var employee in new Employee?[] { null, new(){ EmployeeId=1,IsActive=false },new(){EmployeeId=1,IsActive=true,Role=new Role()} })
            {
                var repo=new FakeQueries(new Fixture(day).Data) { Employee=employee };
                await Throws<UnauthorizedAccessException>(()=>Report(new Fixture(day),repo)); Assert(repo.ReadCalls==0);
            }
            var allowed=new FakeQueries(new Fixture(day).Data); allowed.Employee!.Role.RoleName="Custom";
            await Report(new Fixture(day),allowed); Assert(allowed.ReadCalls==1);
        });
        await Case("Invalid dates rejected before data read", async () =>
        {
            var repo=new FakeQueries(new Fixture(day).Data);
            using var scope=Provider(repo);
            var service=scope.GetRequiredService<IRevenueService>();
            await Throws<ValidationException>(()=>service.GetRevenueAsync(new(){ From=filter.To,To=filter.From.AddDays(-1)},1));
            await Throws<ValidationException>(()=>service.GetRevenueAsync(new(){ From=filter.From,To=filter.From.AddDays(366)},1));
            Assert(repo.ReadCalls==0);
        });
        await Case("Wallet payment cannot point to a top-up ledger", async () =>
        {
            var f=new Fixture(day); f.Order(1,100); f.Pay(1,1,100,"Wallet"); f.Payments[0].WalletTransactionId=1;
            f.Wallets.Add(new WalletTransaction { TransactionId=1,CustomerId=1,Type="TopUp",Kind="Credit",SourceType="CounterTopUp",SourceId="99",Amount=100,CreatedAt=day.AddDays(-1).UtcDateTime });
            Assert((await Reconcile(f)).Issues.Any(x=>x.Code=="WalletLinkInvalid"));
            await Throws<BusinessRuleException>(()=>Report(f));
        });
        await Case("Wallet refund mirror must retain original payment lineage", async () =>
        {
            var f=new Fixture(day); f.Order(1,100); f.Wallet(1,1,100); f.WalletRefund(2,1,1,20);
            f.Payments.Last().RefundOfPaymentId=999;
            Assert((await Reconcile(f)).Issues.Any(x=>x.Code=="WalletLinkInvalid"));
        });
        await Case("Default timestamps cannot silently hide a succeeded payment", async () =>
        {
            var f=new Fixture(day); f.Order(1,100); f.Pay(1,1,100,"Cash");
            f.Payments[0].PaymentTime=DateTimeOffset.MinValue; f.Payments[0].ConfirmedAt=DateTimeOffset.MinValue;
            await Throws<BusinessRuleException>(()=>Report(f));
        });
        await Case("Invoice source collection must be attached to that invoice", async () =>
        {
            var f=new Fixture(day); f.Order(1,100); f.Pay(1,1,100,"Cash");
            f.Invoices.Add(new Invoice { InvoiceId=1,CustomerId=1,InvoiceDate=day.UtcDateTime,Subtotal=100,TotalAmount=100,Status="Paid",InvoiceOrders=[new InvoiceOrder { InvoiceId=1,OrderId=1 }],Details=[new InvoiceDetail { InvoiceDetailId=1,InvoiceId=1,OrderDetailId=10,Quantity=1,UnitPrice=100,Amount=100 }] });
            Assert((await Reconcile(f)).Issues.Any(x=>x.Code=="PaymentInvoiceMismatch"));
        });
        await Case("Paid invoice with only a small collection is not balanced", async () =>
        {
            var f=InvoiceFixture(); f.Payments[0].Amount=10;
            var r=await Reconcile(f); Assert(!r.IsBalanced && r.Issues.Any(x=>x.Code=="InvoicePaidAmountMismatch"));
        });
        await Case("Partial refund with valid full-line return preserves remaining paid obligation", async () =>
        {
            var f=InvoiceFixture(); AddPartialRefund(f,true);
            var r=await Reconcile(f); Assert(r.IsBalanced);
        });
        await Case("Refund without cancellation evidence cannot reduce invoice obligation", async () =>
        {
            var f=InvoiceFixture(); AddPartialRefund(f,false);
            Assert((await Reconcile(f)).Issues.Any(x=>x.Code=="InvoicePaidAmountMismatch"));
        });
        await Case("Return alone does not cancel the source or invoice", async () =>
        {
            var f=InvoiceFixture(); AddPartialRefund(f,true);
            f.Orders[0].Details.Last().Status="Pending";f.Invoices[0].Details.Last().CancelledAmount=0;
            var r=await Reconcile(f);
            Assert(r.Issues.Any(x=>x.Code=="StockReturnUnexpected") && r.Issues.Any(x=>x.Code=="InvoicePaidAmountMismatch"));
        });
        await Case("Cancelled source line requires stock return even while order remains open", async () =>
        {
            var f=InvoiceFixture(); AddPartialRefund(f,true);f.Movements.RemoveAt(2);
            var r=await Reconcile(f);
            Assert(r.Issues.Any(x=>x.Code=="StockReturnMismatch"));
            Assert(!r.Issues.Any(x=>x.Code=="InvoicePaidAmountMismatch"));
        });
        await Case("Cancelled source must match invoice cancellation snapshot", async () =>
        {
            var f=InvoiceFixture(); AddPartialRefund(f,true);f.Invoices[0].Details.Last().CancelledAmount=0;
            Assert((await Reconcile(f)).Issues.Any(x=>x.Code=="InvoiceCancellationMismatch"));
        });
        await Case("Partial cancellation snapshot cannot understate the cancelled whole line", async () =>
        {
            var f=InvoiceFixture();AddPartialRefund(f,true);f.Invoices[0].Details.Last().CancelledAmount=20;
            Assert((await Reconcile(f)).Issues.Any(x=>x.Code=="InvoiceCancellationMismatch"));
        });
        await Case("Pending refund stays in net receipts and is shown as unresolved surplus", async () =>
        {
            var f=InvoiceFixture();AddPartialRefund(f,true);
            var refund=f.Payments.Last();refund.Status="Pending";refund.PaymentTime=null;refund.ConfirmedAt=null;refund.PaymentTimeBasis=null;
            var r=await Reconcile(f);
            Assert((await Report(f)).NetRevenue==100 && r.Issues.Any(x=>x.Code=="InvoiceRefundUnresolved" && x.ActualValue==40));
        });
        await Case("Closed order with uncancelled lines is inconsistent", async () =>
        {
            var f=InvoiceFixture();f.Orders[0].Status="Expired";
            Assert((await Reconcile(f)).Issues.Any(x=>x.Code=="OrderStatusMismatch"));
        });
        await Case("Legacy refund detail ID resolves only through the matching Payment FK", async () =>
        {
            var f=new Fixture(day);f.Order(1,100);f.Wallet(1,1,100);f.WalletRefund(2,1,1,20);f.Wallets.Last().SourceId="10";
            Assert((await Report(f)).NetRevenue==80);
            f.Payments.Last().OrderDetailId=null;
            await Throws<BusinessRuleException>(()=>Report(f));
        });
        await Case("Session wallet mirror Confirmation uses authoritative ledger time", async () =>
        {
            var f=new Fixture(day);f.Order(1,100);f.Wallet(1,1,100);f.Payments[0].PaymentTimeBasis="Confirmation";
            Assert((await Report(f)).NetRevenue==100);
            var t=(await Reconcile(f)).Transactions.Single();Assert(t.EntityType=="WalletTransaction" && t.PaymentTimeBasis=="Source");
        });
        if(failed.Count>0) throw new InvalidOperationException(string.Join("; ",failed));
        Console.WriteLine($"{count} revenue service checks passed (data fixtures; no SQL/API call).");
        return count;

        Fixture InvoiceFixture()
        {
            var f=new Fixture(day); f.Order(1,100); f.Orders[0].Details.First().UnitPrice=60;
            f.Orders[0].Details.Add(new OrderDetail {OrderDetailId=11,OrderId=1,ProductId=2,Quantity=1,UnitPrice=40,ProductNameSnapshot="Second"});
            f.Pay(1,1,100,"Cash"); f.Payments[0].InvoiceId=1;
            f.Movements.AddRange(new InventoryTransaction {InvTransId=1,OrderId=1,OrderDetailId=10,ProductId=1,Type="Sale",Quantity=-1,Before=10,After=9},
                new InventoryTransaction {InvTransId=2,OrderId=1,OrderDetailId=11,ProductId=2,Type="Sale",Quantity=-1,Before=10,After=9});
            f.Invoices.Add(new Invoice {InvoiceId=1,CustomerId=1,InvoiceDate=day.UtcDateTime,Subtotal=100,TotalAmount=100,Status="Paid",
                InvoiceOrders=[new InvoiceOrder {InvoiceId=1,OrderId=1}],Details=[
                    new InvoiceDetail {InvoiceDetailId=1,InvoiceId=1,OrderDetailId=10,Quantity=1,UnitPrice=60,Amount=60},
                    new InvoiceDetail {InvoiceDetailId=2,InvoiceId=1,OrderDetailId=11,Quantity=1,UnitPrice=40,Amount=40}]});
            return f;
        }
        void AddPartialRefund(Fixture f,bool returned)
        {
            f.Pay(2,1,40,"Cash",type:"Refund",original:1); f.Payments.Last().OrderDetailId=11; f.Payments.Last().InvoiceId=1;
            if(returned)
            {
                f.Orders[0].Details.Single(x=>x.OrderDetailId==11).Status="Cancelled";
                f.Invoices[0].Details.Single(x=>x.OrderDetailId==11).CancelledAmount=40;
                f.Movements.Add(new InventoryTransaction {InvTransId=3,OrderId=1,OrderDetailId=11,ProductId=2,Type="Return",Quantity=1,Before=9,After=10});
            }
        }
        async Task Case(string name,Func<Task> test) { try { await test(); count++; Console.WriteLine("PASS "+name); } catch(Exception ex) { failed.Add(name+": "+ex.Message); Console.WriteLine("FAIL "+name+": "+ex.Message); } }
        async Task<RevenueReportDto> Report(Fixture f,FakeQueries? repo=null) { using var p=Provider(repo??new(f.Data)); return await p.GetRequiredService<IRevenueService>().GetRevenueAsync(filter,1); }
        async Task<ReconciliationReportDto> Reconcile(Fixture f) { using var p=Provider(new(f.Data)); return await p.GetRequiredService<IRevenueService>().GetReconciliationAsync(filter,1); }
    }
    private static ServiceProvider Provider(FakeQueries repo) { var s=new ServiceCollection(); s.AddApplicationServices(); s.AddSingleton<IRevenueQueries>(repo); return s.BuildServiceProvider(); }
    private static void Assert(bool result) { if(!result) throw new InvalidOperationException("Assertion failed"); }
    private static async Task Throws<T>(Func<Task> run) where T:Exception { try { await run(); } catch(T) { return; } throw new InvalidOperationException("Expected "+typeof(T).Name); }
    private sealed class FakeQueries(RevenueReadSet data):IRevenueQueries
    {
        public Employee? Employee {get;set;}=new() {EmployeeId=1,IsActive=true,Role=new Role { RoleName="Manager",RolePermissions=[new RolePermission {Permission=new Permission {PermissionName="report.view"}}]}};
        public int ReadCalls; public DateTimeOffset From,To;
        public Task<Employee?> GetEmployeeAsync(int id,CancellationToken ct=default)=>Task.FromResult(Employee);
        public Task<RevenueReadSet> ReadAsync(DateTimeOffset from,DateTimeOffset to,CancellationToken ct=default) { ReadCalls++; From=from;To=to;return Task.FromResult(data); }
    }
    private sealed class Fixture(DateTimeOffset day)
    {
        public List<Payment> Payments=[]; public List<WalletTransaction> Wallets=[]; public List<CounterTopUpRequest> TopUps=[];
        public List<Order> Orders=[]; public List<SessionCharge> Charges=[];public List<InventoryTransaction> Movements=[];public List<PromotionUsage> Usages=[];public List<Invoice> Invoices=[];
        public RevenueReadSet Data=>new(Payments,Wallets,TopUps,Orders,Charges,Movements,Usages,Invoices);
        public void Order(int id,decimal net) => Orders.Add(new Order {OrderId=id,CustomerId=1,OrderTime=day.UtcDateTime,TotalAmount=net,Details=[new OrderDetail {OrderDetailId=id*10,OrderId=id,ProductId=id,Quantity=1,UnitPrice=net,ProductNameSnapshot="Old name"}]});
        public void Pay(int id,int order,decimal amount,string method,string type="Payment",string status="Succeeded",int? original=null,DateTimeOffset? at=null)
        {
            var time=at??day;
            Payments.Add(new Payment {PaymentId=id,OrderId=order,Method=method,Type=type,Status=status,Amount=amount,RefundOfPaymentId=original,CreatedAt=time,PaymentTime=status=="Succeeded"?time:null,ConfirmedAt=status=="Succeeded"?time:null,PaymentTimeBasis=status=="Succeeded"?"Source":null,ProviderOccurredAt=method=="ZaloPay"?time:null});
        }
        public void Wallet(int id,int order,decimal amount)
        {
            Wallets.Add(new WalletTransaction {TransactionId=id,CustomerId=1,Kind="Debit",Type="OrderPayment",SourceType="Order",SourceId=order.ToString(),Amount=-amount,CreatedAt=day.UtcDateTime});
            Pay(id,order,amount,"Wallet"); Payments.Last().WalletTransactionId=id;
        }
        public void WalletRefund(int id,int order,int original,decimal amount)
        {
            Wallets.Add(new WalletTransaction {TransactionId=id,CustomerId=1,Kind="Refund",Type="OrderRefund",SourceType="OrderDetail",SourceId=$"{order}:{order*10}",Amount=amount,OriginalDebitId=original,CreatedAt=day.UtcDateTime});
            Pay(id,order,amount,"Wallet",type:"Refund",original:original); Payments.Last().WalletTransactionId=id;Payments.Last().OrderDetailId=order*10;
        }
    }
}
