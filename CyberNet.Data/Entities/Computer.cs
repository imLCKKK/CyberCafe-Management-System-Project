namespace CyberNet.Data.Entities;

public class Computer
{
    public int ComputerId { get; set; }
    public string ComputerName { get; set; } = string.Empty;
    public string Status { get; set; } = "Trống";
    public decimal PricePerHour { get; set; }

    public ICollection<GamingSession> GamingSessions { get; set; } = new List<GamingSession>();
}
