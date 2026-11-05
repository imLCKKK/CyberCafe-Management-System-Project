namespace CyberManagement.Domain.Entities;

public class TeamMember
{
    public int TeamId { get; set; }

    public int RegistrationId { get; set; }

    public string? AssignedRole { get; set; }

    public Team Team { get; set; } = null!;

    public TournamentRegistration Registration { get; set; } = null!;
}
