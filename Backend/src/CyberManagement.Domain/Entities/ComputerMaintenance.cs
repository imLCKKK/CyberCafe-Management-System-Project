namespace CyberManagement.Domain.Entities;

public class ComputerMaintenance
{
    public int MaintenanceId { get; set; }

    public int ComputerId { get; set; }

    public int EmployeeId { get; set; }

    public string? Description { get; set; }

    public DateTime StartTime { get; set; }

    public DateTime? EndTime { get; set; }

    public string Status { get; set; } = null!;

    public Computer Computer { get; set; } = null!;

    public Employee Employee { get; set; } = null!;
}
