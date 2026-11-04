namespace CyberNet.Data.Entities;

public class Customer
{
    public int CustomerId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public decimal WalletBalance { get; set; }

    public ICollection<GamingSession> GamingSessions { get; set; } = new List<GamingSession>();
    public ICollection<Order> Orders { get; set; } = new List<Order>();
    public ICollection<WalletTransaction> WalletTransactions { get; set; } = new List<WalletTransaction>();
}
