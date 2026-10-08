using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CyberNet.Models;

[Table("computerrooms")]
public class ComputerRoom
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int RoomId { get; set; }

    [Required]
    [MaxLength(100)]
    [Column(TypeName = "varchar(100)")]
    public string RoomName { get; set; } = string.Empty;

    public int Floor { get; set; }

    [MaxLength(255)]
    [Column(TypeName = "varchar(255)")]
    public string? Description { get; set; }

    // ----- Navigation: one-to-many -----
    public ICollection<Computer> Computers { get; set; } = new List<Computer>();
}
