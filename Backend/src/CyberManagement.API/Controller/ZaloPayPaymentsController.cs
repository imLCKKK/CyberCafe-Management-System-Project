using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Security.Claims;
using System.Text.Json.Serialization;
using CyberManagement.Application.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CyberManagement.API.Controller;

[ApiController]
[Route("api")]
public sealed class ZaloPayPaymentsController(IZaloPayPaymentService payments, ILogger<ZaloPayPaymentsController> logger) : ControllerBase
{
    [Authorize(Roles = "Customer")]
    [HttpGet("customers/me/payments/zalopay/config")]
    public async Task<IActionResult> GetConfiguration(CancellationToken cancellationToken)
    {
        // This read also enforces current customer status; no secrets are exposed.
        _ = await payments.GetOwnPageAsync(OwnCustomerId(), 1, 1, "TopUp", cancellationToken);
        return Ok(payments.GetConfiguration());
    }

    [Authorize(Roles = "Customer")]
    [HttpGet("customers/me/payments/zalopay")]
    public async Task<IActionResult> GetOwnPage(CancellationToken cancellationToken, int page = 1, int pageSize = 20, string? purpose = null) =>
        Ok(await payments.GetOwnPageAsync(OwnCustomerId(), page, pageSize, purpose, cancellationToken));

    [Authorize(Roles = "Customer")]
    [HttpPost("customers/me/payments/zalopay/topups")]
    public async Task<IActionResult> CreateTopUp([FromBody] CreateZaloPayTopUpCommand command, CancellationToken cancellationToken) =>
        Ok(await payments.CreateTopUpAsync(OwnCustomerId(), command.AmountVnd, RequestKey(), cancellationToken));

    [Authorize(Roles = "Customer")]
    [HttpPost("customers/me/payments/zalopay/orders/{orderId:int}")]
    public async Task<IActionResult> CreateOrder(int orderId, [FromBody] CreateZaloPayOrderCommand command, CancellationToken cancellationToken) =>
        Ok(await payments.CreateOrderAsync(OwnCustomerId(), orderId, RequestKey(), cancellationToken));

    [Authorize(Roles = "Customer")]
    [HttpGet("customers/me/payments/zalopay/{intentId:int}")]
    public async Task<IActionResult> GetOwn(int intentId, CancellationToken cancellationToken) =>
        Ok(await payments.GetOwnAsync(OwnCustomerId(), intentId, cancellationToken));

    [Authorize(Roles = "Customer")]
    [HttpPost("customers/me/payments/zalopay/{intentId:int}/sync")]
    public async Task<IActionResult> Sync(int intentId, CancellationToken cancellationToken) =>
        Ok(await payments.SyncOwnAsync(OwnCustomerId(), intentId, cancellationToken));

    [AllowAnonymous]
    [RequestSizeLimit(131_072)]
    [HttpPost("payments/zalopay/callback")]
    public async Task<IActionResult> Callback([FromBody] ZaloPayCallbackCommand command, CancellationToken cancellationToken)
    {
        try
        {
            var code = await payments.HandleCallbackAsync(command.Data, command.Mac, command.Type, cancellationToken);
            return Ok(new { return_code = code, return_message = code == 1 ? "Recorded" : code == 2 ? "Invalid callback" : "Retry later" });
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception)
        {
            // Do not log raw callback bodies, signatures, payment URLs or secret configuration.
            logger.LogWarning("ZaloPay callback could not be committed. Query will recover the saved intent.");
            return Ok(new { return_code = 0, return_message = "Retry later" });
        }
    }

    private int OwnCustomerId()
    {
        var claim = User.FindFirstValue("customer_id");
        if (User.FindFirstValue("account_type") != "Customer" || User.FindFirstValue("sub") != claim
            || !int.TryParse(claim, NumberStyles.None, CultureInfo.InvariantCulture, out var id) || id <= 0)
            throw new UnauthorizedAccessException("Danh tính khách hàng không hợp lệ.");
        return id;
    }

    private Guid RequestKey()
    {
        var header = Request.Headers["Idempotency-Key"];
        if (header.Count != 1 || !Guid.TryParseExact(header[0], "D", out var key) || key == Guid.Empty)
            throw new ValidationException("Idempotency-Key phải là UUID hợp lệ cho mỗi ý định thanh toán.");
        return key;
    }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CreateZaloPayTopUpCommand(decimal AmountVnd);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CreateZaloPayOrderCommand;
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ZaloPayCallbackCommand(string Data, string Mac, int Type);
