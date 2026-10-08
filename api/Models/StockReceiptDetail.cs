using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CyberNet.Models;

[Table("stockreceiptdetails")]
public class StockReceiptDetail
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int ReceiptDetailId { get; set; }

    public int ReceiptId { get; set; }

    public int ProductId { get; set; }

    public int Quantity { get; set; }

    [Column(TypeName = "decimal(12,2)")]
    public decimal UnitCost { get; set; }

    // ----- Navigation: many-to-one -----
    [ForeignKey(nameof(ReceiptId))]
    public StockReceipt Receipt { get; set; } = null!;

    [ForeignKey(nameof(ProductId))]
    public Product Product { get; set; } = null!;
}
