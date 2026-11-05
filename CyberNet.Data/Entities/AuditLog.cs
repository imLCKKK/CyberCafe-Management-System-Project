namespace CyberNet.Data.Entities;

public class AuditLog
{
    public int LogId { get; set; }
    public int EmployeeId { get; set; }
    public string Action { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }

    public Employee Employee { get; set; } = null!;
}
