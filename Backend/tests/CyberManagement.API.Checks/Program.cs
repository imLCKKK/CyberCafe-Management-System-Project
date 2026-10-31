using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using CyberManagement.API;
using CyberManagement.Application.DTOs;
using CyberManagement.Application.Exceptions;
using CyberManagement.Application.Interfaces;
using CyberManagement.Application.Interfaces.Services;
using CyberManagement.Application.Validators;

var builder = WebApplication.CreateBuilder(args);
builder.Logging.ClearProviders();
builder.WebHost.UseUrls("http://127.0.0.1:0");
builder.Services.AddApiServices();
builder.Services.AddControllers();
builder.Services.AddScoped<IInvoiceService, InvoiceStub>();
await using var app = builder.Build();
app.UseExceptionHandler();
// Chỉ host kiểm thử tạo danh tính từ header giả lập. Mã này không được đưa vào API thật.
app.Use(async (context, next) =>
{
    var role = context.Request.Headers["X-Test-Role"].ToString();
    if (!string.IsNullOrEmpty(role))
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.Role, role), new("customer_id", "1"), new("employee_id", "8"),
            new("is_active", context.Request.Headers.ContainsKey("X-Test-Inactive") ? "false" : "true"),
            new("must_change_password", "true")
        };
        if (!context.Request.Headers.ContainsKey("X-Test-Missing-Account")) claims.Add(new("account_id", "42"));
        context.User = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestOnly"));
    }
    await next();
});
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
await app.StartAsync();
using var client = new HttpClient { BaseAddress = new Uri(app.Urls.Single()) };
var count = 0;

try
{
    await Check("Anonymous invoice access", HttpMethod.Get, "/api/invoices/1", null, 401);
    await Check("Customer reads invoice", HttpMethod.Get, "/api/invoices/1", "Customer", 200);
    await Check("Customer cannot list all invoices", HttpMethod.Get, "/api/invoices", "Customer", 403);
    await Check("Staff lists invoices", HttpMethod.Get, "/api/invoices", "Staff", 200);
    await Check("Admin lists invoices", HttpMethod.Get, "/api/invoices", "Admin", 200);
    await Check("Statement endpoint", HttpMethod.Get, "/api/invoices/session/1/statement", "Customer", 200);
    await Check("Missing session", HttpMethod.Get, "/api/invoices/session/999/statement", "Customer", 404);
    await Check("Negative invoice ID", HttpMethod.Get, "/api/invoices/-1", "Customer", 400);
    await Check("Unlinked order invoice", HttpMethod.Get, "/api/invoices/order/1", "Customer", 404);
    await Check("Inactive account", HttpMethod.Get, "/api/invoices/1", "Customer", 403, inactive: true);
    await Check("Missing account ID", HttpMethod.Get, "/api/invoices/1", "Staff", 403, missingAccount: true);
    await Check("Invalid role", HttpMethod.Get, "/api/invoices/1", "Unknown", 403);
    foreach (var route in new[] { "/api/orders", "/api/products", "/api/categories" })
        await Check("Removed controller " + route, HttpMethod.Get, route, "Staff", 404);
    Console.WriteLine($"{count} HTTP checks passed.");
}
finally { await app.StopAsync(); }

async Task<HttpResponseMessage> Check(string name, HttpMethod method, string url, string? role, int status,
    object? body = null, string? rawJson = null, bool inactive = false, bool missingAccount = false)
{
    using var message = new HttpRequestMessage(method, url);
    if (role != null) message.Headers.Add("X-Test-Role", role);
    if (inactive) message.Headers.Add("X-Test-Inactive", "true");
    if (missingAccount) message.Headers.Add("X-Test-Missing-Account", "true");
    if (body != null) message.Content = JsonContent.Create(body);
    if (rawJson != null) message.Content = new StringContent(rawJson, System.Text.Encoding.UTF8, "application/json");
    var response = await client.SendAsync(message);
    if ((int)response.StatusCode != status)
        throw new Exception($"{name}: expected {status}, got {(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
    count++; Console.WriteLine($"PASS {name}");
    return response;
}
sealed class InvoiceStub : IInvoiceService
{
    public Task<IEnumerable<InvoiceDTOs>> GetAllAsync() => Task.FromResult<IEnumerable<InvoiceDTOs>>([]);
    public Task<InvoiceDTOs?> GetByIdAsync(long id) => Task.FromResult<InvoiceDTOs?>(new() { InvoiceId = id, CustomerId = 1 });
    public Task<InvoiceDTOs?> GetByOrderIdAsync(long id) => Task.FromResult<InvoiceDTOs?>(null);
    public Task<SessionStatementDTO?> GetSessionStatementAsync(long id) => Task.FromResult<SessionStatementDTO?>(
        id == 1 ? new() { SessionId = 1, CustomerId = 1, FoodDebited = 100 } : null);
}
