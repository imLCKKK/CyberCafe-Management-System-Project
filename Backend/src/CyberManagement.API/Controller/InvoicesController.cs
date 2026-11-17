using System.Globalization;
using System.Security.Claims;
using CyberManagement.Application.DTOs.Invoices;
using CyberManagement.Application.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CyberManagement.API.Controller;

[ApiController]
[Route("api/admin/invoices")]
[Authorize]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
[ProducesResponseType(StatusCodes.Status400BadRequest)]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
[ProducesResponseType(StatusCodes.Status403Forbidden)]
[ProducesResponseType(StatusCodes.Status404NotFound)]
[ProducesResponseType(StatusCodes.Status409Conflict)]
public sealed class InvoicesController(IInvoiceService invoices) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = "permission:invoice.view")]
    public async Task<ActionResult<IReadOnlyList<InvoiceDto>>> List(CancellationToken ct) =>
        Ok(await invoices.GetAllAsync(InvoiceIdentity.Employee(User), ct));

    [HttpGet("{id:int}")]
    [Authorize(Policy = "permission:invoice.view")]
    public async Task<ActionResult<InvoiceDto>> Detail(int id, CancellationToken ct) =>
        Ok(await invoices.GetByIdAsync(id, InvoiceIdentity.Employee(User), ct));

    [HttpPost]
    [Authorize(Policy = "permission:invoice.create")]
    [ProducesResponseType<InvoiceDto>(StatusCodes.Status201Created)]
    public async Task<ActionResult<InvoiceDto>> Issue([FromBody] IssueInvoiceRequest request,
        [FromHeader(Name = "Idempotency-Key")] string operationKey, CancellationToken ct)
    {
        if (Request.Headers["Idempotency-Key"].Count != 1) throw new ArgumentException("Phải gửi đúng một Idempotency-Key.");
        var invoice = await invoices.IssueAsync(request, InvoiceIdentity.Employee(User), operationKey, ct);
        return CreatedAtAction(nameof(Detail), new { id = invoice.Id }, invoice);
    }

    [HttpGet("{id:int}/print")]
    [Authorize(Policy = "permission:invoice.view")]
    public async Task<ContentResult> Print(int id, CancellationToken ct)
    {
        var invoice = await invoices.GetByIdAsync(id, InvoiceIdentity.Employee(User), ct);
        Response.Headers.ContentSecurityPolicy = InvoicePrint.ContentPolicy;
        return Content(InvoicePrint.Render(invoice), "text/html; charset=utf-8");
    }
}

[ApiController]
[Route("api/client/invoices")]
[Authorize(Roles = "Customer")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class ClientInvoicesController(IInvoiceService invoices) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<InvoiceDto>>> List(CancellationToken ct) =>
        Ok(await invoices.GetOwnAsync(InvoiceIdentity.Customer(User), ct));

    [HttpGet("{id:int}")]
    public async Task<ActionResult<InvoiceDto>> Detail(int id, CancellationToken ct) =>
        Ok(await invoices.GetOwnByIdAsync(id, InvoiceIdentity.Customer(User), ct));

    [HttpGet("{id:int}/print")]
    public async Task<ContentResult> Print(int id, CancellationToken ct)
    {
        var invoice = await invoices.GetOwnByIdAsync(id, InvoiceIdentity.Customer(User), ct);
        Response.Headers.ContentSecurityPolicy = InvoicePrint.ContentPolicy;
        return Content(InvoicePrint.Render(invoice), "text/html; charset=utf-8");
    }
}

internal static class InvoiceIdentity
{
    public static int Employee(ClaimsPrincipal user)
    {
        if (user.IsInRole("Customer") || user.FindFirstValue("account_type") == "Customer")
            throw new UnauthorizedAccessException("Danh tính nhân viên không hợp lệ.");
        return Id(user.FindFirstValue("sub"));
    }
    public static int Customer(ClaimsPrincipal user)
    {
        var claim = user.FindFirstValue("customer_id");
        if (user.FindFirstValue("account_type") != "Customer" || user.FindFirstValue("sub") != claim)
            throw new UnauthorizedAccessException("Danh tính khách hàng không hợp lệ.");
        return Id(claim);
    }
    private static int Id(string? value) => int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var id) && id > 0
        ? id : throw new UnauthorizedAccessException("Danh tính đăng nhập không hợp lệ.");
}
