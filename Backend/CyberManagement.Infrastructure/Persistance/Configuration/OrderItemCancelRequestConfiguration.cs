namespace CyberManagement.Infrastructure.Persistance.Configuration;

public class OrderItemCancelRequestConfiguration
    : IEntityTypeConfiguration<OrderItemCancelRequest>
{
    public void Configure(
        EntityTypeBuilder<OrderItemCancelRequest> builder)
    {
        builder.ToTable("OrderItemCancelRequests", t =>
            t.HasCheckConstraint(
                "CK_CancelRequests_Status",
                "[Status] IN ('Pending', 'Approved', 'Rejected')"));

        builder.HasKey(x => x.RequestId);

        builder.Property(x => x.Status)
            .HasMaxLength(30)
            .HasDefaultValue("Pending");

        builder.HasOne(x => x.OrderDetail)
            .WithMany(x => x.CancellationRequests)
            .HasForeignKey(x => x.OrderDetailId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne<Customers>()
            .WithMany()
            .HasForeignKey(x => x.RequestedByCustomerId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasIndex(x => x.OrderDetailId)
            .IsUnique()
            .HasFilter("[Status] = 'Pending'");
    }
}