using System.Globalization;
using System.Security.Claims;
using System.Text.Json.Serialization;
using CyberCafe.Api.Data;
using CyberCafe.Api.Features;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();

// Enum (TopUpStatus, WalletTransactionType, CollectionMethod...) trả về dạng chữ, khớp DB và frontend,
// thay vì số 0/1/2.
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

// Không có connection string thì API vẫn chạy /health (giữ hành vi cũ), nhưng không có endpoint nghiệp vụ.
var connectionString = builder.Configuration.GetConnectionString("CyberCafe");
var hasDatabase = !string.IsNullOrWhiteSpace(connectionString);
if (hasDatabase)
{
    builder.Services.AddDbContext<CyberCafeDbContext>(options => options.UseSqlServer(connectionString));
}

// TẠM THỜI — thuộc phạm vi của Đại (S1-D-1): cookie auth tối thiểu để endpoint của Minh chạy được.
// Hợp đồng Minh cần: ClaimTypes.NameIdentifier = accountId, ClaimTypes.Role = Admin | Staff | Customer.
// Khi Đại đưa auth thật lên, thay khối này (và khối /dev bên dưới) bằng cấu hình của Đại.
builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = "cybercafe.auth";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        // Demo chạy HTTP trên LAN nên không ép Secure; bật Always khi có HTTPS.
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;
        // API trả 401/403 thay vì redirect sang trang đăng nhập.
        options.Events.OnRedirectToLogin = context =>
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return Task.CompletedTask;
        };
        options.Events.OnRedirectToAccessDenied = context =>
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return Task.CompletedTask;
        };
    });
builder.Services.AddAuthorization();

var app = builder.Build();

if (!app.Environment.IsDevelopment()) app.UseExceptionHandler();

app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

if (hasDatabase)
{
    app.MapCustomerEndpoints();
    app.MapClientProfileEndpoint();
}
else
{
    app.Logger.LogWarning(
        "Chưa cấu hình ConnectionStrings:CyberCafe: chỉ /health hoạt động, các endpoint nghiệp vụ không được đăng ký.");
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();

    // CHỈ DÙNG KHI DEV để thử API trước khi có login thật của Đại. Không tồn tại ngoài môi trường Development.
    // Đăng nhập bằng một account CÓ SẴN trong DB (không kiểm tra mật khẩu), vai trò lấy từ DB.
    if (hasDatabase)
    {
        app.MapPost("/dev/sign-in/{accountId:long}", async (
            long accountId, CyberCafeDbContext db, HttpContext http, CancellationToken ct) =>
        {
            var account = await db.Accounts.AsNoTracking()
                .FirstOrDefaultAsync(a => a.Id == accountId && a.IsActive, ct);
            if (account is null) return Results.NotFound();

            var claims = new Claim[]
            {
                new(ClaimTypes.NameIdentifier, account.Id.ToString(CultureInfo.InvariantCulture)),
                new(ClaimTypes.Name, account.DisplayName),
                new(ClaimTypes.Role, account.Role.ToString()),
            };
            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            await http.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity));
            return Results.Ok(new { accountId = account.Id, role = account.Role.ToString() });
        });

        app.MapPost("/dev/sign-out", async (HttpContext http) =>
        {
            await http.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return Results.NoContent();
        });
    }
}

app.Run();

public partial class Program;
