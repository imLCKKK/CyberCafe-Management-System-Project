namespace CyberManagement.Infrastructure.Persistance.Configuration;

public class GamingSessionConfiguration
    : IEntityTypeConfiguration<GamingSessions>
{
    public void Configure(EntityTypeBuilder<GamingSessions> builder)
    {
        builder.ToTable("GamingSessions", t =>
        {
            t.HasCheckConstraint(
                "CK_GamingSessions_Cost", "[TotalCost] >= 0");

            t.HasCheckConstraint(
                "CK_GamingSessions_Time",
                "[EndTime] IS NULL OR [EndTime] >= [StartTime]");
        });

        builder.HasKey(x => x.SessionId);

        builder.Property(x => x.TotalCost).HasPrecision(18, 0);
        builder.Property(x => x.Status).HasMaxLength(30);

        builder.HasOne(x => x.Customer)
            .WithMany()
            .HasForeignKey(x => x.CustomerId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne(x => x.Computer)
            .WithMany(x => x.GamingSessions)
            .HasForeignKey(x => x.ComputerId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasIndex(x => x.CustomerId)
            .IsUnique()
            .HasFilter("[Status] = 'Active'");

        builder.HasIndex(x => x.ComputerId)
            .IsUnique()
            .HasFilter("[Status] = 'Active'");
    }
}