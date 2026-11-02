namespace CyberManagement.Domain.Entities;

public class ComputerType
{
    public int ComputerTypeId { get; set; }

    public string TypeName { get; set; } = null!;

    public string? CPU { get; set; }

    public string? RAM { get; set; }

    public string? GPU { get; set; }

    public decimal PricePerHour { get; set; }

    public string? Description { get; set; }

    public ICollection<Computer> Computers { get; set; } = new List<Computer>();
}
