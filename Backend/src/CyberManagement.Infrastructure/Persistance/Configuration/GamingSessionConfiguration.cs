using CyberManagement.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CyberManagement.Infrastructure.Persistence.Configurations;

public class GamingSessionConfiguration : IEntityTypeConfiguration<GamingSession>
{
    public void Configure(EntityTypeBuilder<GamingSession> builder)
    {
        builder.ToTable("GamingSessions");
        builder.HasKey(x => x.GamingSessionId);

        builder.Property(x => x.TotalCost).HasPrecision(18, 0);
        builder.Property(x => x.Status).HasMaxLength(50).IsRequired();

        builder.HasOne(x => x.Computer)
            .WithMany(x => x.GamingSessions)
            .HasForeignKey(x => x.ComputerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Customer)
            .WithMany(x => x.GamingSessions)
            .HasForeignKey(x => x.CustomerId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
