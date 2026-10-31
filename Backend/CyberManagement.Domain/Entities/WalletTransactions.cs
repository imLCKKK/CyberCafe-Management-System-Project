using System;
using System.Collections.Generic;
using System.Text;

namespace CyberManagement.Domain.Entities
{
    public class WalletTransactions
    {
        public long TransactionId { get; set; }
        public long CustomerId { get; set; }
        public decimal Amount { get; set; }
        public decimal BalanceAfter { get; set; }
        public string Type { get; set; } = string.Empty;
        public long? OrderId { get; set; }
        public long? OrderDetailId { get; set; }
        public long? SessionId { get; set; }
        public long? RecordedByAccountId { get; set; }
        public string Description { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public virtual Customers Customer { get; set; } = null!;
        public virtual ICollection<Payment> Payments { get; set; } = new List<Payment>();
    }
}
