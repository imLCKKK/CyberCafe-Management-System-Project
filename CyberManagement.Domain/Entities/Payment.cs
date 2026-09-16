using System;
using System.Collections.Generic;
using System.Text;

namespace CyberManagement.Domain.Entities
{
    public class Payment
    {
        public int PaymentId { get; set; }
        public int InvoiceId { get; set; }
        public decimal Amount { get; set; }
        public string Method { get; set; } = "Cash"; // Cash, Wallet, Banking
        public DateTime? PaymentTime { get; set; } = DateTime.UtcNow;

        public virtual Invoice Invoice { get; set; } = null!;
    }
}
