namespace CyberManagement.Domain.Entities;

public class Customer
{
    public int CustomerId { get; set; }

    public string FullName { get; set; } = null!;

    public string Phone { get; set; } = null!;

    public string? PasswordHash { get; set; }

    public string? Email { get; set; }

    public decimal WalletBalance { get; set; }

    public string Status { get; set; } = "Active";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<CustomerStatusHistory> StatusHistory { get; set; } = new List<CustomerStatusHistory>();

    public ICollection<WalletTransaction> WalletTransactions { get; set; } = new List<WalletTransaction>();

    public ICollection<GamingSession> GamingSessions { get; set; } = new List<GamingSession>();

    public ICollection<Order> Orders { get; set; } = new List<Order>();

    public ICollection<Invoice> Invoices { get; set; } = new List<Invoice>();
}
