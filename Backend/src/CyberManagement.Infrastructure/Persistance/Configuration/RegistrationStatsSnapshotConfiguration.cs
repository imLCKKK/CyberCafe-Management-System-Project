using CyberManagement.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CyberManagement.Infrastructure.Persistence.Configurations;

public sealed class RegistrationStatsSnapshotConfiguration : IEntityTypeConfiguration<RegistrationStatsSnapshot>
{
    public void Configure(EntityTypeBuilder<RegistrationStatsSnapshot> builder)
    {
        builder.ToTable("RegistrationStatsSnapshots");
        builder.HasKey(x => x.SnapshotId);
        builder.Property(x => x.SnapshotId).ValueGeneratedNever();
        builder.Property(x => x.RiotId).HasMaxLength(128);
        builder.HasOne<TournamentRegistration>().WithMany(x => x.StatsSnapshots)
            .HasForeignKey(x => x.RegistrationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.RegistrationId, x.RiotId, x.FetchedAt }).IsUnique();
    }
}
