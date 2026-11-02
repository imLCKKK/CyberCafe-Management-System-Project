namespace CyberManagement.Domain.Entities;

public class Team
{
    public int TeamId { get; set; }

    public int TournamentId { get; set; }

    public string TeamName { get; set; } = null!;

    public decimal? AvgSkillScore { get; set; }

    public Tournament Tournament { get; set; } = null!;

    public ICollection<TeamMember> Members { get; set; } = new List<TeamMember>();
}
