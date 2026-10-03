using System;
using System.Collections.Generic;
using System.Text;

namespace CyberManagement.Domain.Entities
{
    public class InventoryTransaction //InvTransId, ProductId, EmployeeId, Type, Quantity, ReferenceId, CreatedAt
    {
        public int InvTransId { get; set; }
        public int ProductId { get; set; }
        public int EmployeeId { get; set; }
        public string Type { get; set; } = string.Empty; 
        public int Quantity { get; set; }
        public int ReferenceId { get; set; } 
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public virtual Product Product { get; set; } = null!;
    }
}
