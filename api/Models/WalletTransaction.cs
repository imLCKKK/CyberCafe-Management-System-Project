using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CyberNet.Models;

[Table("wallettransactions")]
public class WalletTransaction
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int TransactionId { get; set; }

    public int CustomerId { get; set; }

    [Column(TypeName = "decimal(12,2)")]
    public decimal Amount { get; set; }

    [Column(TypeName = "decimal(12,2)")]
    public decimal BalanceAfter { get; set; }

    [Required]
    [MaxLength(50)]
    [Column(TypeName = "varchar(50)")]
    public string Type { get; set; } = string.Empty;

    [MaxLength(255)]
    [Column(TypeName = "varchar(255)")]
    public string? Description { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime CreatedAt { get; set; }

    // ----- Navigation: many-to-one -----
    [ForeignKey(nameof(CustomerId))]
    public Customer Customer { get; set; } = null!;

    // ----- Navigation: one-to-many -----
    public ICollection<Payment> Payments { get; set; } = new List<Payment>();
}
