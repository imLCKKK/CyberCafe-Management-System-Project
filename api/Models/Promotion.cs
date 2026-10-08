using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CyberNet.Models;

[Table("promotions")]
public class Promotion
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int PromotionId { get; set; }

    [Required]
    [MaxLength(150)]
    [Column(TypeName = "varchar(150)")]
    public string PromoName { get; set; } = string.Empty;

    [Required]
    [MaxLength(50)]
    [Column(TypeName = "varchar(50)")]
    public string DiscountType { get; set; } = string.Empty;

    [Column(TypeName = "decimal(12,2)")]
    public decimal DiscountValue { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime StartDate { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime EndDate { get; set; }

    [Column(TypeName = "tinyint(1)")]
    public bool IsActive { get; set; } = true;

    // ----- Navigation: one-to-many -----
    public ICollection<Invoice> Invoices { get; set; } = new List<Invoice>();
}
