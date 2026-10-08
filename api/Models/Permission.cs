using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CyberNet.Models;

[Table("permissions")]
public class Permission
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int PermissionId { get; set; }

    [Required]
    [MaxLength(100)]
    [Column(TypeName = "varchar(100)")]
    public string PermissionName { get; set; } = string.Empty;

    [MaxLength(255)]
    [Column(TypeName = "varchar(255)")]
    public string? Description { get; set; }

    // ----- Navigation: one-to-many -----
    public ICollection<RolePermission> RolePermissions { get; set; } = new List<RolePermission>();
}
