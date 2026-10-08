using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CyberNet.Models;

[Table("computermaintenance")]
public class ComputerMaintenance
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int MaintenanceId { get; set; }

    public int ComputerId { get; set; }

    public int EmployeeId { get; set; }

    [MaxLength(255)]
    [Column(TypeName = "varchar(255)")]
    public string? Description { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime StartTime { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime? EndTime { get; set; }

    [Required]
    [MaxLength(50)]
    [Column(TypeName = "varchar(50)")]
    public string Status { get; set; } = string.Empty;

    // ----- Navigation: many-to-one -----
    [ForeignKey(nameof(ComputerId))]
    public Computer Computer { get; set; } = null!;

    [ForeignKey(nameof(EmployeeId))]
    public Employee Employee { get; set; } = null!;
}
