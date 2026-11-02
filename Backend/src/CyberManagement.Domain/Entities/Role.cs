namespace CyberManagement.Domain.Entities;

public class Role
{
    public int RoleId { get; set; }

    public string RoleName { get; set; } = null!;

    public string? Description { get; set; }

    public ICollection<Employee> Employees { get; set; } = new List<Employee>();

    public ICollection<RolePermission> RolePermissions { get; set; } = new List<RolePermission>();
}
