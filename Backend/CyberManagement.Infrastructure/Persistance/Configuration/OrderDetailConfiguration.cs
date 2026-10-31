namespace CyberManagement.Infrastructure.Persistance.Configuration;

public class OrderDetailConfiguration
    : IEntityTypeConfiguration<OrderDetail>
{
    public void Configure(EntityTypeBuilder<OrderDetail> builder)
    {
        builder.ToTable("OrderDetails", t =>
        {
            t.HasCheckConstraint(
                "CK_OrderDetails_Values",
                "[Quantity] > 0 AND [UnitPrice] >= 0");

            t.HasCheckConstraint(
                "CK_OrderDetails_Status",
                "[Status] IN ('Pending', 'Confirmed', 'Cancelled')");
        });

        builder.HasKey(x => x.OrderDetailId);
        builder.Property(x => x.UnitPrice).HasPrecision(18, 0);
        builder.Property(x => x.CancelReason).HasMaxLength(250);

        builder.Property(x => x.Status)
            .HasMaxLength(30)
            .HasDefaultValue("Pending");

        builder.HasOne(x => x.Order)
            .WithMany(x => x.OrderDetails)
            .HasForeignKey(x => x.OrderId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne(x => x.Product)
            .WithMany(x => x.OrderDetails)
            .HasForeignKey(x => x.ProductId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}