using System;
using System.Collections.Generic;
using System.Text;

namespace CyberManagement.Application.DTOs
{
    public class InvoiceDetailDTOs
    {
        public int InvoiceDetailId { get; set; }

        public string ItemType { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;

        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal Amount { get; set; } 
    }
}
