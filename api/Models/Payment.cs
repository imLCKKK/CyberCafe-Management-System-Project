using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CyberNet.Models;

[Table("payments")]
public class Payment
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int PaymentId { get; set; }

    public int InvoiceId { get; set; }

    public int? WalletTransactionId { get; set; }

    [Column(TypeName = "decimal(12,2)")]
    public decimal Amount { get; set; }

    [Required]
    [MaxLength(50)]
    [Column(TypeName = "varchar(50)")]
    public string Method { get; set; } = string.Empty;

    [Column(TypeName = "datetime")]
    public DateTime PaymentTime { get; set; }

    [Required]
    [MaxLength(50)]
    [Column(TypeName = "varchar(50)")]
    public string Status { get; set; } = string.Empty;

    // ----- Navigation: many-to-one -----
    [ForeignKey(nameof(InvoiceId))]
    public Invoice Invoice { get; set; } = null!;

    [ForeignKey(nameof(WalletTransactionId))]
    public WalletTransaction? WalletTransaction { get; set; }
}
