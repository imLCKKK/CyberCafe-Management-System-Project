namespace CyberManagement.Domain.Entities;

public class TournamentRegistration
{
    public int RegistrationId { get; set; }

    public int TournamentId { get; set; }

    public int CustomerId { get; set; }

    public int RankId { get; set; }

    public string? RiotId { get; set; }

    public string? PreferredRole { get; set; }

    public decimal WinRate { get; set; }

    public int HoursPlayed { get; set; }

    public decimal? SkillScore { get; set; }

    public string Status { get; set; } = "Registered";

    public DateTime RegisteredAt { get; set; } = DateTime.UtcNow;

    public decimal? FeeCharged { get; set; }

    public int? ChargeTransactionId { get; set; }

    public WalletTransaction? ChargeTransaction { get; set; }

    public ICollection<RegistrationStatsSnapshot> StatsSnapshots { get; } = new List<RegistrationStatsSnapshot>();

    public Tournament Tournament { get; set; } = null!;

    public Customer Customer { get; set; } = null!;

    public GameRank Rank { get; set; } = null!;
}
