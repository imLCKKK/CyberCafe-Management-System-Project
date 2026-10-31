namespace CyberManagement.Infrastructure.Persistance.Configuration;

public class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> builder)
    {
        builder.ToTable("Orders", t =>
            t.HasCheckConstraint(
                "CK_Orders_TotalAmount", "[TotalAmount] >= 0"));

        builder.HasKey(x => x.OrderId);
        builder.Property(x => x.TotalAmount).HasPrecision(18, 0);
        builder.Property(x => x.Status).HasMaxLength(30);

        builder.HasOne<Customers>()
            .WithMany(x => x.Orders)
            .HasForeignKey(x => x.CustomerId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne<GamingSessions>()
            .WithMany(x => x.Orders)
            .HasForeignKey(x => x.SessionId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne<Employees>()
            .WithMany()
            .HasForeignKey(x => x.EmployeeId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasIndex(x => new { x.CustomerId, x.OrderTime });
    }
}