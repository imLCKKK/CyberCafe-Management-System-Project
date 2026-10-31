using System;
using System.Collections.Generic;
using System.Text;

namespace CyberManagement.Domain.Entities
{
    public class OrderStatusHistory //HistoryId, OrderId, OldStatus, NewStatus, ChangedBy, ChangedAt
    {
        public long HistoryId { get; set; }
        public long OrderId { get; set; }
        public long? OrderDetailId { get; set; }
        public string OldStatus { get; set; } = string.Empty;
        public string NewStatus { get; set; } = string.Empty;
        public long ChangedBy { get; set; }
        public DateTime ChangedAt { get; set; } = DateTime.UtcNow;

        public virtual Order Order { get; set; } = null!;
        public virtual Employees Employee { get; set; } = null!;
    }
}
