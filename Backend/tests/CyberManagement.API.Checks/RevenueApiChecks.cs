using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using CyberManagement.API;
using CyberManagement.API.Controller;
using CyberManagement.Application.Security;
using CyberManagement.Infrastructure.Persistance;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;

internal static class RevenueApiChecks
{
    public static async Task RunAsync(bool validateOnly = false)
    {
        var connection=Environment.GetEnvironmentVariable("AUTH_CHECKS_SQL_CONNECTION");
        if(!validateOnly && string.IsNullOrWhiteSpace(connection)) throw new InvalidOperationException("AUTH_CHECKS_SQL_CONNECTION required; no InMemory fallback.");
        const string signingKey="revenue-api-checks-only-signing-key-at-least-32-bytes";
        var fixture=new AuthCheckDatabase(connection);
        var builder=WebApplication.CreateBuilder(new WebApplicationOptions {EnvironmentName="Development",ApplicationName=typeof(CyberManagement.API.Program).Assembly.FullName});
        builder.WebHost.UseTestServer(); builder.Logging.ClearProviders();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string,string?> { ["Authentication:Jwt:SigningKey"]=signingKey });
        RevenueSqlChecks.Configure(builder.Services,fixture);
        builder.Services.AddApiServices(builder.Configuration);
        builder.Services.AddControllers().AddApplicationPart(typeof(AuthController).Assembly);
        await using var app=builder.Build();
        if (validateOnly)
        {
            Console.WriteLine("Revenue API test host DI validation passed (no SQL connection opened).");
            return;
        }
        var checks=0;
        try
        {
            RevenueSqlChecks.SeedData seed;
            await using(var scope=app.Services.CreateAsyncScope())
            {
                await fixture.InitializeAsync(scope.ServiceProvider.GetRequiredService<ApplicationDbContext>());
                seed=await RevenueSqlChecks.SeedAsync(scope.ServiceProvider);
            }
            app.UseExceptionHandler();app.UseAuthentication();app.UseAuthorization();app.MapControllers();
            await app.StartAsync(); using var client=app.GetTestClient();
            const string report="/api/admin/reports";
            const string reconciliation=report+"/reconciliation";
            const string dates="?from=2026-11-16&to=2026-11-16";
            var tokens=RolePermissionCatalog.Roles.ToDictionary(x=>x.Name,x=>Token(seed.Employees[x.Name].ToString(),x.Name,x.Permissions));
            foreach(var route in new[]{report,reconciliation})
            {
                await Expect(route+" anonymous",route+dates,null,HttpStatusCode.Unauthorized);
                foreach(var (role,token) in tokens)
                    await Expect(route+" "+role,route+dates,token,role is "Admin" or "Manager"?HttpStatusCode.OK:HttpStatusCode.Forbidden);
            }
            using(var response=await Get(report+dates,tokens["Admin"]))
            {
                var json=await Json(response);
                Check(json.GetProperty("collectedAmount").GetDecimal()==440 && json.GetProperty("refundedAmount").GetDecimal()==50
                    && json.GetProperty("netRevenue").GetDecimal()==390 && json.GetProperty("topUpCollectedAmount").GetDecimal()==50000,"API/SQL amounts and separate top-up");
                Check(response.Headers.CacheControl?.NoStore==true,"Financial response disables caching");
                Check(!json.ToString().Contains("password",StringComparison.OrdinalIgnoreCase),"Response excludes identity secrets");
            }
            using(var response=await Get(reconciliation+dates,tokens["Manager"]))
            {
                var json=await Json(response);
                var transaction=json.GetProperty("transactions").EnumerateArray().Single(x=>x.GetProperty("entityType").GetString()=="Payment" && x.GetProperty("entityId").GetInt32()==seed.GatewayRefundId);
                Check(transaction.GetProperty("paymentTimeBasis").GetString()=="Confirmation" && transaction.GetProperty("providerOccurredAt").ValueKind==JsonValueKind.Null
                    && transaction.GetProperty("paymentTime").GetDateTimeOffset()==transaction.GetProperty("confirmedAt").GetDateTimeOffset(),"API refund timing metadata");
                Check(json.GetProperty("confirmationSummary").GetProperty("refundedAmount").GetDecimal()==50,"API confirmation summary");
            }
            foreach(var route in new[]{report,reconciliation})
            {
                foreach(var query in new[]{"?from=16/11/2026&to=2026-11-16","?from=&to=2026-11-16","?from=2026-11-17&to=2026-11-16",
                    "?from=2025-01-01&to=2026-11-16","?from=2026-11-16&from=2026-11-15&to=2026-11-16","?to=9999-12-31"})
                    await Expect("Bad date "+query,route+query,tokens["Admin"],HttpStatusCode.BadRequest);
            }
            using(var response=await Get(report+"?to=2026-11-16",tokens["Admin"]))
            { var json=await Json(response); Check(json.GetProperty("from").GetString()=="2026-11-01" && json.GetProperty("to").GetString()=="2026-11-16","Missing from defaults to first day of end month"); }
            var today=DateOnly.FromDateTime(DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(7)).DateTime);
            using(var response=await Get(report,tokens["Admin"]))
            { var json=await Json(response); Check(json.GetProperty("from").GetString()==new DateOnly(today.Year,today.Month,1).ToString("yyyy-MM-dd") && json.GetProperty("to").GetString()==today.ToString("yyyy-MM-dd"),"Omitted dates default to current Vietnam month through today"); }
            var customer=Token(seed.CustomerId.ToString(),"Customer",["report.view"],customer:true);
            await Expect("Customer cannot impersonate same-number employee",report+dates,customer,HttpStatusCode.Forbidden);
            await Expect("Caller employeeId ignored",report+dates+"&employeeId="+seed.Employees["Admin"],tokens["Cashier"],HttpStatusCode.Forbidden);
            await Expect("Malformed employee subject",report+dates,Token("invalid","Admin",["report.view"]),HttpStatusCode.Forbidden);
            await Expect("Unissued permission claim",report+dates,Token(seed.Employees["Admin"].ToString(),"Admin",[]),HttpStatusCode.Forbidden);
            await Expect("Expired JWT",report+dates,Token(seed.Employees["Admin"].ToString(),"Admin",["report.view"],expired:true),HttpStatusCode.Unauthorized);
            await Expect("Claim cannot replace current DB permission",report+dates,Token(seed.Employees["Cashier"].ToString(),"Admin",["report.view"]),HttpStatusCode.Forbidden);
            await using(var scope=app.Services.CreateAsyncScope())
            {
                var db=scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var manager=await db.Employees.SingleAsync(x=>x.EmployeeId==seed.Employees["Manager"]); manager.IsActive=false;await db.SaveChangesAsync();
                await Expect("Disabled account with old JWT",report+dates,tokens["Manager"],HttpStatusCode.Forbidden);
                manager.IsActive=true;await db.SaveChangesAsync();
                var permission=await db.RolePermissions.Include(x=>x.Permission).SingleAsync(x=>x.RoleId==manager.RoleId && x.Permission.PermissionName=="report.view");
                db.RolePermissions.Remove(permission);await db.SaveChangesAsync();
                await Expect("Revoked permission with old JWT",report+dates,tokens["Manager"],HttpStatusCode.Forbidden);
                db.RolePermissions.Add(permission);await db.SaveChangesAsync();
                var cash=await db.Payments.SingleAsync(x=>x.PaymentId==seed.CashPaymentId);cash.PaymentTimeBasis="Confirmation";await db.SaveChangesAsync();
                await Expect("Bad evidence yields conflict",report+dates,tokens["Admin"],HttpStatusCode.Conflict);
                using(var response=await Get(reconciliation+dates,tokens["Admin"]))
                { var json=await Json(response); Check(json.GetProperty("issues").EnumerateArray().Any(x=>x.GetProperty("code").GetString()=="PaymentTimeInvalid"),"Reconciliation explains bad evidence"); }
                cash.PaymentTimeBasis="Source";await db.SaveChangesAsync();
            }
            Console.WriteLine($"{checks} revenue API/SQL checks passed; signed JWT fixtures and real SQL, no provider call.");

