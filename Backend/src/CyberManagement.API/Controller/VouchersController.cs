using CyberManagement.Application.DTOs.Vouchers;
using CyberManagement.Application.DTOs.Realtime;
using CyberManagement.Application.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CyberManagement.API.Controller;

[ApiController]
[Route("api/admin/vouchers")]
[Authorize]
[ProducesResponseType(StatusCodes.Status400BadRequest)]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
[ProducesResponseType(StatusCodes.Status403Forbidden)]
[ProducesResponseType(StatusCodes.Status404NotFound)]
[ProducesResponseType(StatusCodes.Status409Conflict)]
public sealed class VouchersController(IVoucherService vouchers, IRealtimePublisher publisher) : ControllerBase
{
    // Quyền đọc và quyền quản lý được kiểm tra riêng.
    [HttpGet]
    [Authorize(Policy = "permission:promotion.view")]
    [ProducesResponseType<IReadOnlyList<VoucherDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<VoucherDto>>> GetAll(CancellationToken cancellationToken) =>
        Ok(await vouchers.GetAllAsync(cancellationToken));

    [HttpGet("{id:int}")]
    [Authorize(Policy = "permission:promotion.view")]
    [ProducesResponseType<VoucherDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<VoucherDto>> GetById(int id, CancellationToken cancellationToken) =>
        Ok(await vouchers.GetByIdAsync(id, cancellationToken));

    [HttpPost]
    [Authorize(Policy = "permission:promotion.manage")]
    [ProducesResponseType<VoucherDto>(StatusCodes.Status201Created)]
    public async Task<ActionResult<VoucherDto>> Create([FromBody] SaveVoucherRequest request,
        CancellationToken cancellationToken)
    {
        var result = await vouchers.CreateAsync(request, cancellationToken);
        await PublishChangedAsync(result.Id);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpPut("{id:int}")]
    [Authorize(Policy = "permission:promotion.manage")]
    [ProducesResponseType<VoucherDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<VoucherDto>> Update(int id, [FromBody] SaveVoucherRequest request,
        CancellationToken cancellationToken)
    {
        var result = await vouchers.UpdateAsync(id, request, cancellationToken);
        await PublishChangedAsync(result.Id);
        return Ok(result);
    }

    // Tắt voucher thay cho xóa lịch sử.
    [HttpPut("{id:int}/status")]
    [Authorize(Policy = "permission:promotion.manage")]
    [ProducesResponseType<VoucherDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<VoucherDto>> UpdateStatus(int id, [FromBody] UpdateVoucherStatusRequest request,
        CancellationToken cancellationToken)
    {
        var result = await vouchers.UpdateStatusAsync(id, request, cancellationToken);
        await PublishChangedAsync(result.Id);
        return Ok(result);
    }

    // Management services return after commit; cancellation must not suppress a committed change.
    private Task PublishChangedAsync(int id) =>
        publisher.PublishToPermissionAsync("promotion.view",
            new RealtimeEvent(Guid.NewGuid(), RealtimeEvent.PromotionChanged, id, DateTimeOffset.UtcNow),
            CancellationToken.None);
}
