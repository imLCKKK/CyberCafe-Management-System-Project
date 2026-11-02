using CyberManagement.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CyberManagement.Infrastructure.Persistence.Configurations;

public class TournamentConfiguration : IEntityTypeConfiguration<Tournament>
{
    public void Configure(EntityTypeBuilder<Tournament> builder)
    {
        builder.ToTable("Tournaments");
        builder.HasKey(x => x.TournamentId);

        builder.Property(x => x.TournamentName).HasMaxLength(150).IsRequired();
        builder.Property(x => x.EntryFee).HasPrecision(12, 2);
        builder.Property(x => x.Status).HasMaxLength(50).IsRequired();

        builder.HasOne(x => x.Game)
            .WithMany(x => x.Tournaments)
            .HasForeignKey(x => x.GameId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.CreatedByEmployee)
            .WithMany()
            .HasForeignKey(x => x.CreatedBy)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
