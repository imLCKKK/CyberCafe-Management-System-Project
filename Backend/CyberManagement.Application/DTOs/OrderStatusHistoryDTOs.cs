using System;
using System.Collections.Generic;
using System.Text;

namespace CyberManagement.Application.DTOs
{
    public class OrderStatusHistoryDTOs
    {
        public int HistoryId { get; set; }
        public int OrderId { get; set; }

        public string OldStatus { get; set; } = string.Empty;
        public string NewStatus { get; set; } = string.Empty;

        public int ChangedBy { get; set; }
        public DateTime ChangedAt { get; set; }
    }
}
