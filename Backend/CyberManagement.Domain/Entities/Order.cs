using System;
using System.Collections.Generic;
using System.Text;

namespace CyberManagement.Domain.Entities
{
    public class Order
    {
        public int OrderId { get; set; }
        public int? CustomerId { get; set; }
        public int EmployeeId { get; set; }
        public DateTime OrderTime { get; set; } = DateTime.UtcNow;
        public string Status { get; set; } = "Pending";

        public virtual ICollection<OrderDetail> OrderDetails { get; set; } = new List<OrderDetail>();
        public virtual ICollection<Invoice> Invoices { get; set; } = new List<Invoice>();
    }
}
