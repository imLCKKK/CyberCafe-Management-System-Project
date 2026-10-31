using System;
using System.Collections.Generic;
using System.Text;

namespace CyberManagement.Domain.Entities
{
    public class Role
    {
        public long RoleId { get; set; }
        public string RoleName { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;

        public virtual ICollection<Employees> Employees { get; set; }= new List<Employees>();

    }
}
