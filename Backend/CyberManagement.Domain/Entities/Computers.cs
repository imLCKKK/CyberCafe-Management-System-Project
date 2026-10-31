using System;
using System.Collections.Generic;
using System.Text;

namespace CyberManagement.Domain.Entities
{
    public class Computers
    {
        public long ComputerId { get; set; }
        public long RoomId { get; set; }
        public long ComputerTypeId { get; set; }
        public string ComputerName { get; set; } = string.Empty;
        public bool Status { get; set; }

        public virtual ICollection<GamingSessions> GamingSessions { get; set; } = new List<GamingSessions>();
        public virtual ComputerRooms Room { get; set; } = null!;
        public virtual ComputerTypes ComputerType { get; set; } = null!;
    }
}
