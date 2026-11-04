namespace CyberManagement.Domain.Entities;

public class Promotion
{
    public int PromotionId { get; set; }

    public string PromoName { get; set; } = null!;

    public string DiscountType { get; set; } = null!;

    public decimal DiscountValue { get; set; }

    public DateTime StartDate { get; set; }

    public DateTime EndDate { get; set; }

    public bool IsActive { get; set; } = true;

    public ICollection<Invoice> Invoices { get; set; } = new List<Invoice>();
}
