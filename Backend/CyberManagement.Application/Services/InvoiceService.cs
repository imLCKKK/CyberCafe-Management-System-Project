using CyberManagement.Application.DTOs;
using CyberManagement.Application.Exceptions;
using CyberManagement.Application.Interfaces;
using CyberManagement.Application.Interfaces.Repositories;
using CyberManagement.Application.Interfaces.Services;
using CyberManagement.Domain.Entities;

namespace CyberManagement.Application.Services;

public class InvoiceService : IInvoiceService
{
    private readonly IInvoiceRepository _invoices;
    private readonly IOrderRepository _orders;
    private readonly IOrderWorkflowRepository _workflow;
    private readonly ICurrentUser _user;

    // Nhận repository và danh tính đã xác thực để tra cứu hóa đơn/bảng kê.
    public InvoiceService(IInvoiceRepository invoices, IOrderRepository orders,
        IOrderWorkflowRepository workflow, ICurrentUser user)
    {
        _invoices = invoices;
        _orders = orders;
        _workflow = workflow;
        _user = user;
    }

    // Khách chỉ xem hóa đơn/phiên của mình; nhân viên/Admin đang hoạt động được tra cứu theo quyền quản lý.
    private void RequireAccess(long? customerId)
    {
        if (!_user.IsActive || _user.AccountId <= 0)
            throw new UnauthorizedAccessException("An active authenticated account is required.");
        if (_user.Role == "Customer" && _user.CustomerId is > 0 && _user.CustomerId == customerId) return;
        if ((_user.Role == "Staff" || _user.Role == "Admin") && _user.EmployeeId is > 0) return;
        throw new UnauthorizedAccessException("You cannot access this invoice/session.");
    }

    // Chuyển hóa đơn và chi tiết sang DTO; phương thức thanh toán chỉ lấy từ các Payment đã Completed.
    private static InvoiceDTOs Map(Invoice invoice) => new()
    {
        InvoiceId = invoice.InvoiceId, CustomerId = invoice.CustomerId,
        SessionId = invoice.SessionId, EmployeeId = invoice.EmployeeId,
        PromotionId = invoice.PromotionId, InvoiceDate = invoice.InvoiceDate,
        Subtotal = invoice.SubTotal, Discount = invoice.Discount,
        TotalAmount = invoice.TotalAmount, Status = invoice.Status,
        PaymentMethod = string.Join(", ", invoice.Payments.Where(p => p.Status == "Completed")
            .Select(p => p.Method).Distinct()),
        Items = invoice.InvoiceDetails.Select(i => new InvoiceDetailDTOs
        {
            InvoiceDetailId = i.InvoiceDetailId, ItemType = i.ItemType,
            Description = i.Description, Quantity = i.Quantity, UnitPrice = i.UnitPrice, Amount = i.Amount
        }).ToList()
    };

    // Chỉ cho nhân viên/Admin xem danh sách toàn bộ hóa đơn; chuyển dữ liệu repository trả về sang DTO.
    public async Task<IEnumerable<InvoiceDTOs>> GetAllAsync()
    {
        if (_user.Role == "Customer")
            throw new UnauthorizedAccessException("Staff access is required to list invoices.");
        RequireAccess(null);
        return (await _invoices.GetAllAsync()).Select(Map).ToList();
    }

    // Tìm hóa đơn theo ID, trả null nếu không có và kiểm tra quyền trước khi trả chi tiết.
    public async Task<InvoiceDTOs?> GetByIdAsync(long id)
    {
        var invoice = await _invoices.GetByIdAsync(id);
        if (invoice == null) return null;
        RequireAccess(invoice.CustomerId);
        return Map(invoice);
    }

    // Tìm đơn và kiểm tra quyền, sau đó tra InvoiceId liên kết; chưa có liên kết thì trả null.
    public async Task<InvoiceDTOs?> GetByOrderIdAsync(long orderId)
    {
        var order = await _orders.GetWithDetailsByIdAsync(orderId);
        if (order == null) return null;
        RequireAccess(order.CustomerId);
        return order.InvoiceId is > 0 ? await GetByIdAsync(order.InvoiceId.Value) : null;
    }

    // Bảng kê chỉ dùng để đọc dữ liệu, không trừ ví hoặc tạo thêm Payment/Invoice.
    // Tổng hợp bảng kê một phiên từ giao dịch ví thực tế và các đơn thuộc phiên đó.
    // Phiên Active là tạm tính; hàm không tự kết thúc phiên và không ghi thêm khoản thu.
    public Task<SessionStatementDTO?> GetSessionStatementAsync(long sessionId) =>
        _workflow.ExecuteAsync<SessionStatementDTO?>(async () =>
        {
            // 1. Tìm phiên và kiểm tra quyền truy cập trước khi nạp dữ liệu bảng kê.
            var snapshot = await _workflow.GetSessionAsync(sessionId);
            if (snapshot == null) return null;
            RequireAccess(snapshot.CustomerId);
            // 2. Khóa khách trước rồi đến phiên, cùng thứ tự với luồng đặt món để đọc số liệu nhất quán.
            _ = await _workflow.GetCustomerForUpdateAsync(snapshot.CustomerId)
                ?? throw new BusinessRuleException("Customer does not exist.");
            var session = await _workflow.GetSessionForUpdateAsync(sessionId);
            if (session == null) return null;
            if (session.CustomerId != snapshot.CustomerId)
                throw new BusinessRuleException("Session ownership changed. Retry the operation.");
            RequireAccess(session.CustomerId);
            // 3. Lấy giao dịch thuộc phiên và đảm bảo không có giao dịch của khách khác bị đưa nhầm vào.
            var transactions = (await _workflow.GetSessionWalletTransactionsAsync(sessionId)).ToList();
            if (transactions.Any(t => t.CustomerId != session.CustomerId))
                throw new BusinessRuleException("Session ledger contains another customer's transactions.");
            // 4. Lọc các đơn đúng SessionId; không đưa đơn của phiên khác vào bảng kê.
            var orders = new List<OrderDTOs>();
            var candidates = await _orders.GetByCustomerIdAsync(session.CustomerId);
            foreach (var candidate in candidates.Where(o => o.SessionId == sessionId))
            {
                var full = await _orders.GetWithDetailsByIdAsync(candidate.OrderId);
                if (full == null) continue;
                RequireAccess(full.CustomerId);
                if (full.CustomerId != session.CustomerId || full.SessionId != sessionId)
                    throw new BusinessRuleException("Order ownership/session changed. Retry the operation.");
                orders.Add(OrderReadMapper.Map(full));
            }
            // 5. Tổng hợp tiền chơi và tiền món đã trừ/hoàn thực tế; đổi dấu khoản trừ để hiển thị số tiền dương.
            return new SessionStatementDTO
            {
                SessionId = sessionId, CustomerId = session.CustomerId, Status = session.Status,
                GamingCharged = -transactions.Where(t => t.Type == "GamingCharge").Sum(t => t.Amount),
                FoodDebited = -transactions.Where(t => t.Type == "OrderDebit").Sum(t => t.Amount),
                FoodRefunded = transactions.Where(t => t.Type == "OrderItemRefund").Sum(t => t.Amount),
                Orders = orders
            };
        });
}
