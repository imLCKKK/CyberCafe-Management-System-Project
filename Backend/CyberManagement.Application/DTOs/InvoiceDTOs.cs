using System;
using System.Collections.Generic;
using System.Text;

namespace CyberManagement.Application.DTOs
{
    public class InvoiceDTOs
    {
        public int InvoiceId { get; set; }
        public int? OrderId { get; set; }
        public decimal TotalAmount { get; set; }
        public string PaymentMethod { get; set; } = string.Empty;
        public DateTime? PaymentTime { get; set; }
    }
}
