namespace CyberManagement.Infrastructure.Persistance.Configuration;

public class OrderStatusHistoryConfiguration
    : IEntityTypeConfiguration<OrderStatusHistory>
{
    public void Configure(EntityTypeBuilder<OrderStatusHistory> builder)
    {
        builder.ToTable("OrderStatusHistories");
        builder.HasKey(x => x.HistoryId);

        builder.Property(x => x.OldStatus).HasMaxLength(30);
        builder.Property(x => x.NewStatus).HasMaxLength(30);

        builder.HasOne(x => x.Order)
            .WithMany(x => x.StatusHistories)
            .HasForeignKey(x => x.OrderId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne(x => x.Employee)
            .WithMany(x => x.OrderStatusHistories)
            .HasForeignKey(x => x.ChangedBy)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne<OrderDetail>()
            .WithMany()
            .HasForeignKey(x => x.OrderDetailId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}