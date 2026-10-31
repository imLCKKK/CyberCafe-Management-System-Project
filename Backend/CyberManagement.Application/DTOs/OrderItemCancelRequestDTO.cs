namespace CyberManagement.Application.DTOs;

public class OrderItemCancelRequestDTO
{
    public long RequestId { get; set; }
    public long OrderDetailId { get; set; }
    public long RequestedByCustomerId { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime RequestedAt { get; set; }
    public long? HandledByAccountId { get; set; }
    public DateTime? HandledAt { get; set; }
}
