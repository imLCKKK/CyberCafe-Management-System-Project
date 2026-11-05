namespace CyberManagement.Domain.Entities;

public class ComputerRoom
{
    public int RoomId { get; set; }

    public string RoomName { get; set; } = null!;

    public int Floor { get; set; }

    public string? Description { get; set; }

    public ICollection<Computer> Computers { get; set; } = new List<Computer>();
}
