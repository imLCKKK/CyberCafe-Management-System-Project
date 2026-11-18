namespace CyberManagement.Domain.Entities;

public sealed class RegistrationStatsSnapshot
{
    public Guid SnapshotId { get; set; }
    public int RegistrationId { get; set; }
    public string RiotId { get; set; } = null!;
    public DateTimeOffset FetchedAt { get; set; }
    public string CanonicalJson { get; set; } = null!;
}
