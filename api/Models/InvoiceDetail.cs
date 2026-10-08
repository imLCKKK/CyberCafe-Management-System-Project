using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CyberNet.Models;

[Table("invoicedetails")]
public class InvoiceDetail
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int InvoiceDetailId { get; set; }

    public int InvoiceId { get; set; }

    [Required]
    [MaxLength(50)]
    [Column(TypeName = "varchar(50)")]
    public string ItemType { get; set; } = string.Empty;

    [MaxLength(255)]
    [Column(TypeName = "varchar(255)")]
    public string? Description { get; set; }

    public int Quantity { get; set; }

    [Column(TypeName = "decimal(12,2)")]
    public decimal UnitPrice { get; set; }

    [Column(TypeName = "decimal(12,2)")]
    public decimal Amount { get; set; }

    // ----- Navigation: many-to-one -----
    [ForeignKey(nameof(InvoiceId))]
    public Invoice Invoice { get; set; } = null!;
}
