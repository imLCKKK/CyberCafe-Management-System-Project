using System;
using System.Collections.Generic;
using System.Text;

namespace CyberManagement.Application.DTOs
{
    public class PaymentDTOs
    {
        public int PaymentId { get; set; }
        public int InvoiceId { get; set; }
        public int? WalletTransactionId { get; set; }

        public decimal Amount { get; set; }
        public string Method { get; set; } = string.Empty;

        public DateTime PaymentTime { get; set; }
        public string Status { get; set; } = string.Empty;
    }
}