using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CyberNet.Models;

[Table("stockreceipts")]
public class StockReceipt
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int ReceiptId { get; set; }

    public int SupplierId { get; set; }

    public int EmployeeId { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime ReceiptDate { get; set; }

    [Column(TypeName = "decimal(12,2)")]
    public decimal TotalAmount { get; set; }

    // ----- Navigation: many-to-one -----
    [ForeignKey(nameof(SupplierId))]
    public Supplier Supplier { get; set; } = null!;

    [ForeignKey(nameof(EmployeeId))]
    public Employee Employee { get; set; } = null!;

    // ----- Navigation: one-to-many -----
    public ICollection<StockReceiptDetail> StockReceiptDetails { get; set; } = new List<StockReceiptDetail>();
}
