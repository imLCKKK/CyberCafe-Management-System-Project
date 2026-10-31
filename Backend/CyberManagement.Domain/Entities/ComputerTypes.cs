using System;
using System.Collections.Generic;
using System.Text;

namespace CyberManagement.Domain.Entities
{
    public class ComputerTypes
    {
        public long ComputerTypeId { get; set; }
        public string ComputerTypeName { get; set; } = string.Empty;
        public string CPU { get; set; } = string.Empty;
        public string RAM { get; set; } = string.Empty;
        public string GPU { get; set; } = string.Empty;
        public decimal PricePerHour { get; set; }
        public string Description { get; set; } = string.Empty;

        public virtual ICollection<Computers> Computers { get; set; } = new List<Computers>();
    }
}
