using CyberManagement.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CyberManagement.Infrastructure.Persistence.Configurations;

public class GamingSessionConfiguration : IEntityTypeConfiguration<GamingSession>
{
    public void Configure(EntityTypeBuilder<GamingSession> builder)
    {
        builder.ToTable("GamingSessions", table =>
        {
            table.HasCheckConstraint("CK_GamingSessions_RateSnapshot", "[HourlyRateSnapshot] IS NULL OR [HourlyRateSnapshot] > 0");
            table.HasCheckConstraint("CK_GamingSessions_OperationKeys",
                "([OpenOperationKey] IS NULL OR LEN(LTRIM(RTRIM([OpenOperationKey]))) > 0) AND ([CloseOperationKey] IS NULL OR LEN(LTRIM(RTRIM([CloseOperationKey]))) > 0)");
        });
        builder.Property(x => x.ClosedByActorType).HasMaxLength(20);
        builder.HasKey(x => x.GamingSessionId);

        builder.Property(x => x.TotalCost).HasPrecision(18, 0);
        builder.Property(x => x.PricePerHourSnapshot).HasPrecision(18, 0);
        builder.Property(x => x.Status).HasMaxLength(50).IsRequired();
        builder.Property(x => x.HourlyRateSnapshot).HasPrecision(18, 0);
        builder.Property(x => x.CloseReason).HasMaxLength(250);
        builder.Property(x => x.OpenOperationKey).HasMaxLength(100).UseCollation("Latin1_General_100_BIN2");
        builder.Property(x => x.CloseOperationKey).HasMaxLength(100).UseCollation("Latin1_General_100_BIN2");
        builder.HasIndex(x => x.OpenOperationKey).IsUnique().HasFilter("[OpenOperationKey] IS NOT NULL");
        builder.HasIndex(x => x.CloseOperationKey).IsUnique().HasFilter("[CloseOperationKey] IS NOT NULL");

        // A single slot covers the union of both unsafe states, including inconsistent legacy data.
        // Closed rows use their positive PK so their history never occupies the open slot (zero).
        builder.Property<int>("OpenSessionSlot").HasComputedColumnSql(
            "CASE WHEN [Status] = N'Active' OR [EndTime] IS NULL THEN 0 ELSE [GamingSessionId] END", stored: true);
        builder.HasIndex(nameof(GamingSession.ComputerId), "OpenSessionSlot")
            .IsUnique().HasDatabaseName("UX_GamingSessions_OpenComputer");
        builder.HasIndex(nameof(GamingSession.CustomerId), "OpenSessionSlot")
            .IsUnique().HasDatabaseName("UX_GamingSessions_OpenCustomer");

        builder.HasOne<Employee>().WithMany().HasForeignKey(x => x.OpenedByEmployeeId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne<Employee>().WithMany().HasForeignKey(x => x.ClosedByEmployeeId).OnDelete(DeleteBehavior.NoAction);

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
