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
        builder.Property(x => x.Version).IsRowVersion();
        builder.Property(x => x.CreateRequestKey).HasMaxLength(100).UseCollation("Latin1_General_100_BIN2");
        builder.Property(x => x.CreateRequestFingerprint).HasMaxLength(64).IsUnicode(false);
        builder.HasIndex(x => new { x.CreatedBy, x.CreateRequestKey }).IsUnique()
            .HasFilter("[CreateRequestKey] IS NOT NULL");

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
