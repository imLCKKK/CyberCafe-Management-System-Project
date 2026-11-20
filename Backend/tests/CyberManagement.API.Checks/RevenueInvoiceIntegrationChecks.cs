using CyberManagement.Application.DTOs.Invoices;
using CyberManagement.Application.Interfaces.Repositories;
using CyberManagement.Application.Interfaces.Services;
using CyberManagement.Application.Wallets;
using CyberManagement.Domain.Entities;
using CyberManagement.Infrastructure.Persistance;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

// Kiểm tra giao điểm SCRUM-30/31/33 đã có; nguồn đơn là fixture, không thay checkout/worker.
internal static class RevenueInvoiceIntegrationChecks
{
    public static async Task<int> RunAsync(IServiceProvider services, RevenueSqlChecks.SeedData seed)
    {
        var checks=0; int orderId,detailId,productId,invoiceId; decimal net;
        var date=DateOnly.FromDateTime(seed.Day.DateTime);
        const string issueKey="Revenue:IntegratedInvoice";
        await using(var scope=services.CreateAsyncScope())
        {
            var db=Db(scope);
            orderId=await db.Payments.Where(x=>x.Type=="Payment" && x.Method=="Wallet" && x.OrderId!=null).Select(x=>x.OrderId!.Value).SingleAsync();
            var detail=await db.OrderDetails.SingleAsync(x=>x.OrderId==orderId);detailId=detail.OrderDetailId;productId=detail.ProductId;
            net=detail.Quantity*detail.UnitPrice-detail.DiscountAmount;
            var before=await db.Payments.CountAsync();
            var issued=await scope.ServiceProvider.GetRequiredService<IInvoiceService>().IssueAsync(
                new(){Kind="Orders",OrderIds=[orderId]},seed.Employees["Admin"],issueKey);
            invoiceId=issued.Id;
            Check(issued.PaidAmount==net && issued.GrossAmount==net && await db.Payments.CountAsync()==before,
                "Invoice service attaches the existing collection without collecting again");
        }
        await using(var scope=services.CreateAsyncScope())
        {
            var db=Db(scope); await using var tx=await db.Database.BeginTransactionAsync();
            await scope.ServiceProvider.GetRequiredService<IWalletLedgerWriter>().LockCustomerAsync(seed.CustomerId,default);
            var order=await scope.ServiceProvider.GetRequiredService<IVoucherRepository>().GetOrderForUpdateAsync(orderId)
                ?? throw new InvalidOperationException("Missing source fixture");
            order.Status="Cancelled";order.Details.Single().Status="Cancelled";await db.SaveChangesAsync();
            await scope.ServiceProvider.GetRequiredService<IInventoryService>().ApplyReturnAsync(detailId,productId,order.Details.Single().Quantity,
                seed.Employees["Admin"],"Revenue:IntegratedReturn");
            await scope.ServiceProvider.GetRequiredService<IInvoiceService>().RecordCancelledOrderLinesAsync(orderId,[detailId]);
            await tx.CommitAsync();
        }
        await using(var scope=services.CreateAsyncScope())
        {
            var report=await scope.ServiceProvider.GetRequiredService<IRevenueService>().GetReconciliationAsync(new(){From=date,To=date},seed.Employees["Admin"]);
            Check(report.Issues.Any(x=>x.Code=="InvoiceRefundUnresolved" && x.References.Any(r=>r.EntityType=="Invoice" && r.EntityId==invoiceId)
                && x.ActualValue==net),"Cancelled snapshot with no refund remains visibly unresolved");
        }
        await using(var scope=services.CreateAsyncScope())
        {
            var db=Db(scope);await using var tx=await db.Database.BeginTransactionAsync();
            var wallet=scope.ServiceProvider.GetRequiredService<IWalletLedgerWriter>();
            await wallet.LockCustomerAsync(seed.CustomerId,default);
            _=await scope.ServiceProvider.GetRequiredService<IVoucherRepository>().GetOrderForUpdateAsync(orderId);
            var original=await db.Payments.SingleAsync(x=>x.OrderId==orderId && x.Type=="Payment");
            var occurred=seed.Day.AddHours(1);
            // SCRUM-31 hiện bàn giao nguồn hoàn dạng ID dòng; Payment FK chứng minh đủ đơn/dòng/debit.
            var refund=await wallet.RecordAsync(new WalletMutation(seed.CustomerId,WalletMutationKind.Refund,net,"OrderRefund","OrderDetail",
                detailId.ToString(),"Revenue:IntegratedRefund",new WalletActor("Staff",seed.Employees["Admin"]),occurred,
                OriginalDebitId:original.WalletTransactionId),default);
            db.Payments.Add(new Payment {OrderId=orderId,OrderDetailId=detailId,RefundOfPaymentId=original.PaymentId,WalletTransaction=refund.Entry,
                Type="Refund",Method="Wallet",Status="Succeeded",Amount=net,OperationKey="Revenue:IntegratedRefund",Reason="Cancelled fixture line",
                CreatedAt=occurred,PaymentTime=occurred,ConfirmedAt=occurred,PaymentTimeBasis="Source"});
            await db.SaveChangesAsync();
            var updated=await scope.ServiceProvider.GetRequiredService<IInvoiceService>().ApplyFinancialChangeAsync(orderId,null);
            Check(updated is not null && updated.CancelledAmount==net && updated.RefundAmount==net && updated.PaidAmount==0,
                "Invoice hook preserves cancellation snapshot and reconciles actual wallet refund");
            await tx.CommitAsync();
        }
        await using(var scope=services.CreateAsyncScope())
        {
            var db=Db(scope);var revenue=scope.ServiceProvider.GetRequiredService<IRevenueService>();
            var report=await revenue.GetRevenueAsync(new(){From=date,To=date},seed.Employees["Admin"]);
            Check(report.CollectedAmount==440 && report.RefundedAmount==50+net && report.NetRevenue==390-net && report.TopUpCollectedAmount==50000,
                "Revenue matches service-issued invoice and original/refund ledger exactly once");
            var reconciled=await revenue.GetReconciliationAsync(new(){From=date,To=date},seed.Employees["Admin"]);
            Check(!reconciled.Issues.Any(x=>x.References.Any(r=>r.EntityType=="Invoice" && r.EntityId==invoiceId)),
                "Cancellation/refund snapshot reconciles after financial hook");
            Check(await db.Products.Where(x=>x.ProductId==productId).Select(x=>x.Stock).SingleAsync()==8,
                "Inventory service restores exactly one unit");
        }
        await using(var scope=services.CreateAsyncScope())
        {
            var db=Db(scope);await using var tx=await db.Database.BeginTransactionAsync();
            var invoices=scope.ServiceProvider.GetRequiredService<IInvoiceService>();
            await invoices.RecordCancelledOrderLinesAsync(orderId,[detailId]);
            await invoices.ApplyFinancialChangeAsync(orderId,null);await tx.CommitAsync();
            var replay=await invoices.IssueAsync(new(){Kind="Orders",OrderIds=[orderId]},seed.Employees["Admin"],issueKey);
            Check(replay.Id==invoiceId && replay.CancelledAmount==net && replay.RefundAmount==net
                && await db.Payments.CountAsync(x=>x.OrderId==orderId && x.Type=="Refund")==1,
                "Replay cancellation/issue keeps one invoice, one refund and fixed snapshot");
        }
        return checks;
        void Check(bool result,string name) {if(!result)throw new InvalidOperationException(name);checks++;Console.WriteLine("PASS "+name);}
    }
    private static ApplicationDbContext Db(AsyncServiceScope scope)=>scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
}
