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
        public long? InvoiceId { get; set; }
        public DateTime OrderTime { get; set; } = DateTime.UtcNow;
        public string Status { get; set; } = "Pending";
        public decimal TotalAmount { get; set; }

        public virtual ICollection<OrderDetail> OrderDetails { get; set; } = new List<OrderDetail>();
        public virtual ICollection<OrderStatusHistory> StatusHistories { get; set; } = new List<OrderStatusHistory>();

    }
}
