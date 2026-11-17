using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using CyberManagement.Application.DTOs.Authentication;
using CyberManagement.Application.DTOs.Employees;
using CyberManagement.Application.Interfaces.Services;
using CyberManagement.Application.Services;
using CyberManagement.API.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;

namespace CyberManagement.API.Controller;

[ApiController]
[Route("api/auth")]
public sealed class AuthController(
    IIdentityService identityService,
    ICustomerIdentityService customerIdentityService,
    EmployeesService employeesService,
    IConfiguration configuration) : ControllerBase
{
    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<ActionResult<LoginResponse>> Login(
        [FromBody] LoginRequest request,
        CancellationToken cancellationToken)
    {
        var result = await identityService.LoginAsync(
            new LoginCommand(request.Username, request.Password),
            cancellationToken);
        if (result.Status == LoginStatus.InvalidCredentials)
            return Unauthorized(new { message = "Tên đăng nhập hoặc mật khẩu không chính xác." });
        if (result.Status == LoginStatus.AccountUnavailable)
            return Forbid();

        var employee = result.Employee
            ?? throw new InvalidOperationException("Authenticated login did not return an employee.");
        var permissions = employee.Permissions;
        var now = DateTime.UtcNow;
        var expiresAt = now.AddMinutes(GetTokenLifetimeMinutes());
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, employee.EmployeeId.ToString()),
            new("name", employee.Username),
            new("role", employee.Role)
        };
        claims.AddRange(permissions.Select(permission => new Claim("permission", permission)));

        return Ok(new LoginResponse(
            CreateAccessToken(claims, now, expiresAt),
            expiresAt,
            employee));
    }

    [AllowAnonymous]
    [HttpPost("customer/login")]
    public async Task<ActionResult<CustomerLoginResponse>> CustomerLogin(
        [FromBody] CustomerLoginRequest request,
        CancellationToken cancellationToken)
    {
        var result = await customerIdentityService.LoginAsync(
            new CustomerLoginCommand(request.Phone, request.Password),
            cancellationToken);
        if (result.Status == CustomerLoginStatus.InvalidCredentials)
            return Unauthorized(new { message = "Số điện thoại hoặc mật khẩu không chính xác." });
        if (result.Status == CustomerLoginStatus.AccountUnavailable)
            return Forbid();

        var customer = result.Customer
            ?? throw new InvalidOperationException("Authenticated customer login did not return a customer.");
        var now = DateTime.UtcNow;
        var expiresAt = now.AddMinutes(GetTokenLifetimeMinutes());
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, customer.CustomerId.ToString()),
            new Claim("customer_id", customer.CustomerId.ToString()),
            new Claim("name", customer.FullName),
            new Claim("phone", customer.Phone),
            new Claim("role", "Customer"),
            new Claim("account_type", "Customer")
        };

        return Ok(new CustomerLoginResponse(
            CreateAccessToken(claims, now, expiresAt),
            expiresAt,
            customer));
    }

    [Authorize(Roles = "Admin,Manager,Cashier,Waiter,Technician")]
    [HttpPost("logout")]
    public IActionResult Logout() => NoContent();

    [HttpPost("change-password")]
    [EndpointSummary("Change the authenticated employee's password")]
    [Authorize(Policy = ApiPolicies.ActiveAccount)]
    [Authorize(Roles = "Admin,Manager,Cashier,Waiter,Technician")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ChangePassword(
        [FromBody] ChangePasswordRequest request, CancellationToken cancellationToken)
    {
        if (!int.TryParse(User.FindFirstValue(JwtRegisteredClaimNames.Sub), out var employeeId))
            return Unauthorized();

        await employeesService.ChangePasswordAsync(employeeId, request, employeeId, cancellationToken);
        return NoContent();
    }

    [Authorize(Policy = ApiPolicies.ActiveAccount)]
    [Authorize(Roles = "Admin,Manager,Cashier,Waiter,Technician")]
    [HttpGet("me")]
    public async Task<IActionResult> GetCurrentUser(CancellationToken cancellationToken)
    {
        if (!int.TryParse(User.FindFirstValue(JwtRegisteredClaimNames.Sub), out var employeeId))
            return Unauthorized();

        var employee = await employeesService.GetEmployeeByIdAsync(employeeId, cancellationToken);
        if (employee is null || !employee.IsActive)
            return Forbid();

        return Ok(new
        {
            EmployeeId = employeeId,
            Username = employee.Username,
            Role = employee.RoleName,
            Permissions = User.FindAll("permission").Select(claim => claim.Value).Order(StringComparer.Ordinal)
        });
    }

    [Authorize(Roles = "Customer")]
    [HttpPost("customer/logout")]
    public IActionResult CustomerLogout() => NoContent();

    [Authorize(Roles = "Customer")]
    [HttpGet("customer/me")]
    public IActionResult GetCurrentCustomer()
    {
        var customerId = User.FindFirstValue("customer_id");
        return Ok(new
        {
            CustomerId = customerId,
            FullName = User.FindFirstValue("name"),
            Phone = User.FindFirstValue("phone")
        });
    }

    private string CreateAccessToken(
        IEnumerable<Claim> claims,
        DateTime issuedAt,
        DateTime expiresAt)
    {
        var key = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(configuration["Authentication:Jwt:SigningKey"]!));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer: configuration["Authentication:Jwt:Issuer"] ?? "CyberManagement",
            audience: configuration["Authentication:Jwt:Audience"] ?? "CyberManagement.Client",
            claims: claims,
            notBefore: issuedAt,
            expires: expiresAt,
            signingCredentials: credentials);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private int GetTokenLifetimeMinutes()
    {
        var lifetime = configuration.GetValue("Authentication:Jwt:TokenLifetimeMinutes", 20);
        if (lifetime is < 1 or > 30)
            throw new InvalidOperationException("Authentication:Jwt:TokenLifetimeMinutes must be between 1 and 30.");
        return lifetime;
    }
}

public sealed class LoginRequest
{
    public string Username { get; init; } = string.Empty;

    public string Password { get; init; } = string.Empty;
}

public sealed class CustomerLoginRequest
{
    public string Phone { get; init; } = string.Empty;

    public string Password { get; init; } = string.Empty;
}

public sealed record LoginResponse(
    string AccessToken,
    DateTime ExpiresAtUtc,
    AuthenticatedEmployeeDto Employee);

public sealed record CustomerLoginResponse(
    string AccessToken,
    DateTime ExpiresAtUtc,
    AuthenticatedCustomerDto Customer);
