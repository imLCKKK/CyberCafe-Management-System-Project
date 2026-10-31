using System;
using System.Collections.Generic;
using System.Text;

namespace CyberManagement.Application.DTOs
{
    public class PaymentDTOs
    {
        public long PaymentId { get; set; }
        public long InvoiceId { get; set; }
        public long? WalletTransactionId { get; set; }

        public decimal Amount { get; set; }
        public string Method { get; set; } = string.Empty;

        public DateTime? PaymentTime { get; set; }
        public string Status { get; set; } = string.Empty;
    }
}