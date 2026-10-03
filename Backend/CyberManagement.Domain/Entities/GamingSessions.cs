using System;
using System.Collections.Generic;
using System.Text;

namespace CyberManagement.Domain.Entities
{
    public class GamingSessions
    {
        public int SessionId { get; set; }
        public int ComputerId { get; set; }
        public int CustomerId { get; set; }

        public DateTime StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public DateTime? LastChargedAt { get; set; }

        public decimal TotalCost { get; set; }
        public string Status { get; set; } = string.Empty;

        public virtual Customers Customer { get; set; } = null!;
        public virtual Computers Computer { get; set; } = null!;
        public virtual ICollection<Order> Orders { get; set; }
            = new List<Order>();

        public virtual ICollection<Invoice> Invoices { get; set; }
            = new List<Invoice>();

    }
}
