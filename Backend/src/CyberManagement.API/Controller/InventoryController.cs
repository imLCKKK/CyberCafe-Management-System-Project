using CyberManagement.Application.DTOs.Inventory;
using CyberManagement.Application.Interfaces.Repositories;
using CyberManagement.Application.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;

namespace CyberManagement.API.Controller;

[ApiController]
[Route("api/admin/inventory")]
[Authorize]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
[ProducesResponseType(StatusCodes.Status403Forbidden)]
public sealed class InventoryController : ControllerBase
{
    private readonly IInventoryQueries _queries;
    private readonly IInventoryService _inventoryService;
    private readonly IAuthorizationService _authorizationService;

    public InventoryController(IInventoryQueries queries, IInventoryService inventoryService, IAuthorizationService authorizationService)
    {
        _queries = queries;
        _inventoryService = inventoryService;
        _authorizationService = authorizationService;
    }

    // Xem danh sách tồn kho.
    [HttpGet]
    [Authorize(Policy = "permission:stockreceipt.view")]
    [ProducesResponseType<IReadOnlyList<InventoryItemDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<InventoryItemDto>>>GetInventory(CancellationToken cancellationToken)
    {
        var result = await _queries.GetInventoryAsync(cancellationToken);

        return Ok(result);
    }

    // Xem tối đa 500 biến động mới nhất.
    [HttpGet("movements")]
    [Authorize(Policy = "permission:stockreceipt.view")]
    [ProducesResponseType<IReadOnlyList<InventoryMovementDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<InventoryMovementDto>>>GetMovements(CancellationToken cancellationToken)
    {
        var result = await _inventoryService.GetMovementsAsync(cancellationToken);

        return Ok(result);
    }

    // Nhập hoặc điều chỉnh tồn thủ công.
    [HttpPost("movements")]
    [ProducesResponseType<InventoryMovementDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<InventoryMovementDto>> CreateMovement(
        [FromBody] CreateInventoryMovementRequest request,
        CancellationToken cancellationToken)
    {
        // Chọn quyền theo đúng loại thao tác.
        var policy = request.Type switch
        {
            "Receive" => "permission:stockreceipt.create",
            "Adjust" => "permission:inventory.adjust",
            _ => throw new ValidationException(
                "Thao tác thủ công chỉ hỗ trợ Receive hoặc Adjust.")
        };

        var authorization = await _authorizationService.AuthorizeAsync(User,null,policy);

        if (!authorization.Succeeded)
        {
            return Forbid();
        }

        // Lấy nhân viên từ JWT, không nhận ID do client gửi.
        var subject = User.FindFirstValue("sub");

        if (!int.TryParse(subject, out var employeeId) || employeeId <= 0)
        {
            return Forbid();
        }

        var result = await _inventoryService.CreateMovementAsync(request, employeeId, cancellationToken);

        return Created("/api/admin/inventory/movements", result);
    }

    // Bật hoặc tắt trạng thái bán của sản phẩm.
    [HttpPut("/api/admin/products/{id:int}/status")]
    [Authorize(Policy = "permission:product.manage")]
    [ProducesResponseType<InventoryItemDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<InventoryItemDto>> UpdateProductStatus(int id, [FromBody] UpdateProductStatusRequest request, CancellationToken cancellationToken)
    {
        var result = await _inventoryService.UpdateProductStatusAsync(id, request, cancellationToken);

        return Ok(result);
    }
}
