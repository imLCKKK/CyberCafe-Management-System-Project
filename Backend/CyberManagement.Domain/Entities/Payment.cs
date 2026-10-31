using System;
using System.Collections.Generic;
using System.Text;

namespace CyberManagement.Domain.Entities
{
    public class Payment
    {
        public long PaymentId { get; set; }
        public long InvoiceId { get; set; }
        public long? WalletTransactionId { get; set; } 
        public decimal Amount { get; set; }
        public string Method { get; set; } = "Cash"; // Cash, Wallet, Banking
        public DateTime? PaymentTime { get; set; } = DateTime.UtcNow;
        public string Status { get; set; } = "Pending"; // Pending, Completed, Failed

        public virtual Invoice Invoice { get; set; } = null!;
        public virtual WalletTransactions? WalletTransaction { get; set; }
    }
}
