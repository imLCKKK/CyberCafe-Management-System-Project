using CyberManagement.Application.DTOs.Employees;
using CyberManagement.Application.Services;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CyberManagement.API.Controller;

[ApiController]
[Route("api/employees")]
[Authorize(Roles = "Admin")]
public sealed class EmployeesController(EmployeesService employeesService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<EmployeeDto>>> GetAllEmployees(
        CancellationToken cancellationToken)
    {
        var employees = await employeesService.GetAllEmployeesAsync(cancellationToken);
        return Ok(employees);
    }

    [HttpGet("{employeeId:int}")]
    public async Task<ActionResult<EmployeeDto>> GetEmployee(
        int employeeId,
        CancellationToken cancellationToken)
    {
        var employee = await employeesService.GetEmployeeByIdAsync(employeeId, cancellationToken);
        if (employee is null)
            return NotFound(new { message = "Nhân viên không tồn tại." });

        return Ok(employee);
    }

    [HttpPost]
    public async Task<ActionResult<EmployeeDto>> CreateEmployee(
        [FromBody] CreateEmployeeRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var employee = await employeesService.CreateEmployeeAsync(request, cancellationToken);
            return CreatedAtAction(nameof(GetEmployee), new { employeeId = employee.EmployeeId }, employee);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPut("{employeeId:int}")]
    public async Task<ActionResult<EmployeeDto>> UpdateEmployee(
        int employeeId,
        [FromBody] UpdateEmployeeRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var employee = await employeesService.UpdateEmployeeAsync(
                employeeId, request, cancellationToken);
            return Ok(employee);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("{employeeId:int}/lock")]
    public async Task<ActionResult<EmployeeDto>> LockEmployee(
        int employeeId,
        CancellationToken cancellationToken)
    {
        try
        {
            var employee = await employeesService.LockEmployeeAsync(employeeId, cancellationToken);
            return Ok(employee);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("{employeeId:int}/unlock")]
    public async Task<ActionResult<EmployeeDto>> UnlockEmployeeAsync(
        int employeeId,
        CancellationToken cancellationToken)
    {
        try
        {
            var employee = await employeesService.UnlockEmployeeAsync(employeeId, cancellationToken);
            return Ok(employee);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("{employeeId:int}/change-password")]
    [Authorize]
    public async Task<IActionResult> ChangePassword(
        int employeeId,
        [FromBody] ChangePasswordRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var currentEmployeeIdClaim = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
            if (string.IsNullOrEmpty(currentEmployeeIdClaim) || !int.TryParse(currentEmployeeIdClaim, out var currentEmployeeId))
                return Unauthorized(new { message = "Không thể xác định nhân viên hiện tại." });

            if (currentEmployeeId != employeeId && User.FindFirst("role")?.Value != "Admin")
                return Forbid();

            await employeesService.ChangePasswordAsync(employeeId, request, cancellationToken);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}
