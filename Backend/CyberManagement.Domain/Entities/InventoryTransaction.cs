using System;
using System.Collections.Generic;
using System.Text;

namespace CyberManagement.Domain.Entities
{
    public class InventoryTransaction //InvTransId, ProductId, EmployeeId, Type, Quantity, ReferenceId, CreatedAt
    {
        public long InvTransId { get; set; }
        public long ProductId { get; set; }
        public long? EmployeeId { get; set; }
        public long? RecordedByAccountId { get; set; }
        public long? OrderDetailId { get; set; }
        public int Before { get; set; }
        public int After { get; set; }
        public string Type { get; set; } = string.Empty; 
        public int Quantity { get; set; }
        public string Reason { get; set; } = string.Empty;
        public long ReferenceId { get; set; }
        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
        public virtual Product Product { get; set; } = null!;
    }
}
