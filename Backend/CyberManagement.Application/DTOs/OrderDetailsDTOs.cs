using System;
using System.Collections.Generic;
using System.Text;

namespace CyberManagement.Application.DTOs
{
    public class OrderDetailsDTOs
    {
        public long OrderDetailId { get; set; }
        public string Status { get; set; } = string.Empty;
        public long? ConfirmedByAccountId { get; set; }
        public DateTime? ConfirmedAt { get; set; }
        public long? CancelledByAccountId { get; set; }
        public DateTime? CancelledAt { get; set; }
        public string CancelReason { get; set; } = string.Empty;
        public List<OrderItemCancelRequestDTO> CancellationRequests { get; set; } = new();
        public long ProductId { get; set; }

        public string ProductName { get; set; } = string.Empty;

        public int Quantity { get; set; }

        public decimal UnitPrice { get; set; }

        public decimal SubTotal { get; set; }
    }
}
