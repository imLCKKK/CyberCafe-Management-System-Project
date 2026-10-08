using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CyberNet.Models;

[Table("products")]
public class Product
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int ProductId { get; set; }

    public int CategoryId { get; set; }

    [Required]
    [MaxLength(150)]
    [Column(TypeName = "varchar(150)")]
    public string ProductName { get; set; } = string.Empty;

    [Column(TypeName = "decimal(12,2)")]
    public decimal Price { get; set; }

    public int Stock { get; set; }

    [Column(TypeName = "tinyint(1)")]
    public bool IsActive { get; set; } = true;

    // ----- Navigation: many-to-one -----
    [ForeignKey(nameof(CategoryId))]
    public Category Category { get; set; } = null!;

    // ----- Navigation: one-to-many -----
    public ICollection<StockReceiptDetail> StockReceiptDetails { get; set; } = new List<StockReceiptDetail>();
    public ICollection<InventoryTransaction> InventoryTransactions { get; set; } = new List<InventoryTransaction>();
    public ICollection<OrderDetail> OrderDetails { get; set; } = new List<OrderDetail>();
}
