using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CyberNet.Models;

[Table("suppliers")]
public class Supplier
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int SupplierId { get; set; }

    [Required]
    [MaxLength(150)]
    [Column(TypeName = "varchar(150)")]
    public string SupplierName { get; set; } = string.Empty;

    [MaxLength(20)]
    [Column(TypeName = "varchar(20)")]
    public string? Phone { get; set; }

    [MaxLength(150)]
    [Column(TypeName = "varchar(150)")]
    public string? Email { get; set; }

    [MaxLength(255)]
    [Column(TypeName = "varchar(255)")]
    public string? Address { get; set; }

    // ----- Navigation: one-to-many -----
    public ICollection<StockReceipt> StockReceipts { get; set; } = new List<StockReceipt>();
}
