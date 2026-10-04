using System;
using System.Collections.Generic;
using System.Text;

namespace CyberManagement.Application.DTOs
{
    public class OrderDTOs
    {
        public int OrderId { get; set; }
        public int? CustomerId { get; set; }
        public int? SessionId { get; set; }
        public int EmployeeId { get; set; }
        public int? InvoiceId { get; set; }

        public DateTime OrderTime { get; set; }
        public string Status { get; set; } = string.Empty;
        public decimal TotalAmount { get; set; }

        public List<OrderDetailsDTOs> Items { get; set; } = new();

        //Nếu muốn front-end hiển thị lịch sử trạng thái của đơn hàng, có thể bỏ comment dòng dưới đây và bổ sung logic lấy dữ liệu
        // trong Service/Repository rồi mapping vào DTO.
        // public List<OrderStatusHistoryDTOs> StatusHistory { get; set; } = new();
    }
}
