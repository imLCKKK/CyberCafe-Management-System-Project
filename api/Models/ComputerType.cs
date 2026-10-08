using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CyberNet.Models;

[Table("computertypes")]
public class ComputerType
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int ComputerTypeId { get; set; }

    [Required]
    [MaxLength(100)]
    [Column(TypeName = "varchar(100)")]
    public string TypeName { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    [Column(TypeName = "varchar(100)")]
    public string CPU { get; set; } = string.Empty;

    [Required]
    [MaxLength(50)]
    [Column(TypeName = "varchar(50)")]
    public string RAM { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    [Column(TypeName = "varchar(100)")]
    public string GPU { get; set; } = string.Empty;

    [Column(TypeName = "decimal(12,2)")]
    public decimal PricePerHour { get; set; }

    [MaxLength(255)]
    [Column(TypeName = "varchar(255)")]
    public string? Description { get; set; }

    // ----- Navigation: one-to-many -----
    public ICollection<Computer> Computers { get; set; } = new List<Computer>();
}
