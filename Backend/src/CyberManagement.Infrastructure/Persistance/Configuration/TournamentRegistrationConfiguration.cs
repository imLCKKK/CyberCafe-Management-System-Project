using CyberManagement.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CyberManagement.Infrastructure.Persistence.Configurations;

public class TournamentRegistrationConfiguration : IEntityTypeConfiguration<TournamentRegistration>
{
    public void Configure(EntityTypeBuilder<TournamentRegistration> builder)
    {
        builder.ToTable("TournamentRegistrations");
        builder.HasKey(x => x.RegistrationId);

        builder.Property(x => x.PreferredRole).HasMaxLength(50);
        builder.Property(x => x.WinRate).HasPrecision(5, 2);
        builder.Property(x => x.SkillScore).HasPrecision(6, 2);
        builder.Property(x => x.Status).HasMaxLength(50).IsRequired();

        builder.HasIndex(x => new { x.TournamentId, x.CustomerId }).IsUnique();

        builder.HasOne(x => x.Tournament)
            .WithMany(x => x.Registrations)
            .HasForeignKey(x => x.TournamentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Customer)
            .WithMany()
            .HasForeignKey(x => x.CustomerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Rank)
            .WithMany()
            .HasForeignKey(x => x.RankId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
