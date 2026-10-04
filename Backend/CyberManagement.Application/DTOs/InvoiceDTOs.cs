using System;
using System.Collections.Generic;
using System.Text;

namespace CyberManagement.Application.DTOs
{
    public class InvoiceDTOs
    {
        public int InvoiceId { get; set; }
        public int? CustomerId { get; set; }
        public int? SessionId { get; set; }
        public int EmployeeId { get; set; }
        public int? PromotionId { get; set; }

        public DateTime InvoiceDate { get; set; }

        public decimal Subtotal { get; set; }
        public decimal Discount { get; set; }
        public decimal TotalAmount { get; set; }

        public string Status { get; set; } = string.Empty;

        public string PaymentMethod { get; set; } = string.Empty;
        public List<InvoiceDetailDTOs> Items { get; set; } = new();
    }
}
