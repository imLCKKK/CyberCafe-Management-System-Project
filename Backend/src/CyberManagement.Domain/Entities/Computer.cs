namespace CyberManagement.Domain.Entities;

public class Computer
{
    public int ComputerId { get; set; }

    public int RoomId { get; set; }

    public int ComputerTypeId { get; set; }

    public string ComputerName { get; set; } = null!;

    public string Status { get; set; } = "Available";

    public ComputerRoom Room { get; set; } = null!;

    public ComputerType ComputerType { get; set; } = null!;

    public ICollection<ComputerMaintenance> Maintenances { get; set; } = new List<ComputerMaintenance>();

    public ICollection<GamingSession> GamingSessions { get; set; } = new List<GamingSession>();
}
