namespace CyberManagement.Domain.Entities;

// Khóa HTTP lưu cùng giao dịch; scope gồm actor, route và key phân biệt hoa/thường.
public sealed class OrderOperation
{
    public int Id { get; set; }
    public string ActorType { get; set; } = null!;
    public int ActorId { get; set; }
    public string Route { get; set; } = null!;
    public string Key { get; set; } = null!;
    public string RequestHash { get; set; } = null!;
    public int OrderId { get; set; }
    public Order Order { get; set; } = null!;
    public DateTime CreatedAtUtc { get; set; }
}
