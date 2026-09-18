using System;
using System.Collections.Generic;
using System.Text;

namespace CyberManagement.Application.DTOs
{
    public class OrderDTOs
    {
        public int OrderId { get; set; }
        public int? CustomerId { get; set; }
        public DateTime OrderTime { get; set; }
        public string Status { get; set; } = string.Empty;
        public decimal TotalAmount { get; set; }

        public List<CreateOrderRequest> Items { get; set; } = new();    
    }
}
