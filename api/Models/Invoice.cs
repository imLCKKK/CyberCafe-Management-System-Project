using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CyberNet.Models;

[Table("invoices")]
public class Invoice
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int InvoiceId { get; set; }

    public int? CustomerId { get; set; }

    public int? SessionId { get; set; }

    public int EmployeeId { get; set; }

    public int? PromotionId { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime InvoiceDate { get; set; }

    [Column(TypeName = "decimal(12,2)")]
    public decimal Subtotal { get; set; }

    [Column(TypeName = "decimal(12,2)")]
    public decimal Discount { get; set; }

    [Column(TypeName = "decimal(12,2)")]
    public decimal TotalAmount { get; set; }

    [Required]
    [MaxLength(50)]
    [Column(TypeName = "varchar(50)")]
    public string Status { get; set; } = string.Empty;

    // ----- Navigation: many-to-one -----
    [ForeignKey(nameof(CustomerId))]
    public Customer? Customer { get; set; }

    [ForeignKey(nameof(SessionId))]
    public GamingSession? GamingSession { get; set; }

    [ForeignKey(nameof(EmployeeId))]
    public Employee Employee { get; set; } = null!;

    [ForeignKey(nameof(PromotionId))]
    public Promotion? Promotion { get; set; }

    // ----- Navigation: one-to-many -----
    public ICollection<InvoiceDetail> InvoiceDetails { get; set; } = new List<InvoiceDetail>();
    public ICollection<Payment> Payments { get; set; } = new List<Payment>();
    public ICollection<Order> Orders { get; set; } = new List<Order>();
}
