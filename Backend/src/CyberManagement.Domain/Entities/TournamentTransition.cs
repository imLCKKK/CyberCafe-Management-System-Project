namespace CyberManagement.Domain.Entities;

public class TournamentTransition
{
    public int TournamentTransitionId { get; set; }
    public int TournamentId { get; set; }
    public int EmployeeId { get; set; }
    public string RequestKey { get; set; } = null!;
    public string ToStatus { get; set; } = null!;
    public string ExpectedVersion { get; set; } = null!;
    public string ResponseJson { get; set; } = null!;
    public Tournament Tournament { get; set; } = null!;
    public Employee Employee { get; set; } = null!;
}
