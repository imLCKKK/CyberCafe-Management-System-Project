using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CyberNet.Models;

[Table("customerstatushistory")]
public class CustomerStatusHistory
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int HistoryId { get; set; }

    public int CustomerId { get; set; }

    [MaxLength(50)]
    [Column(TypeName = "varchar(50)")]
    public string? OldStatus { get; set; }

    [Required]
    [MaxLength(50)]
    [Column(TypeName = "varchar(50)")]
    public string NewStatus { get; set; } = string.Empty;

    [MaxLength(255)]
    [Column(TypeName = "varchar(255)")]
    public string? Reason { get; set; }

    public int ChangedBy { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime ChangedAt { get; set; }

    // ----- Navigation: many-to-one -----
    [ForeignKey(nameof(CustomerId))]
    public Customer Customer { get; set; } = null!;

    [ForeignKey(nameof(ChangedBy))]
    public Employee ChangedByEmployee { get; set; } = null!;
}
