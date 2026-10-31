using System.Net;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

//Đây là controller chỉ dùng trong môi trường Development, để cấp danh tính giả lập phục vụ test, không phải chức năng đăng nhập thật.
// trong console run: dotnet user-secrets set "TestAuth:Enabled" "true" --project CyberManagement.API
// trong console run: dotnet user-secrets set "TestAuth:Key" "KHOA_KIEM_THU_CUA_BAN" --project CyberManagement.API
// Khi quên khóa run: dotnet user-secrets remove "TestAuth:Key" --project CyberManagement.API
namespace CyberManagement.API.Controller;

[ApiController]
[Route("api/dev/auth")]
public class DevAuthController(
    IWebHostEnvironment environment,
    IConfiguration configuration) : ControllerBase
{
    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<IActionResult> Login(
        [FromBody] DevLoginRequest request,
        [FromHeader(Name = "X-Dev-Test-Key")] string? testKey)
    {
        // Chỉ cho phép khi bật rõ ràng trong Development,
        // và request đến từ chính máy đang chạy API.
        var remoteIp = HttpContext.Connection.RemoteIpAddress;

        if (!environment.IsDevelopment()
            || !configuration.GetValue<bool>("TestAuth:Enabled")
            || remoteIp is null
            || !IPAddress.IsLoopback(remoteIp))
        {
            return NotFound();
        }

        var expectedKey = configuration["TestAuth:Key"];

        if (string.IsNullOrWhiteSpace(expectedKey)
            || string.IsNullOrWhiteSpace(testKey)
            || testKey != expectedKey)
        {
            return Unauthorized();
        }

        if (request.AccountId <= 0
            || request.Role is not ("Admin" or "Staff" or "Customer")
            || request.EmployeeId is <= 0
            || request.CustomerId is <= 0
            || (request.Role == "Customer"
                && request.CustomerId is null))
        {
            return BadRequest("Danh tính kiểm thử không hợp lệ.");
        }

        var claims = new List<Claim>
        {
            new("account_id", request.AccountId.ToString()),
            new(ClaimTypes.Role, request.Role),
            new("is_active", "true"),
            new("must_change_password", "false")
        };

        if (request.EmployeeId.HasValue)
        {
            claims.Add(new Claim(
                "employee_id", request.EmployeeId.Value.ToString()));
        }

        if (request.CustomerId.HasValue)
        {
            claims.Add(new Claim(
                "customer_id", request.CustomerId.Value.ToString()));
        }

        var identity = new ClaimsIdentity(
            claims,
            CookieAuthenticationDefaults.AuthenticationScheme);

        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(identity),
            new AuthenticationProperties
            {
                IsPersistent = false,
                ExpiresUtc = DateTimeOffset.UtcNow.AddMinutes(30)
            });

        return Ok(new { request.AccountId, request.Role });
    }
}

public class DevLoginRequest
{
    public long AccountId { get; set; }
    public string Role { get; set; } = string.Empty;
    public long? EmployeeId { get; set; }
    public long? CustomerId { get; set; }
}