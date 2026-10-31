using System;
using System.Collections.Generic;
using System.Text;

namespace CyberManagement.Domain.Entities
{
    public class CustomerStatusHistory
    {
        public long HistoryId { get; set; }
        public long CustomerId { get; set; }
        public string OldStatus { get; set; } = string.Empty;
        public string NewStatus { get; set; } = string.Empty;
        public string Reason { get; set; } = string.Empty;
        public long ChangedBy { get; set; }
        public DateTime ChangedAt { get; set; }

        public virtual Customers Customer { get; set; } = null!;
        public virtual Employees ChangedByNavigation { get; set; } = null!;
    }
}
