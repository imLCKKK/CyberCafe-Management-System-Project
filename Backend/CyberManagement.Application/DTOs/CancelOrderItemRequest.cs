namespace CyberManagement.Application.DTOs;

// Dữ liệu của ca sử dụng hủy món; không phụ thuộc annotation hoặc kiểu dữ liệu HTTP.
public class CancelOrderItemRequest
{
    public string Reason { get; set; } = string.Empty;
}
