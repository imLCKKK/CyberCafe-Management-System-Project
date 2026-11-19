using System.Security.Claims;
using CyberManagement.API.Security;
using CyberManagement.Application.DTOs.Computers;
using CyberManagement.Application.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CyberManagement.API.Controller;

[ApiController]
[Route("api/admin")]
public sealed class ComputersController(IComputerService computers) : ControllerBase
{
    [HttpGet("computers")]
    [Authorize(Policy = ApiPolicies.ComputerView)]
    public async Task<IActionResult> GetComputers(CancellationToken cancellationToken) =>
        Ok(await computers.GetComputersAsync(cancellationToken));

    [HttpGet("computer-types")]
    [Authorize(Policy = ApiPolicies.ComputerView)]
    public async Task<IActionResult> GetComputerTypes(CancellationToken cancellationToken) =>
        Ok(await computers.GetComputerTypesAsync(cancellationToken));

    [HttpGet("computer-rooms")]
    [Authorize(Policy = ApiPolicies.ComputerView)]
    public async Task<IActionResult> GetComputerRooms(CancellationToken cancellationToken) =>
        Ok(await computers.GetComputerRoomsAsync(cancellationToken));

    [HttpPatch("computers/{computerId}/status")]
    [Authorize(Policy = ApiPolicies.ComputerStatusUpdate)]
    public async Task<IActionResult> UpdateStatus(
        int computerId, [FromBody] UpdateComputerStatusCommand command, CancellationToken cancellationToken) =>
        Ok(await computers.UpdateStatusAsync(computerId, command, cancellationToken));

    [HttpPost("computers")]
    [Authorize(Policy = "permission:computer.manage")]
    public async Task<IActionResult> CreateComputer(SaveComputerCommand command, CancellationToken cancellationToken)
    {
        var saved = await computers.SaveComputerAsync(null, command, cancellationToken);
        return Created("/api/admin/computers", saved);
    }

    [HttpPut("computers/{computerId:int}")]
    [Authorize(Policy = "permission:computer.manage")]
    public async Task<IActionResult> UpdateComputer(int computerId, SaveComputerCommand command, CancellationToken cancellationToken) =>
        Ok(await computers.SaveComputerAsync(computerId, command, cancellationToken));

    [HttpDelete("computers/{computerId:int}")]
    [Authorize(Policy = "permission:computer.manage")]
    public async Task<IActionResult> DeleteComputer(int computerId, CancellationToken cancellationToken)
    {
        await computers.DeleteComputerAsync(computerId, cancellationToken);
        return NoContent();
    }

    [HttpPost("computer-types")]
    [Authorize(Policy = "permission:computer.manage")]
    public async Task<IActionResult> CreateType(SaveComputerTypeCommand command, CancellationToken cancellationToken)
    {
        var saved = await computers.SaveComputerTypeAsync(null, command, cancellationToken);
        return Created("/api/admin/computer-types", saved);
    }

    [HttpPut("computer-types/{typeId:int}")]
    [Authorize(Policy = "permission:computer.manage")]
    public async Task<IActionResult> UpdateType(int typeId, SaveComputerTypeCommand command, CancellationToken cancellationToken) =>
        Ok(await computers.SaveComputerTypeAsync(typeId, command, cancellationToken));

    [HttpDelete("computer-types/{typeId:int}")]
    [Authorize(Policy = "permission:computer.manage")]
    public async Task<IActionResult> DeleteType(int typeId, CancellationToken cancellationToken)
    {
        await computers.DeleteComputerTypeAsync(typeId, cancellationToken);
        return NoContent();
    }

    [HttpGet("computers/{computerId:int}/maintenances")]
    [Authorize(Policy = "permission:maintenance.view")]
    public async Task<IActionResult> GetMaintenances(int computerId, CancellationToken cancellationToken) =>
        Ok(await computers.GetMaintenancesAsync(computerId, cancellationToken));

    [HttpPost("computers/{computerId:int}/maintenances")]
    [Authorize(Policy = "permission:maintenance.manage")]
    public async Task<IActionResult> StartMaintenance(int computerId, SaveMaintenanceCommand command, CancellationToken cancellationToken)
    {
        if (User.HasClaim("account_type", "Customer") || !int.TryParse(User.FindFirstValue("sub"), out var employeeId))
            return Forbid();
        var saved = await computers.StartMaintenanceAsync(computerId, employeeId, command, cancellationToken);
        return Created($"/api/admin/computers/{computerId}/maintenances", saved);
    }

    [HttpPut("computers/{computerId:int}/maintenances/{maintenanceId:int}")]
    [Authorize(Policy = "permission:maintenance.manage")]
    public async Task<IActionResult> UpdateMaintenance(
        int computerId, int maintenanceId, SaveMaintenanceCommand command, CancellationToken cancellationToken) =>
        Ok(await computers.UpdateMaintenanceAsync(computerId, maintenanceId, command, cancellationToken));

    [HttpPost("computers/{computerId:int}/maintenances/{maintenanceId:int}/complete")]
    [Authorize(Policy = "permission:maintenance.manage")]
    public async Task<IActionResult> CompleteMaintenance(int computerId, int maintenanceId, CancellationToken cancellationToken) =>
        Ok(await computers.CompleteMaintenanceAsync(computerId, maintenanceId, cancellationToken));
}
