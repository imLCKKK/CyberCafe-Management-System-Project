using CyberManagement.Application.DTOs;
using CyberManagement.Domain.Entities;

namespace CyberManagement.Application.Services;

// Ánh xạ dữ liệu đọc cho bảng kê, không phụ thuộc service xử lý đơn.
internal static class OrderReadMapper
{
    internal static string StatusOf(Order order)
    {
        var items = order.OrderDetails;
        if (items.Count == 0) return "Pending";
        if (items.All(i => i.Status == "Cancelled")) return "Cancelled";
        if (items.All(i => i.Status != "Pending")) return "Confirmed";
        return items.Any(i => i.Status != "Pending") ? "Mixed" : "Pending";
    }

    // Chuyển yêu cầu hủy sang DTO, giữ thông tin người yêu cầu, trạng thái và người/thời gian xử lý.
    private static OrderItemCancelRequestDTO MapRequest(OrderItemCancelRequest r) => new()
    {
        RequestId = r.RequestId,
        OrderDetailId = r.OrderDetailId,
        RequestedByCustomerId = r.RequestedByCustomerId,
        Status = r.Status,
        RequestedAt = r.RequestedAt,
        HandledAt = r.HandledAt,
        HandledByAccountId = r.HandledByAccountId
    };

    // Chuyển đơn cùng các món và yêu cầu hủy sang DTO; tính trạng thái toàn đơn từ từng món.
    // TotalAmount giữ tổng lúc đặt, không tự trừ khoản hoàn; số tiền thực chi phải đối chiếu giao dịch ví.
    public static OrderDTOs Map(Order order) => new()
    {
        OrderId = order.OrderId, CustomerId = order.CustomerId, SessionId = order.SessionId,
        EmployeeId = order.EmployeeId, InvoiceId = order.InvoiceId, OrderTime = order.OrderTime,
        Status = StatusOf(order), TotalAmount = order.TotalAmount,
        Items = order.OrderDetails.Select(i => new OrderDetailsDTOs
        {
            OrderDetailId = i.OrderDetailId, ProductId = i.ProductId, ProductName = i.Product.ProductName,
            Quantity = i.Quantity, UnitPrice = i.UnitPrice, SubTotal = i.Quantity * i.UnitPrice,
            Status = i.Status, ConfirmedAt = i.ConfirmedAt, ConfirmedByAccountId = i.ConfirmedByAccountId,
            CancelledAt = i.CancelledAt, CancelledByAccountId = i.CancelledByAccountId, CancelReason = i.CancelReason,
            CancellationRequests = i.CancellationRequests.Select(MapRequest).ToList()
        }).ToList()
    };
}
