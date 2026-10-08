using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CyberNet.Models;

[Table("employees")]
public class Employee
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int EmployeeId { get; set; }

    public int RoleId { get; set; }

    [Required]
    [MaxLength(150)]
    [Column(TypeName = "varchar(150)")]
    public string FullName { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    [Column(TypeName = "varchar(100)")]
    public string Username { get; set; } = string.Empty;

    [Required]
    [MaxLength(255)]
    [Column(TypeName = "varchar(255)")]
    public string PasswordHash { get; set; } = string.Empty;

    [MaxLength(20)]
    [Column(TypeName = "varchar(20)")]
    public string? Phone { get; set; }

    [Column(TypeName = "tinyint(1)")]
    public bool IsActive { get; set; } = true;

    [Column(TypeName = "datetime")]
    public DateTime CreatedAt { get; set; }

    // ----- Navigation: many-to-one -----
    [ForeignKey(nameof(RoleId))]
    public Role Role { get; set; } = null!;

    // ----- Navigation: one-to-many -----
    public ICollection<Shift> Shifts { get; set; } = new List<Shift>();
    public ICollection<EmployeeSession> EmployeeSessions { get; set; } = new List<EmployeeSession>();
    public ICollection<AuditLog> AuditLogs { get; set; } = new List<AuditLog>();
    public ICollection<CustomerStatusHistory> ChangedCustomerStatusHistories { get; set; } = new List<CustomerStatusHistory>();
    public ICollection<ComputerMaintenance> ComputerMaintenances { get; set; } = new List<ComputerMaintenance>();
    public ICollection<StockReceipt> StockReceipts { get; set; } = new List<StockReceipt>();
    public ICollection<InventoryTransaction> InventoryTransactions { get; set; } = new List<InventoryTransaction>();
    public ICollection<Invoice> Invoices { get; set; } = new List<Invoice>();
    public ICollection<Order> Orders { get; set; } = new List<Order>();
    public ICollection<OrderStatusHistory> ChangedOrderStatusHistories { get; set; } = new List<OrderStatusHistory>();
    public ICollection<Tournament> CreatedTournaments { get; set; } = new List<Tournament>();
}
