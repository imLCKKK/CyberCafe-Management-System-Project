using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CyberNet.Models;

[Table("inventorytransactions")]
public class InventoryTransaction
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int InvTransId { get; set; }

    public int ProductId { get; set; }

    public int EmployeeId { get; set; }

    [Required]
    [MaxLength(50)]
    [Column(TypeName = "varchar(50)")]
    public string Type { get; set; } = string.Empty;

    public int Quantity { get; set; }

    public int? ReferenceId { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime CreatedAt { get; set; }

    // ----- Navigation: many-to-one -----
    [ForeignKey(nameof(ProductId))]
    public Product Product { get; set; } = null!;

    [ForeignKey(nameof(EmployeeId))]
    public Employee Employee { get; set; } = null!;
}
