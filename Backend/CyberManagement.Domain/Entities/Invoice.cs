using System;
using System.Collections.Generic;
using System.Text;

namespace CyberManagement.Domain.Entities
{
    public class Invoice
    {
        public long InvoiceId { get; set; }
        public long CustomerId { get; set; }
        public long? SessionId { get; set; }
        public long? EmployeeId { get; set; }
        public long? PromotionId { get; set; }
        public DateTime? InvoiceDate { get; set; } = DateTime.UtcNow;
        public decimal SubTotal { get; set; }
        public decimal Discount { get; set; }
        public decimal TotalAmount { get; set; }
        public string Status { get; set; } = "Unpaid"; // Unpaid, Paid, Cancelled

        public virtual ICollection<Payment> Payments { get; set; } = new List<Payment>(); //1 invoice can have many payments
        public virtual ICollection<Order> Orders { get; set; } = new List<Order>(); //1 invoice can have many orders
        public virtual ICollection<InvoiceDetail> InvoiceDetails { get; set; } = new List<InvoiceDetail>(); //1 invoice can have many invoice details
        public virtual Customers Customer { get; set; } = null!;
        public virtual GamingSessions? Session { get; set; }
        public virtual Promotions? Promotion { get; set; }
        public virtual Employees? Employee { get; set; }
    }
}
