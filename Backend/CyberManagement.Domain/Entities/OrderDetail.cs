using System;
using System.Collections.Generic;
using System.Text;

namespace CyberManagement.Domain.Entities
{
    public class OrderDetail
    {
        public long OrderDetailId { get; set; }
        public long OrderId { get; set; }
        public long ProductId { get; set; }
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public string Status { get; set; } = "Pending";
        public long? ConfirmedByAccountId { get; set; }
        public DateTime? ConfirmedAt { get; set; }
        public long? CancelledByAccountId { get; set; }
        public DateTime? CancelledAt { get; set; }
        public string CancelReason { get; set; } = string.Empty;
        public virtual ICollection<OrderItemCancelRequest> CancellationRequests { get; set; }
            = new List<OrderItemCancelRequest>();

        // Phần tiền giảm được phân bổ cho toàn bộ dòng món này,
        // không phải tiền giảm trên một sản phẩm.
        public decimal DiscountAmount { get; set; }


        public virtual Order Order { get; set; } = null!;
        public virtual Product Product { get; set; } = null!;
    }
}
