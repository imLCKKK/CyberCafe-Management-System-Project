using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CyberNet.Models;

[Table("orders")]
public class Order
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int OrderId { get; set; }

    public int? CustomerId { get; set; }

    public int? SessionId { get; set; }

    public int EmployeeId { get; set; }

    public int? InvoiceId { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime OrderTime { get; set; }

    [Required]
    [MaxLength(50)]
    [Column(TypeName = "varchar(50)")]
    public string Status { get; set; } = string.Empty;

    [Column(TypeName = "decimal(12,2)")]
    public decimal TotalAmount { get; set; }

    // ----- Navigation: many-to-one -----
    [ForeignKey(nameof(CustomerId))]
    public Customer? Customer { get; set; }

    [ForeignKey(nameof(SessionId))]
    public GamingSession? GamingSession { get; set; }

    [ForeignKey(nameof(EmployeeId))]
    public Employee Employee { get; set; } = null!;

    [ForeignKey(nameof(InvoiceId))]
    public Invoice? Invoice { get; set; }

    // ----- Navigation: one-to-many -----
    public ICollection<OrderDetail> OrderDetails { get; set; } = new List<OrderDetail>();
    public ICollection<OrderStatusHistory> OrderStatusHistories { get; set; } = new List<OrderStatusHistory>();
}
