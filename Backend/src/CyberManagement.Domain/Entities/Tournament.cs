namespace CyberManagement.Domain.Entities;

public class Tournament
{
    public int TournamentId { get; set; }

    public int GameId { get; set; }

    public int CreatedBy { get; set; }

    public string TournamentName { get; set; } = null!;

    public DateTime RegistrationStart { get; set; }

    public DateTime RegistrationEnd { get; set; }

    public DateTime StartTime { get; set; }

    public decimal EntryFee { get; set; }

    public string Status { get; set; } = "Draft";

    public Game Game { get; set; } = null!;

    public Employee CreatedByEmployee { get; set; } = null!;

    public ICollection<TournamentRegistration> Registrations { get; set; } = new List<TournamentRegistration>();

    public ICollection<Team> Teams { get; set; } = new List<Team>();
}
