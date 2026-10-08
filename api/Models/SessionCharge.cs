using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CyberNet.Models;

[Table("sessioncharges")]
public class SessionCharge
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int ChargeId { get; set; }

    public int SessionId { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime StartTime { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime EndTime { get; set; }

    [Column(TypeName = "decimal(12,2)")]
    public decimal Amount { get; set; }

    // ----- Navigation: many-to-one -----
    [ForeignKey(nameof(SessionId))]
    public GamingSession GamingSession { get; set; } = null!;
}
