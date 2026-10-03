using System;
using System.Collections.Generic;
using System.Text;

namespace CyberManagement.Domain.Entities
{
    public class ComputerRooms
    {
        public int RoomId { get; set; }
        public string RoomName { get; set; } = string.Empty;
        public int Floor { get; set; }
        public string Description { get; set; } = string.Empty;
        public virtual ICollection<Computers> Computers { get; set; } = new List<Computers>();
    }
}
