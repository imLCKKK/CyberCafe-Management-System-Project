using CyberManagement.Application.DTOs.Employees;
using CyberManagement.Application.Exceptions;
using CyberManagement.Application.Services;
using CyberManagement.API.Realtime;
using System.IdentityModel.Tokens.Jwt;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using CyberManagement.API.Security;
using System.Text.RegularExpressions;

namespace CyberManagement.API.Controller;

[ApiController]
[Route("api/employees")]
[Authorize(Policy = ApiPolicies.ActiveAccount)]
public sealed class EmployeesController(
    EmployeesService employeesService,
    RealtimeAccountConnections realtimeAccountConnections) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = ApiPolicies.EmployeeView)]
    public async Task<ActionResult<IReadOnlyList<EmployeeDto>>> GetAllEmployees(
        CancellationToken cancellationToken)
    {
        var employees = await employeesService.GetAllEmployeesAsync(cancellationToken);
        return Ok(employees);
    }

    [HttpGet("{employeeId:int}")]
    [Authorize(Policy = ApiPolicies.EmployeeView)]
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
    [Authorize(Policy = ApiPolicies.EmployeeManage)]
    public async Task<ActionResult<EmployeeDto>> CreateEmployee(
        [FromBody] CreateEmployeeRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var currentEmployeeIdClaim = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
            if (string.IsNullOrEmpty(currentEmployeeIdClaim) || !int.TryParse(currentEmployeeIdClaim, out var currentEmployeeId))
                return Unauthorized(new { message = "Không thể xác định nhân viên hiện tại." });

            var key = ReadIdempotencyKey();
            var result = await employeesService.CreateEmployeeWithIdempotencyAsync(
                request, key, currentEmployeeId, cancellationToken);
            
            if (result.Replayed)
                Response.Headers["Idempotency-Replayed"] = "true";

            return result.Replayed
                ? Ok(result.Employee)
                : CreatedAtAction(nameof(GetEmployee), new { employeeId = result.Employee.EmployeeId }, result.Employee);
        }
        catch (ValidationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (InvalidOperationException ex) when (ex is not BusinessRuleException)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (ConflictException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }

    [HttpPut("{employeeId:int}")]
    [Authorize(Policy = ApiPolicies.EmployeeManage)]
    [ProducesResponseType<EmployeeDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<EmployeeDto>> UpdateEmployee(
        int employeeId,
        [FromBody] UpdateEmployeeRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var currentEmployeeIdClaim = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
            if (string.IsNullOrEmpty(currentEmployeeIdClaim) || !int.TryParse(currentEmployeeIdClaim, out var currentEmployeeId))
                return Unauthorized(new { message = "Không thể xác định nhân viên hiện tại." });

            var key = ReadIdempotencyKey();
            var result = await employeesService.UpdateEmployeeWithIdempotencyAsync(
                employeeId, request, key, currentEmployeeId, cancellationToken);
            MarkReplay(result.Replayed);
            return Ok(result.Employee);
        }
        catch (InvalidOperationException ex) when (ex is not BusinessRuleException)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("{employeeId:int}/lock")]
    [Authorize(Policy = ApiPolicies.EmployeeManage)]
    public async Task<ActionResult<EmployeeDto>> LockEmployee(
        int employeeId,
        CancellationToken cancellationToken)
    {
        try
        {
            var currentEmployeeIdClaim = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
            if (string.IsNullOrEmpty(currentEmployeeIdClaim) || !int.TryParse(currentEmployeeIdClaim, out var currentEmployeeId))
                return Unauthorized(new { message = "Không thể xác định nhân viên hiện tại." });

            var key = ReadIdempotencyKey();
            var result = await employeesService.LockEmployeeWithIdempotencyAsync(
                employeeId, key, currentEmployeeId, cancellationToken);
            MarkReplay(result.Replayed);
            await realtimeAccountConnections.RevokeEmployeeAsync(employeeId);
            return Ok(result.Employee);
        }
        catch (InvalidOperationException ex) when (ex is not BusinessRuleException)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("{employeeId:int}/unlock")]
    [Authorize(Policy = ApiPolicies.EmployeeManage)]
    public async Task<ActionResult<EmployeeDto>> UnlockEmployeeAsync(
        int employeeId,
        CancellationToken cancellationToken)
    {
        try
        {
            var currentEmployeeIdClaim = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
            if (string.IsNullOrEmpty(currentEmployeeIdClaim) || !int.TryParse(currentEmployeeIdClaim, out var currentEmployeeId))
                return Unauthorized(new { message = "Không thể xác định nhân viên hiện tại." });

            var key = ReadIdempotencyKey();
            var result = await employeesService.UnlockEmployeeWithIdempotencyAsync(
                employeeId, key, currentEmployeeId, cancellationToken);
            MarkReplay(result.Replayed);
            return Ok(result.Employee);
        }
        catch (InvalidOperationException ex) when (ex is not BusinessRuleException)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    private string ReadIdempotencyKey()
    {
        var raw = Request.Headers["Idempotency-Key"];
        var key = raw.Count == 1 ? raw[0] : null;
        if (key is null || !Regex.IsMatch(key, @"\A[A-Za-z0-9._:-]{8,100}\z"))
            throw new ValidationException("Idempotency-Key phải có 8–100 ký tự chữ, số, '.', '_', ':', '-'.");
        return key;
    }

    private void MarkReplay(bool replayed)
    {
        if (replayed)
            Response.Headers["Idempotency-Replayed"] = "true";
    }

    [HttpPost("{employeeId:int}/change-password")]
    [Authorize(Roles = "Admin,Manager,Cashier,Waiter,Technician")]
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

            if (currentEmployeeId != employeeId)
                return Forbid();

            var key = ReadIdempotencyKey();
            var replayed = await employeesService.ChangePasswordWithIdempotencyAsync(
                employeeId, request, key, currentEmployeeId, cancellationToken);
            MarkReplay(replayed);
            return NoContent();
        }
        catch (InvalidOperationException ex) when (ex is not BusinessRuleException)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}
