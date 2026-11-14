using System.Globalization;
using System.Security.Claims;
using System.ComponentModel.DataAnnotations;
using CyberManagement.API.Security;
using CyberManagement.Application.DTOs.Customers;
using CyberManagement.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CyberManagement.API.Controller;

[ApiController]
[Route("api")]
public sealed class CounterTopUpsController(CounterTopUpService topups) : ControllerBase
{
    [HttpGet("customers/me/wallet/counter-topups/options")]
    [Authorize(Roles = "Customer")]
    public async Task<IActionResult> GetOptions(CancellationToken cancellationToken) =>
        Ok(await topups.GetOptionsAsync(OwnCustomerId(), cancellationToken));

    [HttpGet("customers/me/wallet/counter-topups")]
    [Authorize(Roles = "Customer")]
    public async Task<IActionResult> GetOwn(CancellationToken cancellationToken, int page = 1, int pageSize = 20) =>
        Ok(await topups.GetOwnAsync(OwnCustomerId(), page, pageSize, cancellationToken));

    [HttpPost("customers/me/wallet/counter-topups")]
    [Authorize(Roles = "Customer")]
    public async Task<IActionResult> Create([FromBody] CreateCounterTopUpCommand command, CancellationToken cancellationToken)
    {
        var raw = Request.Headers["Idempotency-Key"];
        if (raw.Count != 1 || !Guid.TryParseExact(raw[0], "D", out var key))
            throw new ValidationException("Idempotency-Key phải là UUID dạng chuẩn.");
        var result = await topups.CreateAsync(OwnCustomerId(), key, command, cancellationToken);
        return StatusCode(result.Replayed ? StatusCodes.Status200OK : StatusCodes.Status201Created, result.Request);
    }

    [HttpGet("admin/topups")]
    [Authorize(Policy = ApiPolicies.WalletTopUp)]
    public async Task<IActionResult> GetAdmin(CancellationToken cancellationToken, int page = 1, int pageSize = 20, string status = "Pending") =>
        Ok(await topups.GetAdminAsync(status, page, pageSize, cancellationToken));

    [HttpPost("admin/topups/{requestId}/approve")]
    [Authorize(Policy = ApiPolicies.WalletTopUp)]
    public async Task<IActionResult> Approve(int requestId, [FromBody] ApproveCounterTopUpCommand command, CancellationToken cancellationToken) =>
        Ok(await topups.ApproveAsync(requestId, StaffId(), command, cancellationToken));

    private int OwnCustomerId() => IdentityId("Customer", "customer_id");
    private int StaffId()
    {
        // Existing staff JWTs identify employees through sub and have no account_type claim.
        if (User.FindFirstValue("account_type") is not null
            || string.IsNullOrEmpty(User.FindFirstValue("role")) || User.FindFirstValue("role") == "Customer"
            || !int.TryParse(User.FindFirstValue("sub"), NumberStyles.None, CultureInfo.InvariantCulture, out var id) || id <= 0)
            throw new UnauthorizedAccessException("Danh tính nhân viên không hợp lệ.");
        return id;
    }
    private int IdentityId(string accountType, string claimType)
    {
        var claim = User.FindFirstValue(claimType);
        if (User.FindFirstValue("account_type") != accountType || User.FindFirstValue("sub") != claim
            || !int.TryParse(claim, NumberStyles.None, CultureInfo.InvariantCulture, out var id) || id <= 0)
            throw new UnauthorizedAccessException("Danh tính tài khoản không hợp lệ.");
        return id;
    }
}
