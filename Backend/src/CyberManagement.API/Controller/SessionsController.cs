using CyberManagement.Application.DTOs.Computers;
using CyberManagement.Application.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CyberManagement.API.Controller;

[ApiController]
[Route("api/admin/sessions")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class SessionsController(ISessionService sessions) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = "permission:session.view")]
    public async Task<IActionResult> List(CancellationToken ct) => Ok(await sessions.GetSessionsAsync(ct));

    [HttpPost]
    [Authorize(Policy = "permission:session.start")]
    public async Task<IActionResult> Open(OpenSessionCommand command,
        [FromHeader(Name = "Idempotency-Key")] string key, CancellationToken ct)
    {
        RequireOneKey();
        return Ok(await sessions.OpenAsync(command, InvoiceIdentity.Employee(User), key, ct));
    }

    [HttpPost("{id:int}/close")]
    [Authorize(Policy = "permission:session.end")]
    [Authorize(Policy = "permission:invoice.create")]
    public async Task<IActionResult> Close(int id, CloseSessionCommand command,
        [FromHeader(Name = "Idempotency-Key")] string key, CancellationToken ct)
    {
        RequireOneKey();
        return Ok(await sessions.CloseAsync(id, InvoiceIdentity.Employee(User), key, ct));
    }

    private void RequireOneKey()
    {
        if (Request.Headers["Idempotency-Key"].Count != 1)
            throw new ArgumentException("Cần đúng một Idempotency-Key.");
    }
}

[ApiController]
[Route("api/client/home")]
[Authorize(Roles = "Customer")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class SessionHomeController(ISessionService sessions) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct) =>
        Ok(await sessions.GetHomeAsync(InvoiceIdentity.Customer(User), ct));
}
