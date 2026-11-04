namespace CyberManagement.Domain.Entities;

public class Payment
{
    public int PaymentId { get; set; }

    public int InvoiceId { get; set; }

    public int? WalletTransactionId { get; set; }

    public decimal Amount { get; set; }

    public string Method { get; set; } = null!;

    public DateTime PaymentTime { get; set; } = DateTime.UtcNow;

    public string Status { get; set; } = null!;

    public Invoice Invoice { get; set; } = null!;

    public WalletTransaction? WalletTransaction { get; set; }
}
