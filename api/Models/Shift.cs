using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CyberNet.Models;

[Table("shifts")]
public class Shift
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int ShiftId { get; set; }

    public int EmployeeId { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime StartTime { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime EndTime { get; set; }

    // ----- Navigation: many-to-one -----
    [ForeignKey(nameof(EmployeeId))]
    public Employee Employee { get; set; } = null!;
}
