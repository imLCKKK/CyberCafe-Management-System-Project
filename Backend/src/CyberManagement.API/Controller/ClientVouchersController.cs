using System.Globalization;
using System.Security.Claims;
using CyberManagement.Application.DTOs.Vouchers;
using CyberManagement.Application.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CyberManagement.API.Controller;

[ApiController]
[Route("api/client/vouchers")]
[Authorize(Roles = "Customer")]
public sealed class ClientVouchersController(IVoucherService vouchers) : ControllerBase
{
    // Chỉ tính thử; customerId luôn lấy từ danh tính đăng nhập.
    [HttpPost("preview")]
    [ProducesResponseType<VoucherPreviewDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<VoucherPreviewDto>> Preview([FromBody] PreviewVoucherRequest request,
        CancellationToken cancellationToken) =>
        Ok(await vouchers.PreviewAsync(request, OwnCustomerId(), cancellationToken));

    private int OwnCustomerId()
    {
        var claim = User.FindFirstValue("customer_id");
        if (User.FindFirstValue("account_type") != "Customer"
            || !int.TryParse(claim, NumberStyles.None, CultureInfo.InvariantCulture, out var id)
            || id <= 0 || User.FindFirstValue("sub") != claim)
            throw new UnauthorizedAccessException("Danh tính khách hàng không hợp lệ.");
        return id;
    }
}
