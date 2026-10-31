using System.ComponentModel.DataAnnotations;
using CyberManagement.API.Security;
using CyberManagement.Application.DTOs;
using CyberManagement.Application.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CyberManagement.API.Controller;

[ApiController]
[Route("api/admin/inventory")]
//yêu cầu tài khoản đang hoạt động và thuộc Admin/Staff.
[Authorize(Policy = ApiPolicies.ActiveAccount)]
[Authorize(Roles = ApiPolicies.StaffRoles)] 
public class InventoryController : ControllerBase
{
    private readonly IInventoryService _inventoryService;

    public InventoryController(IInventoryService inventoryService)
    {
        _inventoryService = inventoryService;
    }

    // GET /api/admin/inventory
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<InventoryItemDto>>>GetInventory(CancellationToken cancellationToken)
    {
        var result = await _inventoryService.GetInventoryAsync(cancellationToken);

        return Ok(result);
    }

    // GET /api/admin/inventory/movements
    [HttpGet("movements")]
    public async Task<ActionResult<IReadOnlyList<InventoryMovementDto>>>GetMovements(CancellationToken cancellationToken)
    {
        var result = await _inventoryService.GetMovementsAsync(cancellationToken);

        return Ok(result);
    }

    // POST /api/admin/inventory/movements
    [HttpPost("movements")]
    public async Task<ActionResult<InventoryMovementDto>>CreateMovement([FromBody] CreateInventoryMovementRequest request,CancellationToken cancellationToken)
    {
        var result = await _inventoryService.CreateMovementAsync(request, cancellationToken);

        return StatusCode(StatusCodes.Status201Created, result);
    }

    // PUT /api/admin/products/{id}/status
    [HttpPut("/api/admin/products/{id:long}/status")]
    public async Task<ActionResult<InventoryItemDto>>UpdateProductStatus(
        [FromRoute][Range(typeof(long), "1", "9223372036854775807")]long id, 
        [FromBody] UpdateProductStatusRequest request,CancellationToken cancellationToken)
    {
        var result = await _inventoryService.UpdateProductStatusAsync(id,request,cancellationToken);

        return Ok(result);
    }
}