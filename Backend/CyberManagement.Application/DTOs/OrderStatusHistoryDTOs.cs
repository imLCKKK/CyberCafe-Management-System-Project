using System;
using System.Collections.Generic;
using System.Text;

namespace CyberManagement.Application.DTOs
{
    public class OrderStatusHistoryDTOs
    {
        public long HistoryId { get; set; }
        public long OrderId { get; set; }
        public long? OrderDetailId { get; set; }

        public string OldStatus { get; set; } = string.Empty;
        public string NewStatus { get; set; } = string.Empty;

        public long ChangedBy { get; set; }
        public DateTime ChangedAt { get; set; }
    }
}
