using System;
using System.Collections.Generic;
using System.Text;

namespace CyberManagement.Domain.Entities
{
    public class OrderStatusHistory //HistoryId, OrderId, OldStatus, NewStatus, ChangedBy, ChangedAt
    {
        public int HistoryId { get; set; }
        public int OrderId { get; set; }
        public string OldStatus { get; set; }
        public string NewStatus { get; set; }
        public int ChangedBy { get; set; }
        public DateTime ChangedAt { get; set; } = DateTime.UtcNow;

        public virtual Order Order { get; set; } = null!;
        public virtual Employees Employee { get; set; } = null!;
    }
}
