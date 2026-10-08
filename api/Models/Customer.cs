using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CyberNet.Models;

[Table("customers")]
public class Customer
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int CustomerId { get; set; }

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

    [MaxLength(150)]
    [Column(TypeName = "varchar(150)")]
    public string? Email { get; set; }

    [Column(TypeName = "decimal(12,2)")]
    public decimal WalletBalance { get; set; }

    [Required]
    [MaxLength(50)]
    [Column(TypeName = "varchar(50)")]
    public string Status { get; set; } = string.Empty;

    [Column(TypeName = "datetime")]
    public DateTime CreatedAt { get; set; }

    // ----- Navigation: one-to-many -----
    public ICollection<CustomerStatusHistory> CustomerStatusHistories { get; set; } = new List<CustomerStatusHistory>();
    public ICollection<WalletTransaction> WalletTransactions { get; set; } = new List<WalletTransaction>();
    public ICollection<GamingSession> GamingSessions { get; set; } = new List<GamingSession>();
    public ICollection<Invoice> Invoices { get; set; } = new List<Invoice>();
    public ICollection<Order> Orders { get; set; } = new List<Order>();
    public ICollection<TournamentRegistration> TournamentRegistrations { get; set; } = new List<TournamentRegistration>();
}
