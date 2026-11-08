using System.Globalization;
using System.Security.Claims;
using CyberManagement.API.Security;
using CyberManagement.Application.DTOs.Customers;
using CyberManagement.Application.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CyberManagement.API.Controller;

[ApiController]
[Route("api")]
public sealed class CustomersController(ICustomerService customers) : ControllerBase
{
    [HttpGet("admin/customers")]
    [Authorize(Policy = ApiPolicies.CustomerView)]
    public async Task<IActionResult> GetCustomers(
        CancellationToken cancellationToken, int page = 1, int pageSize = 20, string? search = null) =>
        Ok(await customers.GetCustomersAsync(page, pageSize, search, cancellationToken));

    [HttpPost("admin/customers")]
    [Authorize(Policy = ApiPolicies.CustomerCreate)]
    public async Task<IActionResult> CreateCustomer(
        [FromBody] CreateCustomerCommand command, CancellationToken cancellationToken)
    {
        var customer = await customers.CreateAsync(command, cancellationToken);
        return CreatedAtAction(nameof(GetCustomer), new { customerId = customer.CustomerId }, customer);
    }

    [HttpGet("admin/customers/{customerId}")]
    [Authorize(Policy = ApiPolicies.CustomerView)]
    public async Task<IActionResult> GetCustomer(int customerId, CancellationToken cancellationToken) =>
        Ok(await customers.GetProfileAsync(customerId, cancellationToken));

    [HttpGet("admin/customers/{customerId}/wallet")]
    [Authorize(Policy = ApiPolicies.WalletView)]
    public async Task<IActionResult> GetWallet(
        int customerId, CancellationToken cancellationToken, int page = 1, int pageSize = 20) =>
        Ok(await customers.GetWalletAsync(customerId, page, pageSize, cancellationToken));

    [HttpGet("customers/me")]
    [Authorize(Roles = "Customer")]
    public async Task<IActionResult> GetOwnProfile(CancellationToken cancellationToken) =>
        Ok(await customers.GetOwnProfileAsync(GetOwnCustomerId(), cancellationToken));

    [HttpGet("customers/me/wallet")]
    [Authorize(Roles = "Customer")]
    public async Task<IActionResult> GetOwnWallet(
        CancellationToken cancellationToken, int page = 1, int pageSize = 20) =>
        Ok(await customers.GetOwnWalletAsync(GetOwnCustomerId(), page, pageSize, cancellationToken));

    private int GetOwnCustomerId()
    {
        var claim = User.FindFirstValue("customer_id");
        if (User.FindFirstValue("account_type") != "Customer"
            || !int.TryParse(claim, NumberStyles.None, CultureInfo.InvariantCulture, out var customerId)
            || customerId <= 0 || User.FindFirstValue("sub") != claim)
            throw new UnauthorizedAccessException("Danh tính khách hàng không hợp lệ.");
        return customerId;
    }
}
