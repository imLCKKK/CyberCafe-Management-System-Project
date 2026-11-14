using CyberManagement.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CyberManagement.Infrastructure.Persistence.Configurations;

public class TeamConfiguration : IEntityTypeConfiguration<Team>
{
    public void Configure(EntityTypeBuilder<Team> builder)
    {
        builder.ToTable("Teams");
        builder.HasKey(x => x.TeamId);

        builder.Property(x => x.TeamName).HasMaxLength(100).IsRequired();
        builder.Property(x => x.AvgSkillScore).HasPrecision(6, 2);

        builder.HasOne(x => x.Tournament)
            .WithMany(x => x.Teams)
            .HasForeignKey(x => x.TournamentId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
