namespace CyberManagement.Domain.Entities;

public class Promotion
{
    public int PromotionId { get; set; }

    // Mã voucher duy nhất, lưu chữ hoa; hạn mức null nghĩa là không giới hạn.
    public string Code { get; set; } = null!;
    public decimal MinOrderAmount { get; set; }
    public decimal? MaxDiscountAmount { get; set; }
    public int? TotalUsageLimit { get; set; }
    public int? PerCustomerUsageLimit { get; set; }

    public string PromoName { get; set; } = null!;

    public string DiscountType { get; set; } = null!;

    public decimal DiscountValue { get; set; }

    public DateTimeOffset StartDate { get; set; }

    public DateTimeOffset EndDate { get; set; }

    public bool IsActive { get; set; } = true;

    public ICollection<Invoice> Invoices { get; set; } = new List<Invoice>();
}
