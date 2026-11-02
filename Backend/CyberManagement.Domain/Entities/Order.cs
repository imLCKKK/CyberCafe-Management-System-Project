using System;
using System.Collections.Generic;
using System.Text;

namespace CyberManagement.Domain.Entities
{
    public class Order
    {
        public long OrderId { get; set; }
        public long? CustomerId { get; set; }
        public long? SessionId { get; set; }
        public long? EmployeeId { get; set; }
        public DateTime OrderTime { get; set; } = DateTime.UtcNow;
        public string Status { get; set; } = "Pending";
        public decimal TotalAmount { get; set; }

        // Snapshot voucher tại thời điểm đặt đơn.
        // Null khi đơn không sử dụng voucher.
        public string? VoucherCodeSnapshot { get; set; } // FOOD20
        public string? VoucherDiscountTypeSnapshot { get; set; } // Percent hoặc Fixed
        public decimal? VoucherDiscountValueSnapshot { get; set; } // 20 hoặc 20000

        // Tổng tiền giảm thực tế của cả đơn.
        public decimal DiscountAmount { get; set; } 


        public virtual ICollection<OrderDetail> OrderDetails { get; set; } = new List<OrderDetail>();
        public virtual ICollection<OrderStatusHistory> StatusHistories { get; set; } = new List<OrderStatusHistory>();

    }
}
