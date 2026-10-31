using System;
using System.Collections.Generic;
using System.Text;

namespace CyberManagement.Application.DTOs
{
    public class InvoiceDTOs
    {
        public long InvoiceId { get; set; }
        public long? CustomerId { get; set; }
        public long? SessionId { get; set; }
        public long? EmployeeId { get; set; }
        public long? PromotionId { get; set; }

        public DateTime? InvoiceDate { get; set; }

        public decimal Subtotal { get; set; }
        public decimal Discount { get; set; }
        public decimal TotalAmount { get; set; }

        public string Status { get; set; } = string.Empty;

        public string PaymentMethod { get; set; } = string.Empty;
        public List<InvoiceDetailDTOs> Items { get; set; } = new();
    }
}
