using CyberManagement.Application.DTOs.Inventory;
using CyberManagement.Application.Interfaces.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CyberManagement.API.Controller;

[ApiController]
[Route("api/admin/inventory")]
[Authorize(Policy = "permission:stockreceipt.view")]
public sealed class InventoryController(IInventoryQueries queries) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<InventoryItemDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IReadOnlyList<InventoryItemDto>>> GetInventory(
        CancellationToken cancellationToken) =>
        Ok(await queries.GetInventoryAsync(cancellationToken));
}
