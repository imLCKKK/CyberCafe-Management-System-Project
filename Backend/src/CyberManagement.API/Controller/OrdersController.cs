using System.Globalization;
using System.Security.Claims;
using CyberManagement.Application.DTOs.Orders;
using CyberManagement.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CyberManagement.API.Controller;

[ApiController]
[Route("api")]
public sealed class OrdersController(OrderService orders) : ControllerBase
{
    [HttpPost("client/orders")]
    [Authorize(Roles = "Customer")]
    public async Task<IActionResult> CreateOwn(CreateOrderRequest request, CancellationToken ct)
    {
        var actor = Customer();
        return Result(await orders.CreateAsync(actor, new(actor.Id, request.GamingSessionId,
            request.Items, request.PaymentMethod, request.VoucherCode), Key(), ct), created: true);
    }

    [HttpPost("admin/orders")]
    [Authorize(Policy = "permission:order.create")]
    public async Task<IActionResult> Create(StaffCreateOrderRequest request, CancellationToken ct) =>
        Result(await orders.CreateAsync(Staff(), request, Key(), ct), created: true);

    [HttpGet("client/orders")]
    [Authorize(Roles = "Customer")]
    public async Task<IActionResult> GetOwn(CancellationToken ct, int page = 1, int pageSize = 20) =>
        Ok(await orders.ListAsync(Customer(), page, pageSize, null, null, ct));

    [HttpGet("client/orders/{orderId:int}")]
    [Authorize(Roles = "Customer")]
    public async Task<IActionResult> GetOwnById(int orderId, CancellationToken ct) =>
        Ok(await orders.GetAsync(Customer(), orderId, ct));

    [HttpGet("admin/orders")]
    [Authorize(Policy = "permission:order.view")]
    public async Task<IActionResult> GetAll(CancellationToken ct, int page = 1, int pageSize = 20,
        string? status = null, int? customerId = null) =>
        Ok(await orders.ListAsync(Staff(), page, pageSize, status, customerId, ct));

    [HttpGet("admin/orders/{orderId:int}")]
    [Authorize(Policy = "permission:order.view")]
    public async Task<IActionResult> GetById(int orderId, CancellationToken ct) =>
        Ok(await orders.GetAsync(Staff(), orderId, ct));

    [HttpPost("admin/orders/{orderId:int}/items/{detailId:int}/confirm")]
    [Authorize(Policy = "permission:order.status.update")]
    public async Task<IActionResult> Confirm(int orderId, int detailId, OrderVersionRequest request, CancellationToken ct) =>
        Result(await orders.ConfirmAsync(Staff(), orderId, detailId, request, Key(), ct));

    [HttpPost("admin/orders/{orderId:int}/cash-collections")]
    [Authorize(Policy = "permission:payment.create")]
    public async Task<IActionResult> Collect(int orderId, CashCollectionRequest request, CancellationToken ct) =>
        Result(await orders.CollectCashAsync(Staff(), orderId, request, Key(), ct));

    private IActionResult Result(OrderResult result, bool created = false)
    {
        if (result.Replayed) Response.Headers["Idempotency-Replayed"] = "true";
        if (!created || result.Replayed) return Ok(result.Order);
        var audience = User.FindFirstValue("account_type") == "Customer" ? "client" : "admin";
        return Created($"/api/{audience}/orders/{result.Order.Id}", result.Order);
    }
    private string Key()
    {
        var values = Request.Headers["Idempotency-Key"];
        if (values.Count != 1) throw new System.ComponentModel.DataAnnotations.ValidationException("Cần một Idempotency-Key.");
        return values[0]!;
    }
    private OrderActor Customer()
    {
        var claim = User.FindFirstValue("customer_id");
        if (User.FindFirstValue("account_type") != "Customer" || User.FindFirstValue("sub") != claim)
            throw new UnauthorizedAccessException("Danh tính khách không hợp lệ.");
        return new("Customer", Id(claim));
    }
    private OrderActor Staff()
    {
        if (User.FindFirstValue("account_type") is not null || User.FindFirstValue("role") is null or "Customer")
            throw new UnauthorizedAccessException("Danh tính nhân viên không hợp lệ.");
        return new("Staff", Id(User.FindFirstValue("sub")));
    }
    private static int Id(string? value) => int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var id)
        && id > 0 ? id : throw new UnauthorizedAccessException("ID tài khoản không hợp lệ.");
}
