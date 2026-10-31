namespace CyberManagement.Application.DTOs;

public class SessionStatementDTO
{
    public long SessionId { get; set; }
    public long CustomerId { get; set; }
    public string Status { get; set; } = string.Empty;
    public decimal GamingCharged { get; set; }
    public decimal FoodDebited { get; set; }
    public decimal FoodRefunded { get; set; }
    public decimal FoodNet => FoodDebited - FoodRefunded;
    public decimal TotalRecorded => GamingCharged + FoodNet;
    public List<OrderDTOs> Orders { get; set; } = new();
}
