namespace CyberManagement.Domain.Entities;

public class Employee
{
    public int EmployeeId { get; set; }

    public int RoleId { get; set; }

    public string FullName { get; set; } = null!;

    public string? Phone { get; set; }

    public string Username { get; set; } = null!;

    public string PasswordHash { get; set; } = null!;

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Role Role { get; set; } = null!;

    public ICollection<Shift> Shifts { get; set; } = new List<Shift>();
}
