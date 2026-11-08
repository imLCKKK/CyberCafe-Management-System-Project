namespace CyberManagement.Domain.Entities;

// Khóa phát hành bền vững, kể cả khi hóa đơn 0 đồng không có Payment.
public class InvoiceIssueRequest
{
    public string OperationKey { get; set; } = null!;
    public string RequestHash { get; set; } = null!;
    public int InvoiceId { get; set; }
    public Invoice Invoice { get; set; } = null!;
}
