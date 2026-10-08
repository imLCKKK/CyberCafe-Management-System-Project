using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CyberNet.Models;

[Table("employeesessions")]
public class EmployeeSession
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int EmployeeSessionId { get; set; }

    public int EmployeeId { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime LoginTime { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime? LogoutTime { get; set; }

    [MaxLength(50)]
    [Column(TypeName = "varchar(50)")]
    public string? IPAddress { get; set; }

    [Required]
    [MaxLength(50)]
    [Column(TypeName = "varchar(50)")]
    public string Status { get; set; } = string.Empty;

    // ----- Navigation: many-to-one -----
    [ForeignKey(nameof(EmployeeId))]
    public Employee Employee { get; set; } = null!;
}
