using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CyberNet.Models;

[Table("gamingsessions")]
public class GamingSession
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int SessionId { get; set; }

    public int ComputerId { get; set; }

    public int CustomerId { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime StartTime { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime? EndTime { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime? LastChargedAt { get; set; }

    [Column(TypeName = "decimal(12,2)")]
    public decimal TotalCost { get; set; }

    [Required]
    [MaxLength(50)]
    [Column(TypeName = "varchar(50)")]
    public string Status { get; set; } = string.Empty;

    // ----- Navigation: many-to-one -----
    [ForeignKey(nameof(ComputerId))]
    public Computer Computer { get; set; } = null!;

    [ForeignKey(nameof(CustomerId))]
    public Customer Customer { get; set; } = null!;

    // ----- Navigation: one-to-many -----
    public ICollection<SessionCharge> SessionCharges { get; set; } = new List<SessionCharge>();
    public ICollection<Invoice> Invoices { get; set; } = new List<Invoice>();
    public ICollection<Order> Orders { get; set; } = new List<Order>();
}