            string Token(string sub,string role,IEnumerable<string> permissions,bool customer=false,bool expired=false)
            {
                var claims=new List<Claim>{new("sub",sub),new("role",role),new("name","Revenue fixture")};
                claims.AddRange(permissions.Select(x=>new Claim("permission",x)));
                if(customer){claims.Add(new("customer_id",sub));claims.Add(new("account_type","Customer"));}
                var now=DateTime.UtcNow;
                return new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken("CyberManagement","CyberManagement.Client",claims,
                    expires:expired?now.AddMinutes(-10):now.AddMinutes(10),signingCredentials:new(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)),SecurityAlgorithms.HmacSha256)));
            }
            async Task<HttpResponseMessage> Get(string url,string? token)
            { using var request=new HttpRequestMessage(HttpMethod.Get,url);if(token is not null)request.Headers.Authorization=new AuthenticationHeaderValue("Bearer",token);return await client.SendAsync(request); }
            async Task<JsonElement> Json(HttpResponseMessage response)
            { if(response.StatusCode!=HttpStatusCode.OK) throw new InvalidOperationException($"Expected 200, got {response.StatusCode}: "+await response.Content.ReadAsStringAsync());return (await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync())).RootElement.Clone(); }
            async Task Expect(string name,string url,string? token,HttpStatusCode expected)
            { using var response=await Get(url,token);if(response.StatusCode!=expected)throw new InvalidOperationException($"{name}: expected {expected}, got {response.StatusCode}: "+await response.Content.ReadAsStringAsync());Check(true,name); }
            void Check(bool success,string name) {if(!success)throw new InvalidOperationException(name);checks++;Console.WriteLine("PASS "+name);}
        }
        finally {try {await app.StopAsync();}finally{await fixture.CleanupAsync();}}
    }
}
