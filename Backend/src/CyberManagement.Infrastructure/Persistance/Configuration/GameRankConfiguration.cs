using CyberManagement.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CyberManagement.Infrastructure.Persistence.Configurations;

public class GameRankConfiguration : IEntityTypeConfiguration<GameRank>
{
    public void Configure(EntityTypeBuilder<GameRank> builder)
    {
        builder.ToTable("GameRanks");
        builder.HasKey(x => x.RankId);

        builder.Property(x => x.RankName).HasMaxLength(50).IsRequired();

        builder.HasIndex(x => new { x.GameId, x.RankName }).IsUnique();

        builder.HasOne(x => x.Game)
            .WithMany(x => x.Ranks)
            .HasForeignKey(x => x.GameId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
