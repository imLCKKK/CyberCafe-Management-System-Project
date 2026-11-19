using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using CyberManagement.API;
using CyberManagement.API.Controller;
using CyberManagement.API.Realtime;
using CyberManagement.Application;
using CyberManagement.Application.DTOs.Identity;
using CyberManagement.Application.DTOs.Invoices;
using CyberManagement.Application.Exceptions;
using CyberManagement.Application.Interfaces.Repositories;
using CyberManagement.Application.Interfaces.Services;
using CyberManagement.Application.Security;
using CyberManagement.Application.Wallets;
using CyberManagement.Domain.Entities;
using CyberManagement.Infrastructure;
using CyberManagement.Infrastructure.Persistance;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

internal static class InvoiceChecks
{
    public const string Password = "Invoice-check-only-2026!";
    public static async Task<int> RunAsync(string? connection, bool serve = false)
    {
        if (string.IsNullOrWhiteSpace(connection)) throw new ArgumentException("AUTH_CHECKS_SQL_CONNECTION is required for invoice SQL checks.");
        var fixture = new AuthCheckDatabase(connection);
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = "Development", ApplicationName = typeof(CyberManagement.API.Program).Assembly.FullName
        });
        if (serve) builder.WebHost.UseUrls("http://127.0.0.1:0"); else builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string,string?>
        {
            ["Authentication:Jwt:SigningKey"] = "invoice-check-only-signing-key-at-least-32-bytes",
            ["ConnectionStrings:DefaultConnection"] = connection
        });
        builder.Services.AddInfrastructureServices(builder.Configuration);
        var readGate = new InvoiceReadGate();
        builder.Services.AddDbContext<ApplicationDbContext>(options => { fixture.Configure(options); options.AddInterceptors(readGate); });
        builder.Services.AddApplicationServices(); builder.Services.AddApiServices(builder.Configuration);
        builder.Services.AddControllers().AddApplicationPart(typeof(AuthController).Assembly);
        await using var app = builder.Build();
        var checks = 0;
        int customerA=0, customerB=0, employeeId=0, productId=0;
        try
        {
            await using(var scope=app.Services.CreateAsyncScope())
            {
                var db=Db(scope); await fixture.InitializeAsync(db);
                await scope.ServiceProvider.GetRequiredService<IIdentitySeeder>().SeedAsync(new IdentitySeedOptions(true,
                    RolePermissionCatalog.Roles.ToDictionary(x=>x.Name,_=>(string?)Password)),default);
                var customers = new[] { new Customer { FullName="Invoice A", Phone="0900031001",WalletBalance=1000000 },
                    new Customer { FullName="Invoice B", Phone="0900031002",WalletBalance=0 } };
                foreach(var customer in customers) customer.PasswordHash=scope.ServiceProvider.GetRequiredService<ICustomerPasswordHasher>().HashPassword(customer,Password);
                db.Customers.AddRange(customers);
                var product=new Product {ProductName="Current food", Price=100000,Stock=20,IsActive=true,
                    Category=new Category {CategoryName="Invoice checks"}};
                db.Products.Add(product); await db.SaveChangesAsync();
                customerA=customers[0].CustomerId; customerB=customers[1].CustomerId; productId=product.ProductId;
                employeeId=await db.Employees.Where(x=>x.Username=="admin").Select(x=>x.EmployeeId).SingleAsync();
            }
            app.UseExceptionHandler(); app.UseAuthentication(); app.UseAuthorization(); app.MapControllers(); app.MapHub<RealtimeHub>(RealtimeHub.Endpoint);
            if(serve)
            {
                var order = await NewOrder(customerA,100000,10000);
                await AddCash(order,90000,true);
                app.MapGet("/__checks/invoices/source",()=>new {orderId=order,customerId=customerA,productId}).RequireAuthorization("permission:invoice.view");
                app.MapPost("/__checks/invoices/confirmation-refund", () => SeedConfirmationRefundAsync())
                    .RequireAuthorization("permission:invoice.create");
                app.MapPost("/__checks/invoices/change-product",async ()=>
                {
                    await using var scope=app.Services.CreateAsyncScope(); var db=Db(scope);
                    var product=await db.Products.SingleAsync(x=>x.ProductId==productId);
                    product.ProductName="Changed after issue"; product.Price=999999; await db.SaveChangesAsync();
                    return new {product.ProductId,product.Price};
                }).RequireAuthorization("permission:invoice.create");
                await app.StartAsync();
                Console.WriteLine("INVOICE_HOST_READY "+app.Urls.Single());
                _ = Task.Run(async () =>
                {
                    while (await Console.In.ReadLineAsync() is { } line)
                        if (line.Trim() == "stop") break;
                    app.Lifetime.StopApplication();
                });
                await app.WaitForShutdownAsync(); return 0;
            }
            await app.StartAsync(); using var client=app.GetTestClient(); client.BaseAddress=new Uri("https://localhost");
            var tokens=new Dictionary<string,string>();
            foreach(var user in new[]{"admin","manager01","cashier01","waiter01","tech01"})
            {
                using var login=await client.PostAsJsonAsync("/api/auth/login",new {username=user,password=Password});
                await Status("Login "+user,HttpStatusCode.OK,login);
                using var json=JsonDocument.Parse(await login.Content.ReadAsStringAsync()); tokens[user]=json.RootElement.GetProperty("accessToken").GetString()!;
            }
            var customerTokens=new List<string>();
            foreach(var phone in new[]{"0900031001","0900031002"})
            {
                using var login=await client.PostAsJsonAsync("/api/auth/customer/login",new {phone,password=Password});
                await Status("Customer login "+phone,HttpStatusCode.OK,login);
                using var json=JsonDocument.Parse(await login.Content.ReadAsStringAsync());customerTokens.Add(json.RootElement.GetProperty("accessToken").GetString()!);
            }
            var admin=tokens["admin"]; const string route="/api/admin/invoices";
            var first=await NewOrder(customerA,100000,10000); var second=await NewOrder(customerA,50000);
            await AddCash(first,90000,true);
            var request=Orders(first,second);
            await ExpectHttp("Anonymous read",HttpStatusCode.Unauthorized,HttpMethod.Get,route,null);
            await ExpectHttp("Anonymous issue",HttpStatusCode.Unauthorized,HttpMethod.Post,route,null,request,"anonymous");
            foreach(var (user,token) in tokens)
                await ExpectHttp(user+" invoice.view",user is "waiter01" or "tech01"?HttpStatusCode.Forbidden:HttpStatusCode.OK,HttpMethod.Get,route,token);
            await ExpectHttp("Customer cannot view admin invoices",HttpStatusCode.Forbidden,HttpMethod.Get,route,customerTokens[0]);
            foreach(var user in new[]{"waiter01","tech01"})
                await ExpectHttp(user+" cannot issue",HttpStatusCode.Forbidden,HttpMethod.Post,route,tokens[user],request,"denied:"+user);
            await ExpectHttp("Customer cannot issue",HttpStatusCode.Forbidden,HttpMethod.Post,route,customerTokens[0],request,"customer");
            foreach(var key in new[]{"SessionAutoInvoice:1","sessionautoclose:1"," bad ","contains.dot"})
                await ExpectHttp("Reserved/invalid key "+key,HttpStatusCode.BadRequest,HttpMethod.Post,route,admin,request,key);
            await ExpectHttp("Missing key",HttpStatusCode.BadRequest,HttpMethod.Post,route,admin,request);
            await ExpectHttp("Cannot spoof System/money",HttpStatusCode.BadRequest,HttpMethod.Post,route,admin,
                new {kind="Orders",orderIds=new[]{first},actorType="System",paidAmount=999},"spoof");
            await ExpectHttp("Reject mixed new source",HttpStatusCode.BadRequest,HttpMethod.Post,route,admin,
                new {kind="Session",orderIds=new[]{first},gamingSessionId=(int?)null},"mixed");
            await ExpectHttp("Missing source",HttpStatusCode.NotFound,HttpMethod.Post,route,admin,Orders(int.MaxValue),"missing");
            var invoice=await Issue(request,"issue:original");
            Check("Grouped food snapshot and unpaid balance",invoice.Subtotal==150000 && invoice.Discount==10000
                && invoice.GrossAmount==140000 && invoice.PaidAmount==90000 && invoice.AmountDue==50000 && invoice.Status=="Pending");
            Check("Staff actor persisted",invoice.ActorType=="Staff" && invoice.EmployeeId==employeeId && invoice.OrderIds.SequenceEqual(new[]{first,second}));
            var repeated=await Issue(Orders(second,first,first),"issue:original");
            Check("Normalized source retry returns same invoice",repeated.Id==invoice.Id);
            await ExpectHttp("Same key changed source",HttpStatusCode.Conflict,HttpMethod.Post,route,admin,Orders(first),"issue:original");
            await ExpectHttp("Same source new key",HttpStatusCode.Conflict,HttpMethod.Post,route,admin,request,"issue:newkey");
            await ExpectHttp("Changed employee changes request hash",HttpStatusCode.Conflict,HttpMethod.Post,route,tokens["manager01"],request,"issue:original");
            await ExpectHttp("Own customer detail",HttpStatusCode.OK,HttpMethod.Get,$"/api/client/invoices/{invoice.Id}",customerTokens[0]);
            await ExpectHttp("Other customer gets 404",HttpStatusCode.NotFound,HttpMethod.Get,$"/api/client/invoices/{invoice.Id}",customerTokens[1]);
            await ExpectHttp("Other customer cannot print",HttpStatusCode.NotFound,HttpMethod.Get,$"/api/client/invoices/{invoice.Id}/print",customerTokens[1]);
            using(var own=await Send(HttpMethod.Get,"/api/client/invoices",customerTokens[1]))
            {await Status("Own list",HttpStatusCode.OK,own);Check("Ownership filtered in SQL",(await own.Content.ReadFromJsonAsync<InvoiceDto[]>())!.Length==0);}
            using(var print=await Send(HttpMethod.Get,$"{route}/{invoice.Id}/print",admin))
            {await Status("Print",HttpStatusCode.OK,print);Check("Print has restrictive CSP",print.Headers.Contains("Content-Security-Policy"));}
            await using(var scope=app.Services.CreateAsyncScope())
            {
                var db=Db(scope); var product=await db.Products.SingleAsync(x=>x.ProductId==productId);
                product.ProductName="Changed after issue"; product.Price=999999; await db.SaveChangesAsync();
                Check("Issuing only linked existing money",await db.Payments.CountAsync()==1 && !await db.WalletTransactions.AnyAsync());
                Check("Exactly one key and invoice persisted",await db.InvoiceIssueRequests.CountAsync()==1 && await db.Invoices.CountAsync()==1);
            }
            var afterEdit=await Read(invoice.Id);
            Check("Later product edits do not change snapshot",afterEdit.GrossAmount==invoice.GrossAmount && afterEdit.Lines[0].Description=="Food at order time"
                && afterEdit.Lines[0].UnitPrice==100000 && afterEdit.Lines[0].VoucherCode=="SALE10");
            using(var print=await Send(HttpMethod.Get,$"{route}/{invoice.Id}/print",admin))
            {var html=await print.Content.ReadAsStringAsync();Check("Print uses stored price/name",html.Contains("Food at order time")&&!html.Contains("Changed after issue")&&!html.Contains("999.999"));}

            var zero=await NewOrder(customerA,10000,10000);
            var zeroInvoice=await Issue(Orders(zero),"zero");
            Check("Zero invoice has durable key and no charge",zeroInvoice.GrossAmount==0 && zeroInvoice.PaidAmount==0 && zeroInvoice.Status=="Paid");
            Check("Zero replay returns same invoice",(await Issue(Orders(zero),"zero")).Id==zeroInvoice.Id);
            var walletOrder=await NewOrder(customerA,20000);await AddWallet(walletOrder,20000);
            var walletInvoice=await Issue(Orders(walletOrder),"wallet");
            Check("Wallet counted once",walletInvoice.PaidAmount==20000 && walletInvoice.AmountDue==0);
            await using(var scope=app.Services.CreateAsyncScope()) Check("Issue never debits wallet again",await Db(scope).WalletTransactions.CountAsync()==1);

            var rollbackOrder=await NewOrder(customerA,10000);await AddCash(rollbackOrder,10000,true);
            await Throws<Rollback>("Outer transaction rollback",async ()=>
            {
                await using var scope=app.Services.CreateAsyncScope();
                await Uow(scope).ExecuteAsync<int>(async ()=>{await Service(scope).IssueAsync(Orders(rollbackOrder),employeeId,"rollback");throw new Rollback();});
            });
            await using(var scope=app.Services.CreateAsyncScope()) Check("Rollback removes invoice/key/payment link",!await Db(scope).InvoiceIssueRequests.AnyAsync(x=>x.OperationKey=="rollback")
                && (await Db(scope).Payments.SingleAsync(x=>x.OrderId==rollbackOrder)).InvoiceId is null);
            await Issue(Orders(rollbackOrder),"rollback");
            var concurrentOrder=await NewOrder(customerA,1000);
            var results=await Task.WhenAll(Issue(Orders(concurrentOrder),"parallel"),Issue(Orders(concurrentOrder),"parallel"));
            Check("Concurrent same key returns one ID",results[0].Id==results[1].Id);
            var collisionA=await NewOrder(customerA,1000);var collisionB=await NewOrder(customerB,1000);
            using(var gate=new SemaphoreSlim(0))
            {
                var tasks=new[]{collisionA,collisionB}.Select(async id=>{await gate.WaitAsync();using var response=await Send(HttpMethod.Post,route,admin,Orders(id),"collision");return response.StatusCode;}).ToArray();
                gate.Release(2);var statuses=await Task.WhenAll(tasks);
                Check("Concurrent key collision has one success and one 409",statuses.Count(x=>x==HttpStatusCode.Created)==1&&statuses.Count(x=>x==HttpStatusCode.Conflict)==1);
            }
            await using(var scope=app.Services.CreateAsyncScope()) Check("No orphan invoice after key collision",await Db(scope).InvoiceIssueRequests.CountAsync()==await Db(scope).Invoices.CountAsync());

            var lateOrder=await NewOrder(customerA,100000);var lateInvoice=await Issue(Orders(lateOrder),"late");
            var latePayment=await AddCash(lateOrder,100000,true);
            await Financial(lateOrder);
            var late=await Read(lateInvoice.Id);
            Check("Late confirmed payment links source and updates status",late.PaidAmount==100000&&late.Status=="Paid");
            int refundId;
            await using(var scope=app.Services.CreateAsyncScope())
            {
                var db=Db(scope);var payment=await db.Payments.SingleAsync(x=>x.PaymentId==latePayment);
                Check("Late payment has right invoice",payment.InvoiceId==lateInvoice.Id);
                var refund=new Payment {OrderId=lateOrder,OrderDetailId=await db.OrderDetails.Where(x=>x.OrderId==lateOrder).Select(x=>x.OrderDetailId).SingleAsync(),
                    RefundOfPaymentId=latePayment,Type="Refund",Amount=20000,Method="Cash",Status="Pending",Reason="Verified partial cancellation",OperationKey="refund:late"};
                db.Payments.Add(refund);await db.SaveChangesAsync();refundId=refund.PaymentId;
            }
            await Financial(lateOrder);
            Check("Pending refund does not reduce collected amount",(await Read(lateInvoice.Id)).PaidAmount==100000);
            await using(var scope=app.Services.CreateAsyncScope())
            {
                var refund=await Db(scope).Payments.SingleAsync(x=>x.PaymentId==refundId);Success(refund);await Db(scope).SaveChangesAsync();
            }
            await Financial(lateOrder);
            var refunded=await Read(lateInvoice.Id);
            Check("Succeeded refund deducted once",refunded.PaidAmount==80000&&refunded.RefundAmount==20000);
            await Financial(lateOrder);Check("Repeated financial sync cannot refund twice",(await Read(lateInvoice.Id)).PaidAmount==80000);
            await using(var scope=app.Services.CreateAsyncScope())
            {
                var cancelledLines=await Db(scope).OrderDetails.Where(x=>x.OrderId==lateOrder).ToListAsync();
                foreach(var line in cancelledLines)line.Status="Cancelled";
                await Db(scope).SaveChangesAsync();
                var detailIds=cancelledLines.Select(x=>x.OrderDetailId).ToArray();
                await Uow(scope).ExecuteAsync(async ()=>{await Service(scope).RecordCancelledOrderLinesAsync(lateOrder,detailIds);return true;});
                await Uow(scope).ExecuteAsync(async ()=>{await Service(scope).RecordCancelledOrderLinesAsync(lateOrder,detailIds);return true;});
            }
            var cancelled=await Read(lateInvoice.Id);
            Check("Cancellation snapshot idempotent; unpaid refund not hidden",cancelled.CancelledAmount==100000&&cancelled.GrossAmount==100000
                &&cancelled.PaidAmount==80000&&cancelled.OverpaidAmount==80000&&cancelled.Status=="Cancelled");
            await using(var scope=app.Services.CreateAsyncScope())
                await Throws<InvalidOperationException>("System entry requires shared transaction",()=>scope.ServiceProvider.GetRequiredService<ISessionInvoiceIssuer>().IssueForAutoClosedSessionAsync(1));
            var session=await NewSession(customerB,5000);
            InvoiceDto? systemInvoice;
            await using(var scope=app.Services.CreateAsyncScope())
                systemInvoice=await Uow(scope).ExecuteAsync(()=>scope.ServiceProvider.GetRequiredService<ISessionInvoiceIssuer>().IssueForAutoClosedSessionAsync(session));
            Check("System session invoice has no fake employee",systemInvoice is {ActorType:"System",EmployeeId:null,PaidAmount:5000}&&systemInvoice.OrderIds.Count==0);
            await using(var scope=app.Services.CreateAsyncScope())
            {
                var audits=await Db(scope).AuditLogs.AsNoTracking().Where(x=>x.EntityType=="Invoice"&&x.EntityId==systemInvoice!.Id).ToListAsync();
                Check("System audit stores reason, session and operation key",audits.Count==1 && audits[0].EmployeeId is null
                    && audits[0].Action.Contains($"session={session};",StringComparison.Ordinal)
                    && audits[0].Action.Contains("reason=InsufficientBalance;",StringComparison.Ordinal)
                    && audits[0].Action.Contains($"key=SessionAutoInvoice:{session}",StringComparison.Ordinal)
                    && audits[0].Action.Length<=255);
                var replay=await Uow(scope).ExecuteAsync(()=>scope.ServiceProvider.GetRequiredService<ISessionInvoiceIssuer>().IssueForAutoClosedSessionAsync(session));
                Check("Worker retry returns same invoice",replay!.Id==systemInvoice!.Id);
                Check("Worker retry does not duplicate audit",await Db(scope).AuditLogs.CountAsync(x=>x.EntityType=="Invoice"&&x.EntityId==systemInvoice!.Id)==audits.Count);
            }
            var gatewayInvoice=await SeedConfirmationRefundAsync();
            using(var response=await Send(HttpMethod.Get,$"{route}/{gatewayInvoice.Id}",admin))
            {
                await Status("Confirmation refund invoice detail",HttpStatusCode.OK,response);
                using var document=JsonDocument.Parse(await response.Content.ReadAsStringAsync());
                var refund=document.RootElement.GetProperty("payments").EnumerateArray().Single(x=>x.GetProperty("type").GetString()=="Refund");
                Check("Refund DTO includes all timestamp metadata",new[]{"paymentTime","confirmedAt","providerOccurredAt","paymentTimeBasis"}.All(name=>refund.TryGetProperty(name,out _)));
                Check("Confirmation refund preserves its basis and nullable provider time",refund.GetProperty("paymentTimeBasis").GetString()=="Confirmation"
                    &&refund.GetProperty("providerOccurredAt").ValueKind==JsonValueKind.Null
                    &&refund.GetProperty("paymentTime").GetDateTimeOffset()==refund.GetProperty("confirmedAt").GetDateTimeOffset()
                    &&refund.GetProperty("paidAt").GetDateTimeOffset()==refund.GetProperty("paymentTime").GetDateTimeOffset());
                Check("Timestamp metadata does not change collected/refunded money",gatewayInvoice.PaidAmount==0&&gatewayInvoice.RefundAmount==90000
                    &&gatewayInvoice.CancelledAmount==90000&&gatewayInvoice.AmountDue==0);
                var original=document.RootElement.GetProperty("payments").EnumerateArray().Single(x=>x.GetProperty("type").GetString()=="Payment");
                Check("Source payment keeps verified provider time",original.GetProperty("paymentTimeBasis").GetString()=="Source"
                    &&original.GetProperty("providerOccurredAt").GetDateTimeOffset()==original.GetProperty("paymentTime").GetDateTimeOffset());
            }
            using(var print=await Send(HttpMethod.Get,$"{route}/{gatewayInvoice.Id}/print",admin))
            {
                await Status("Confirmation refund print",HttpStatusCode.OK,print);
                var html=WebUtility.HtmlDecode(await print.Content.ReadAsStringAsync());
                Check("Print distinguishes confirmation from provider transaction time",html.Contains("Ghi nhận theo thời điểm hệ thống xác nhận")
                    &&html.Contains("Chưa có thời điểm giao dịch từ nhà cung cấp")&&html.Contains("17/11/2026 00:03:00 UTC+07:00"));
            }
            var emptySession=await NewSession(customerB,0);
            await using(var scope=app.Services.CreateAsyncScope())
                Check("Zero session does not invent invoice",await Uow(scope).ExecuteAsync(()=>scope.ServiceProvider.GetRequiredService<ISessionInvoiceIssuer>().IssueForAutoClosedSessionAsync(emptySession)) is null);
            var preCancelled=await NewOrder(customerA,1000);
            await using(var scope=app.Services.CreateAsyncScope())
            {var line=await Db(scope).OrderDetails.SingleAsync(x=>x.OrderId==preCancelled);line.Status="Cancelled";await Db(scope).SaveChangesAsync();}
            var preInvoice=await Issue(Orders(preCancelled),"pre-cancelled");
            Check("Cancellation before issue is snapshotted without debt",preInvoice.GrossAmount==1000&&preInvoice.CancelledAmount==1000&&preInvoice.AmountDue==0&&preInvoice.Status=="Cancelled");
            await using(var scope=app.Services.CreateAsyncScope())
            {
                var ids=await Db(scope).OrderDetails.Where(x=>x.OrderId==walletOrder).Select(x=>x.OrderDetailId).ToArrayAsync();
                await Throws<BusinessRuleException>("Cannot invent source cancellation",()=>Uow(scope).ExecuteAsync(async ()=>{await Service(scope).RecordCancelledOrderLinesAsync(walletOrder,ids);return true;}));
            }
            var freeCancelled=await NewOrder(customerA,1000,1000);
            await using(var scope=app.Services.CreateAsyncScope())
            {var line=await Db(scope).OrderDetails.SingleAsync(x=>x.OrderId==freeCancelled);line.Status="Cancelled";await Db(scope).SaveChangesAsync();}
            var freeCancelledInvoice=await Issue(Orders(freeCancelled),"free-cancelled");
            Check("Cancelled zero-value order is not labelled Paid",freeCancelledInvoice.Status=="Cancelled"&&freeCancelledInvoice.AmountDue==0);
            var corruptOrder=await NewOrder(customerA,500);
            int mismatchedPayment;
            await using(var scope=app.Services.CreateAsyncScope())
            {
                var payment=new Payment{OrderId=corruptOrder,InvoiceId=invoice.Id,Amount=500,Method="Cash",OperationKey="wrong-invoice-source"};
                Success(payment);Db(scope).Payments.Add(payment);await Db(scope).SaveChangesAsync();mismatchedPayment=payment.PaymentId;
            }
            await ExpectHttp("Reject money linked from another source",HttpStatusCode.Conflict,HttpMethod.Get,$"{route}/{invoice.Id}",admin);
            await using(var scope=app.Services.CreateAsyncScope())
            {var payment=await Db(scope).Payments.SingleAsync(x=>x.PaymentId==mismatchedPayment);payment.InvoiceId=null;await Db(scope).SaveChangesAsync();}
            var badSnapshot=await NewOrder(customerA,1000);
            await using(var scope=app.Services.CreateAsyncScope())
            {var order=await Db(scope).Orders.SingleAsync(x=>x.OrderId==badSnapshot);order.TotalAmount=1001;await Db(scope).SaveChangesAsync();}
            await ExpectHttp("Reject inconsistent source snapshot",HttpStatusCode.Conflict,HttpMethod.Post,route,admin,Orders(badSnapshot),"inconsistent");
            var xssOrder=await NewOrder(customerA,1000);
            await using(var scope=app.Services.CreateAsyncScope())
            {var detail=await Db(scope).OrderDetails.SingleAsync(x=>x.OrderId==xssOrder);detail.ProductNameSnapshot="<script>alert(1)</script>";await Db(scope).SaveChangesAsync();}
            var xssInvoice=await Issue(Orders(xssOrder),"xss");
            using(var print=await Send(HttpMethod.Get,$"{route}/{xssInvoice.Id}/print",admin))
            {var html=await print.Content.ReadAsStringAsync();Check("Print escapes source HTML",!html.Contains("<script>")&&html.Contains("&lt;script&gt;"));}
            var sameSource=await NewOrder(customerA,1000);
            var sourceRaces=await Task.WhenAll(new[]{"source-key-a","source-key-b"}.Select(async key=>
            {using var response=await Send(HttpMethod.Post,route,admin,Orders(sameSource),key);return response.StatusCode;}));
            Check("Different keys cannot issue the same source twice",sourceRaces.Count(x=>x==HttpStatusCode.Created)==1&&sourceRaces.Count(x=>x==HttpStatusCode.Conflict)==1);
            await using(var scope=app.Services.CreateAsyncScope())
            {
                var db=Db(scope);var original=await db.Payments.SingleAsync(x=>x.OrderId==walletOrder&&x.Type=="Payment");
                var detailId=await db.OrderDetails.Where(x=>x.OrderId==walletOrder).Select(x=>x.OrderDetailId).SingleAsync();
                await Uow(scope).ExecuteAsync(async ()=>
                {
                    var ledger=await scope.ServiceProvider.GetRequiredService<IWalletLedgerWriter>().RecordAsync(new WalletMutation(customerA,WalletMutationKind.Refund,5000,
                        "OrderRefund","OrderDetail",detailId.ToString(),"wallet-refund",new WalletActor("Staff",employeeId),DateTimeOffset.UtcNow,original.WalletTransactionId),default);
                    await Uow(scope).SaveChangesAsync();
                    var refund=new Payment{OrderId=walletOrder,OrderDetailId=detailId,RefundOfPaymentId=original.PaymentId,WalletTransactionId=ledger.Entry.TransactionId,
                        Type="Refund",Method="Wallet",Amount=5000,OperationKey="wallet-refund-payment",Reason="Verified cancellation"};
                    Success(refund);db.Payments.Add(refund);await Uow(scope).SaveChangesAsync();
                    return await Service(scope).ApplyFinancialChangeAsync(walletOrder,null);
                });
            }
            var walletRefund=await Read(walletInvoice.Id);
            Check("Wallet refund uses ledger once",walletRefund.PaidAmount==15000&&walletRefund.RefundAmount==5000);
            var staffSession=await NewSession(customerA,5000);
            await using(var scope=app.Services.CreateAsyncScope())
            {var customer=await Db(scope).Customers.SingleAsync(x=>x.CustomerId==customerA);customer.WalletBalance=1;await Db(scope).SaveChangesAsync();}
            await using(var scope=app.Services.CreateAsyncScope())
                await Throws<BusinessRuleException>("System requires verified zero balance",()=>Uow(scope).ExecuteAsync(()=>scope.ServiceProvider.GetRequiredService<ISessionInvoiceIssuer>().IssueForAutoClosedSessionAsync(staffSession)));
            var staffInvoice=await Issue(new IssueInvoiceRequest{Kind="Session",OrderIds=[],GamingSessionId=staffSession},"staff-session");
            Check("Staff issues session-only confirmed charges",staffInvoice.ActorType=="Staff"&&staffInvoice.OrderIds.Count==0&&staffInvoice.PaidAmount==5000);

            checks += await InvoiceReadConsistencyChecks.RunAsync(app.Services, readGate, employeeId, customerA, productId);

            await using(var scope=app.Services.CreateAsyncScope())
            {
                var db=Db(scope);var employee=await db.Employees.SingleAsync(x=>x.EmployeeId==employeeId);employee.IsActive=false;await db.SaveChangesAsync();
            }
            await ExpectHttp("Current disabled employee denied despite JWT",HttpStatusCode.Forbidden,HttpMethod.Get,route,admin);
            await ExpectHttp("Replay still checks current permission",HttpStatusCode.Forbidden,HttpMethod.Post,route,admin,request,"issue:original");
            await using(var scope=app.Services.CreateAsyncScope())
            {
                var db=Db(scope);var migrationCount=(await db.Database.GetAppliedMigrationsAsync()).Count();
                await Throws<SqlException>("Unsafe downgrade refused",()=>db.GetService<IMigrator>().MigrateAsync("20261114071953_AddInventoryMovementOperationKey"));
                Check("Refused downgrade preserves metadata/history",(await db.Database.GetAppliedMigrationsAsync()).Count()==migrationCount
                    &&await db.Invoices.AnyAsync(x=>x.ActorType=="System"));
            }
            var upgradeFixture=new AuthCheckDatabase(connection);
            try
            {
                var options=new DbContextOptionsBuilder<ApplicationDbContext>();upgradeFixture.Configure(options);
                await using var oldDb=new ApplicationDbContext(options.Options);
                await upgradeFixture.InitializeAsync(oldDb);
                await oldDb.GetService<IMigrator>().MigrateAsync("20261114071953_AddInventoryMovementOperationKey");
                var oldCustomer=new Customer{FullName="Historical invoice",Phone="0900319999"};
                var oldEmployee=new Employee{Username="historical-invoice",FullName="Historical employee",PasswordHash="test-only-no-login",Role=new Role{RoleName="HistoricalRole"}};
                oldDb.AddRange(oldCustomer,oldEmployee);await oldDb.SaveChangesAsync();
                await oldDb.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO dbo.Invoices (CustomerId, EmployeeId, InvoiceDate, Subtotal, Discount, TotalAmount, Status) VALUES ({oldCustomer.CustomerId}, {oldEmployee.EmployeeId}, {DateTime.UtcNow}, {1000m}, {0m}, {1000m}, N'Unpaid')");
                await oldDb.Database.MigrateAsync();
                var historical=await oldDb.Invoices.AsNoTracking().SingleAsync();
                Check("Upgrade preserves historical money and employee",historical.TotalAmount==1000&&historical.EmployeeId==oldEmployee.EmployeeId&&historical.ActorType=="Staff");
                Check("Model and migration are synchronized",!oldDb.Database.HasPendingModelChanges());
            }
            finally{await upgradeFixture.CleanupAsync();}
            return checks;

            IssueInvoiceRequest Orders(params int[] ids)=>new(){Kind="Orders",OrderIds=ids.ToList()};
            async Task<InvoiceDto> Issue(IssueInvoiceRequest body,string key)
            {using var response=await Send(HttpMethod.Post,route,admin,body,key);await Status("Issue "+key,HttpStatusCode.Created,response);var value=(await response.Content.ReadFromJsonAsync<InvoiceDto>())!;Check("Created Location",response.Headers.Location?.OriginalString.EndsWith("/api/admin/invoices/"+value.Id)==true);return value;}
            async Task<InvoiceDto> Read(int id)
            {using var response=await Send(HttpMethod.Get,$"{route}/{id}",admin);await Status("Read "+id,HttpStatusCode.OK,response);return (await response.Content.ReadFromJsonAsync<InvoiceDto>())!;}
            async Task<HttpResponseMessage> Send(HttpMethod method,string path,string? token,object? body=null,string? key=null)
            {using var request=new HttpRequestMessage(method,path);if(token!=null)request.Headers.Authorization=new AuthenticationHeaderValue("Bearer",token);if(key!=null)request.Headers.Add("Idempotency-Key",key);if(body!=null)request.Content=JsonContent.Create(body);return await client.SendAsync(request);}
            async Task ExpectHttp(string name,HttpStatusCode expected,HttpMethod method,string path,string? token,object? body=null,string? key=null)
            {using var response=await Send(method,path,token,body,key);await Status(name,expected,response);}
            async Task Status(string name,HttpStatusCode expected,HttpResponseMessage response)
            {if(response.StatusCode!=expected)throw new Exception($"{name}: expected {expected}, got {response.StatusCode}: {await response.Content.ReadAsStringAsync()}");Check(name,true);}
        }
        finally{await app.StopAsync();await fixture.CleanupAsync();}

        // Fixture SQL: mô phỏng khoản thu/hoàn đã được xác minh, không gọi provider.
        async Task<InvoiceDto> SeedConfirmationRefundAsync()
        {
            var orderId=await NewOrder(customerA,100000,10000);
            var suffix=Guid.NewGuid().ToString("N")[..8];
            await using var scope=app.Services.CreateAsyncScope();var db=Db(scope);
            var sourceAt=DateTimeOffset.Parse("2026-11-16T16:55:00Z");
            var confirmedAt=DateTimeOffset.Parse("2026-11-16T17:03:00Z");
            var original=new Payment{OrderId=orderId,Amount=90000,Method="ZaloPay",Status="Succeeded",
                OperationKey="gateway-time-charge:"+suffix,MerchantTransactionId="261116_invoice_"+suffix,
                GatewayTransactionId="fixture-charge-"+suffix,PaymentTime=sourceAt,ProviderOccurredAt=sourceAt,
                ConfirmedAt=sourceAt.AddSeconds(1),PaymentTimeBasis="Source"};
            db.Payments.Add(original);await db.SaveChangesAsync();
            var issued=await Service(scope).IssueAsync(new IssueInvoiceRequest{Kind="Orders",OrderIds=[orderId]},employeeId,"gateway-time-issue:"+suffix);
            return (await Uow(scope).ExecuteAsync(async ()=>
            {
                await db.Customers.FromSqlInterpolated($"SELECT * FROM [dbo].[Customers] WITH (UPDLOCK,HOLDLOCK) WHERE [CustomerId]={customerA}").AsNoTracking().SingleAsync();
                var order=await db.Orders.Include(x=>x.Details).SingleAsync(x=>x.OrderId==orderId);
                order.Status="Cancelled";order.Details.Single().Status="Cancelled";
                var refund=new Payment{OrderId=orderId,OrderDetailId=order.Details.Single().OrderDetailId,
                    RefundOfPaymentId=original.PaymentId,Type="Refund",Method="ZaloPay",Amount=90000,Status="Succeeded",
                    OperationKey="gateway-time-refund:"+suffix,Reason="Verified full cancellation",PaymentTime=confirmedAt,
                    ConfirmedAt=confirmedAt,ProviderOccurredAt=null,PaymentTimeBasis="Confirmation"};
                db.Payments.Add(refund);await db.SaveChangesAsync();
                db.RefundAttempts.Add(new RefundAttempt{PaymentId=refund.PaymentId,ProviderAppId=2553,
                    MerchantRefundId="261117_2553_"+suffix,Status="Succeeded",LastApi="QueryRefund",
                    LastQueriedAt=confirmedAt,LastReturnCode=1});await db.SaveChangesAsync();
                return await Service(scope).ApplyFinancialChangeAsync(orderId,null);
            }))!;
        }

        async Task<int> NewOrder(int customerId,decimal gross,decimal discount=0)
        {
            await using var scope=app.Services.CreateAsyncScope();var order=new Order {CustomerId=customerId,EmployeeId=employeeId,TotalAmount=gross-discount,DiscountAmount=discount};
            if(discount>0){order.VoucherCodeSnapshot="SALE10";order.VoucherDiscountTypeSnapshot="Fixed";order.VoucherDiscountValueSnapshot=discount;}
            order.Details.Add(new OrderDetail {ProductId=productId,ProductNameSnapshot="Food at order time",Quantity=1,UnitPrice=gross,DiscountAmount=discount});
            Db(scope).Orders.Add(order);await Db(scope).SaveChangesAsync();return order.OrderId;
        }
        async Task<int> AddCash(int orderId,decimal amount,bool succeeded)
        {
            await using var scope=app.Services.CreateAsyncScope();var payment=new Payment{OrderId=orderId,Amount=amount,Method="Cash",OperationKey="cash:"+Guid.NewGuid().ToString("N")};
            if(succeeded)Success(payment);Db(scope).Payments.Add(payment);await Db(scope).SaveChangesAsync();return payment.PaymentId;
        }
        async Task AddWallet(int orderId,decimal amount)
        {
            await using var scope=app.Services.CreateAsyncScope();await Uow(scope).ExecuteAsync(async ()=>
            {
                var wallet=scope.ServiceProvider.GetRequiredService<IWalletLedgerWriter>();
                var debit=await wallet.RecordAsync(new WalletMutation(customerA,WalletMutationKind.Debit,amount,"OrderPayment","Order",orderId.ToString(),"wallet:"+orderId,
                    new WalletActor("Staff",employeeId),DateTimeOffset.UtcNow),default);
                await Uow(scope).SaveChangesAsync();var payment=new Payment{OrderId=orderId,Amount=amount,Method="Wallet",WalletTransactionId=debit.Entry.TransactionId,OperationKey="wallet-payment:"+orderId};
                Success(payment);Db(scope).Payments.Add(payment);await Uow(scope).SaveChangesAsync();return true;
            });
        }
        async Task<int> NewSession(int customerId,decimal amount)
        {
            await using var scope=app.Services.CreateAsyncScope();var db=Db(scope);var now=DateTime.UtcNow;
            var session=new GamingSession{CustomerId=customerId,StartTime=now.AddHours(-1),EndTime=now,Status="Ended",TotalCost=amount,
                Computer=new Computer{ComputerName="INV-PC-"+Guid.NewGuid().ToString("N")[..6],Room=new ComputerRoom{RoomName="Invoice room"},ComputerType=new ComputerType{TypeName="Invoice type",PricePerHour=10000}}};
            db.GamingSessions.Add(session);SessionCharge? charge=null;
            if(amount>0){charge=new SessionCharge{GamingSession=session,StartTime=session.StartTime,EndTime=now,Amount=amount};db.SessionCharges.Add(charge);}
            var customer=await db.Customers.SingleAsync(x=>x.CustomerId==customerId);customer.WalletBalance=amount;await db.SaveChangesAsync();
            if(charge!=null)await Uow(scope).ExecuteAsync(async ()=>
            {
                var wallet=scope.ServiceProvider.GetRequiredService<IWalletLedgerWriter>();
                var debit=await wallet.RecordAsync(new WalletMutation(customerId,WalletMutationKind.Debit,amount,"GamingCharge","SessionCharge",charge.ChargeId.ToString(),"charge:"+charge.ChargeId,
                    new WalletActor("System"),DateTimeOffset.UtcNow),default);
                await Uow(scope).SaveChangesAsync();var payment=new Payment{SessionChargeId=charge.ChargeId,Amount=amount,Method="Wallet",WalletTransactionId=debit.Entry.TransactionId,OperationKey="charge-payment:"+charge.ChargeId};
                Success(payment);db.Payments.Add(payment);await Uow(scope).SaveChangesAsync();return true;
            });
            return session.GamingSessionId;
        }
        async Task Financial(int orderId)
        {await using var scope=app.Services.CreateAsyncScope();await Uow(scope).ExecuteAsync(()=>Service(scope).ApplyFinancialChangeAsync(orderId,null));}
        static ApplicationDbContext Db(AsyncServiceScope scope)=>scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        static IInvoiceService Service(AsyncServiceScope scope)=>scope.ServiceProvider.GetRequiredService<IInvoiceService>();
        static IInvoiceUnitOfWork Uow(AsyncServiceScope scope)=>scope.ServiceProvider.GetRequiredService<IInvoiceUnitOfWork>();
        static void Success(Payment payment){payment.Status="Succeeded";payment.PaymentTime=payment.ConfirmedAt=DateTimeOffset.UtcNow;payment.PaymentTimeBasis="Confirmation";}
        async Task Throws<T>(string name,Func<Task> action) where T:Exception
        {try{await action();}catch(T){Check(name,true);return;}throw new Exception("Expected "+typeof(T).Name+": "+name);}
        void Check(string name,bool passed){if(!passed)throw new Exception("Invoice check failed: "+name);checks++;Console.WriteLine("PASS Invoice: "+name);}
    }
    private sealed class Rollback:Exception;
}
