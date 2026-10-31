using System.ComponentModel.DataAnnotations;
using CyberManagement.API.Security;
using CyberManagement.Application.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CyberManagement.API.Controller;

// Tất cả endpoint bên dưới yêu cầu đăng nhập và tài khoản đang hoạt động: thiếu đăng nhập trả 401, thiếu quyền trả 403.
// Các ID truyền vào phải là số nguyên dương; dữ liệu không hợp lệ trả 400 trước khi xử lý nghiệp vụ.
[NonController]
[Route("api/[controller]")]
[ApiController]
[Authorize(Policy = ApiPolicies.ActiveAccount)]
public class InvoicesController(IInvoiceService invoiceService) : ControllerBase
{
    // GET /api/invoices
    // Quyền: Staff/Admin. Không cần body hoặc tham số.
    // Trả 200 kèm danh sách hóa đơn đã lưu; khách không được xem toàn bộ danh sách.
    [HttpGet]
    [Authorize(Roles = ApiPolicies.StaffRoles)]
    public async Task<IActionResult> GetAll() => Ok(await invoiceService.GetAllAsync());

    // GET /api/invoices/{id}
    // Nhận ID hóa đơn từ URL. Khách chỉ xem hóa đơn của mình; Staff/Admin được tra cứu.
    // Trả 200 kèm hóa đơn và chi tiết; không tìm thấy trả 404, không có quyền sở hữu trả 403.
    [HttpGet("{id:long}")]
    public async Task<IActionResult> GetById([Range(typeof(long), "1", "9223372036854775807")] long id)
    {
        var invoice = await invoiceService.GetByIdAsync(id);
        return invoice == null ? NotFound() : Ok(invoice);
    }

    // GET /api/invoices/order/{orderId}
    // Nhận ID đơn từ URL; service kiểm tra quyền xem đơn rồi tra InvoiceId đã liên kết.
    // Trả 200 kèm hóa đơn; đơn không tồn tại hoặc chưa liên kết hóa đơn trả 404. Không tự tạo hóa đơn.
    [HttpGet("order/{orderId:long}")]
    public async Task<IActionResult> GetInvoicesByOrder([Range(typeof(long), "1", "9223372036854775807")] long orderId)
    {
        var invoice = await invoiceService.GetByOrderIdAsync(orderId);
        return invoice == null ? NotFound() : Ok(invoice);
    }

    // GET /api/invoices/session/{sessionId}/statement
    // Nhận ID phiên từ URL. Khách chỉ xem phiên của mình; Staff/Admin được tra cứu.
    // Trả 200 kèm đơn và tổng tiền chơi, tiền món đã trừ/hoàn thực tế; không thấy phiên trả 404.
    // Phiên Active là tạm tính; endpoint không kết thúc phiên, tạo hóa đơn hoặc thu thêm tiền.
    [HttpGet("session/{sessionId:long}/statement")]
    public async Task<IActionResult> GetSessionStatement([Range(typeof(long), "1", "9223372036854775807")] long sessionId)
    {
        var statement = await invoiceService.GetSessionStatementAsync(sessionId);
        return statement == null ? NotFound() : Ok(statement);
    }
}
