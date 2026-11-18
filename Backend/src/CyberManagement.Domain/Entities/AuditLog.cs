namespace CyberManagement.Domain.Entities;

public class AuditLog
{
    public int LogId { get; set; }

    public int? EmployeeId { get; set; }

    public string EntityType { get; set; } = null!;

    public int EntityId { get; set; }

    public string Action { get; set; } = null!;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public string? RequestKey { get; set; }

    public string? RequestFingerprint { get; set; }

    public string? ResponseJson { get; set; }

    public Employee? Employee { get; set; }
}
