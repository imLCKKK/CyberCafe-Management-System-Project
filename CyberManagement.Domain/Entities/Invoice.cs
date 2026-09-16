using System;
using System.Collections.Generic;
using System.Text;

namespace CyberManagement.Domain.Entities
{
    public class Invoice
    {
        public int InvoiceId { get; set; }
        public int? OrderId { get; set; }
        public int? SessionId { get; set; }
        public decimal TotalAmount { get; set; }
        public DateTime? PaymentTime { get; set; } = DateTime.UtcNow;

        public virtual Order? Order { get; set; }
        public virtual ICollection<Payment> Payments { get; set; } = new List<Payment>();
    }
}
