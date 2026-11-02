using CyberManagement.API.Security;
using CyberManagement.Application.DTOs;
using CyberManagement.Application.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;

namespace CyberManagement.API.Controller;

[ApiController]
[Authorize(Policy = ApiPolicies.ActiveAccount)]
public class InvoicesController : ControllerBase
{
    private readonly IInvoiceService _invoiceService;

    public InvoicesController(IInvoiceService invoiceService)
    {
        _invoiceService = invoiceService;
    }

    // Admin/Staff xem tối đa 500 hóa đơn mới nhất.
    [HttpGet("/api/admin/invoices")]
    [Authorize(Roles = ApiPolicies.StaffRoles)]
    public async Task<ActionResult<IReadOnlyList<InvoiceResponse>>>GetAdminInvoices(CancellationToken cancellationToken)
    {
        var result = await _invoiceService.GetAdminInvoicesAsync(cancellationToken);

        return Ok(result);
    }

    // Customer xem tối đa 200 hóa đơn của chính mình.
    [HttpGet("/api/client/invoices")]
    [Authorize(Roles = "Customer")]
    public async Task<ActionResult<IReadOnlyList<InvoiceResponse>>>GetMyInvoices(CancellationToken cancellationToken)
    {
        var result = await _invoiceService.GetMyInvoicesAsync(cancellationToken);

        return Ok(result);
    }
}