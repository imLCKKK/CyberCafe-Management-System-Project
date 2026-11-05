using CyberNet.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CyberNet.Data.Configurations;

public class GamingSessionConfiguration : IEntityTypeConfiguration<GamingSession>
{
    public void Configure(EntityTypeBuilder<GamingSession> builder)
    {
        builder.ToTable("GamingSessions", t =>
        {
            t.HasCheckConstraint("CK_Sessions_Time", "[EndTime] IS NULL OR [EndTime] >= [StartTime]");
            t.HasCheckConstraint("CK_Sessions_Cost", "[TotalCost] IS NULL OR [TotalCost] >= 0");
        });
        builder.HasKey(x => x.SessionId);

        builder.Property(x => x.StartTime).HasColumnType("datetime");
        builder.Property(x => x.EndTime).HasColumnType("datetime");
        builder.Property(x => x.TotalCost).HasColumnType("decimal(12,2)");

        builder.HasOne(x => x.Computer)
               .WithMany(c => c.GamingSessions)
               .HasForeignKey(x => x.ComputerId)
               .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Customer)
               .WithMany(c => c.GamingSessions)
               .HasForeignKey(x => x.CustomerId)
               .OnDelete(DeleteBehavior.Restrict);

        // Mỗi máy chỉ có tối đa 1 phiên đang chơi (EndTime IS NULL)
        builder.HasIndex(x => x.ComputerId)
               .IsUnique()
               .HasFilter("[EndTime] IS NULL")
               .HasDatabaseName("UX_Sessions_ActivePerComputer");
    }
}
