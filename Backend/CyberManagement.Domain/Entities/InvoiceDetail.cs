using System;
using System.Collections.Generic;
using System.Text;

namespace CyberManagement.Domain.Entities
{
    public class InvoiceDetail // InvoiceDetailId, InvoiceId, ItemType, Description, Quantity, UnitPrice, Amount
    {
        public long InvoiceDetailId { get; set; }
        public long InvoiceId { get; set; }
        public string ItemType { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal Amount { get; set; }

        public virtual Invoice Invoice { get; set; } = null!;
    }
}
