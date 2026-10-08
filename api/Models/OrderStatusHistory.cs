using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CyberNet.Models;

[Table("orderstatushistory")]
public class OrderStatusHistory
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int HistoryId { get; set; }

    public int OrderId { get; set; }

    [MaxLength(50)]
    [Column(TypeName = "varchar(50)")]
    public string? OldStatus { get; set; }

    [Required]
    [MaxLength(50)]
    [Column(TypeName = "varchar(50)")]
    public string NewStatus { get; set; } = string.Empty;

    public int ChangedBy { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime ChangedAt { get; set; }

    // ----- Navigation: many-to-one -----
    [ForeignKey(nameof(OrderId))]
    public Order Order { get; set; } = null!;

    [ForeignKey(nameof(ChangedBy))]
    public Employee ChangedByEmployee { get; set; } = null!;
}
