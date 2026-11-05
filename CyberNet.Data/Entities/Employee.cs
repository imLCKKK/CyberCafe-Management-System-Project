namespace CyberNet.Data.Entities;

public class Employee
{
    public int EmployeeId { get; set; }
    public int RoleId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string? Phone { get; set; }

    public Role Role { get; set; } = null!;
    public ICollection<Shift> Shifts { get; set; } = new List<Shift>();
    public ICollection<AuditLog> AuditLogs { get; set; } = new List<AuditLog>();
    public ICollection<Order> Orders { get; set; } = new List<Order>();
}
