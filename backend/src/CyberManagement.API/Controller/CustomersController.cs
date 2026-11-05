using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using CyberManagement.API.Security;
using CyberManagement.Application.DTOs.Customers;
using CyberManagement.Application.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CyberManagement.API.Controller;

[ApiController]
[Route("api/admin/customers")]
public sealed class CustomersController(ICustomerService customers) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = CustomerPolicies.View)]
    public async Task<ActionResult<IReadOnlyList<CustomerDto>>> List(CancellationToken cancellationToken) =>
        Ok(await customers.ListAsync(cancellationToken));

    [HttpGet("{id:int}")]
    [Authorize(Policy = CustomerPolicies.View)]
    public async Task<ActionResult<CustomerDto>> Get(int id, CancellationToken cancellationToken) =>
        Ok(await customers.GetAsync(id, cancellationToken));

    [HttpPost]
    [Authorize(Policy = CustomerPolicies.Create)]
    public async Task<ActionResult<CustomerDto>> Create(
        [FromBody] CreateCustomerRequest request, CancellationToken cancellationToken)
    {
        var created = await customers.CreateAsync(
            new CreateCustomerCommand(request.FullName, request.Phone, request.Email, request.InitialPassword, ActorId()),
            cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = created.CustomerId }, created);
    }

    [HttpPut("{id:int}")]
    [Authorize(Policy = CustomerPolicies.Update)]
    public async Task<ActionResult<CustomerDto>> Update(
        int id, [FromBody] UpdateCustomerRequest request, CancellationToken cancellationToken) =>
        Ok(await customers.UpdateAsync(
            new UpdateCustomerCommand(id, request.FullName, request.Phone, request.Email), cancellationToken));

    [HttpPut("{id:int}/status")]
    [Authorize(Policy = CustomerPolicies.StatusChange)]
    public async Task<ActionResult<CustomerDto>> ChangeStatus(
        int id, [FromBody] ChangeCustomerStatusRequest request, CancellationToken cancellationToken) =>
        Ok(await customers.ChangeStatusAsync(
            new ChangeCustomerStatusCommand(id, request.Status, request.Reason, ActorId()), cancellationToken));

    [HttpGet("{id:int}/wallet-transactions")]
    [Authorize(Policy = CustomerPolicies.WalletView)]
    public async Task<ActionResult<IReadOnlyList<WalletTransactionDto>>> WalletTransactions(
        int id, CancellationToken cancellationToken) =>
        Ok(await customers.GetWalletTransactionsAsync(id, cancellationToken));

    [HttpPost("{id:int}/wallet/top-ups")]
    [Authorize(Policy = CustomerPolicies.WalletTopUp)]
    public async Task<ActionResult<WalletTransactionDto>> TopUp(
        int id, [FromBody] StaffTopUpRequest request,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey, CancellationToken cancellationToken)
    {
        var result = await customers.TopUpAsync(
            new StaffTopUpCommand(id, request.Amount, request.CollectionMethod, ActorId(), idempotencyKey?.Trim()),
            cancellationToken);
        // 201: ví vừa được cộng. 200 + Idempotency-Replayed: khóa đã xử lý trước đó, đây là kết quả cũ, ví không đổi thêm.
        if (!result.Replayed)
            return StatusCode(StatusCodes.Status201Created, result.Transaction);
        Response.Headers["Idempotency-Replayed"] = "true";
        return Ok(result.Transaction);
    }

    // sub = EmployeeId (hợp đồng identity); thiếu/sai thì ApiExceptionHandler trả 401/403.
    private int ActorId() =>
        int.TryParse(User.FindFirstValue(JwtRegisteredClaimNames.Sub), out var id)
            ? id
            : throw new UnauthorizedAccessException("Không xác định được nhân viên từ token.");
}

public sealed class CreateCustomerRequest
{
    public string? FullName { get; init; }
    public string? Phone { get; init; }
    public string? Email { get; init; }
    public string? InitialPassword { get; init; }
}

public sealed class UpdateCustomerRequest
{
    public string? FullName { get; init; }
    public string? Phone { get; init; }
    public string? Email { get; init; }
}

public sealed class ChangeCustomerStatusRequest
{
    public string? Status { get; init; }
    public string? Reason { get; init; }
}

public sealed class StaffTopUpRequest
{
    public decimal Amount { get; init; }
    public string? CollectionMethod { get; init; }
}
