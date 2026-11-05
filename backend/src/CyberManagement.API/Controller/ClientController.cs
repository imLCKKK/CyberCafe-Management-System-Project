using System.Security.Claims;
using CyberManagement.Application.DTOs.Customers;
using CyberManagement.Application.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CyberManagement.API.Controller;

// Khách đăng nhập (POST /api/auth/customer/login) xem dữ liệu CỦA CHÍNH MÌNH.
// Không có tham số id: customerId luôn lấy từ claim customer_id trong token, nên sửa ID trong request không đổi chủ dữ liệu.
[ApiController]
[Route("api/client")]
[Authorize(Roles = "Customer")]
public sealed class ClientController(IClientAccountService client) : ControllerBase
{
    [HttpGet("profile")]
    public async Task<ActionResult<CustomerDto>> Profile(CancellationToken cancellationToken) =>
        Ok(await client.GetProfileAsync(CurrentCustomerId(), cancellationToken));

    [HttpGet("wallet")]
    public async Task<ActionResult<ClientWalletDto>> Wallet(CancellationToken cancellationToken) =>
        Ok(await client.GetWalletAsync(CurrentCustomerId(), cancellationToken));

    private int CurrentCustomerId() =>
        int.TryParse(User.FindFirstValue("customer_id"), out var id)
            ? id
            : throw new UnauthorizedAccessException("Không xác định được khách hàng từ token.");
}
