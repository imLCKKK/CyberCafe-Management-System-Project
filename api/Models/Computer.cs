using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CyberNet.Models;

[Table("computers")]
public class Computer
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int ComputerId { get; set; }

    public int RoomId { get; set; }

    public int ComputerTypeId { get; set; }

    [Required]
    [MaxLength(100)]
    [Column(TypeName = "varchar(100)")]
    public string ComputerName { get; set; } = string.Empty;

    [Required]
    [MaxLength(50)]
    [Column(TypeName = "varchar(50)")]
    public string Status { get; set; } = string.Empty;

    // ----- Navigation: many-to-one -----
    [ForeignKey(nameof(RoomId))]
    public ComputerRoom Room { get; set; } = null!;

    [ForeignKey(nameof(ComputerTypeId))]
    public ComputerType ComputerType { get; set; } = null!;

    // ----- Navigation: one-to-many -----
    public ICollection<ComputerMaintenance> ComputerMaintenances { get; set; } = new List<ComputerMaintenance>();
    public ICollection<GamingSession> GamingSessions { get; set; } = new List<GamingSession>();
}
