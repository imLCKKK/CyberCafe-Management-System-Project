using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CyberNet.Models;

[Table("rolepermissions")]
public class RolePermission
{
    public int RoleId { get; set; }

    public int PermissionId { get; set; }

    // ----- Navigation: many-to-one -----
    [ForeignKey(nameof(RoleId))]
    public Role Role { get; set; } = null!;

    [ForeignKey(nameof(PermissionId))]
    public Permission Permission { get; set; } = null!;
}
