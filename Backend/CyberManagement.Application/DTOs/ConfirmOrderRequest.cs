using System;
using System.Collections.Generic;
using System.Text;

namespace CyberManagement.Application.DTOs
{
    public class ConfirmOrderRequest
    {
        public string PaymentMethod { get; set; } = string.Empty;
    }
}
