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
}
