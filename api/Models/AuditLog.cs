using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CyberNet.Models;

[Table("auditlogs")]
public class AuditLog
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int LogId { get; set; }

    public int EmployeeId { get; set; }

    [Required]
    [MaxLength(255)]
    [Column(TypeName = "varchar(255)")]
    public string Action { get; set; } = string.Empty;

    [Column(TypeName = "datetime")]
    public DateTime CreatedAt { get; set; }

    // ----- Navigation: many-to-one -----
    [ForeignKey(nameof(EmployeeId))]
    public Employee Employee { get; set; } = null!;
}
