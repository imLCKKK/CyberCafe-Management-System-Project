namespace CyberManagement.Domain.Entities;

public class Shift
{
    public int ShiftId { get; set; }

    public int EmployeeId { get; set; }

    public DateTime StartTime { get; set; }

    public DateTime? EndTime { get; set; }

    public Employee Employee { get; set; } = null!;
}
