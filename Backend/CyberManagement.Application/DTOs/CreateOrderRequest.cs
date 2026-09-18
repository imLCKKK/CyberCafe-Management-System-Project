using System;
using System.Collections.Generic;
using System.Text;

namespace CyberManagement.Application.DTOs
{
    public class OrderItemDto
    {
        public int ProductId { get; set; }
        public int Quantity { get; set; }
    }

    public class CreateOrderRequest
    {
        public int? CustomerId { get; set; }
        public string PaymentMethod { get; set; } = "Cash";
        public List<OrderItemDto> Items { get; set; } = new();
    }
}
