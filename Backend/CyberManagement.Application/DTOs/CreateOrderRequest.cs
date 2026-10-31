using System;
using System.Collections.Generic;
using System.Text;

namespace CyberManagement.Application.DTOs
{
    public class OrderItemDto
    {
        public long ProductId { get; set; }
        public int Quantity { get; set; }
    }

    public class CreateOrderRequest
    {
        public long? CustomerId { get; set; }
        public long? SessionId { get; set; }
        public List<OrderItemDto> Items { get; set; } = new();
    }
}
