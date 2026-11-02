namespace CyberManagement.Infrastructure.Persistance.Configuration;

public class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> builder)
    {
        builder.ToTable("Orders", table =>
        {
            table.HasCheckConstraint(
                "CK_Orders_TotalAmount",
                "[TotalAmount] >= 0");

            table.HasCheckConstraint(
                "CK_Orders_DiscountAmount",
                "[DiscountAmount] >= 0");

            table.HasCheckConstraint(
                "CK_Orders_VoucherSnapshot",
                """
        (
            [VoucherCodeSnapshot] IS NULL
            AND [VoucherDiscountTypeSnapshot] IS NULL
            AND [VoucherDiscountValueSnapshot] IS NULL
            AND [DiscountAmount] = 0
        )
        OR
        (
            [VoucherCodeSnapshot] IS NOT NULL
            AND LEN(LTRIM(RTRIM([VoucherCodeSnapshot]))) > 0
            AND [VoucherDiscountTypeSnapshot] IS NOT NULL
            AND [VoucherDiscountValueSnapshot] IS NOT NULL
            AND
            (
                (
                    [VoucherDiscountTypeSnapshot] = 'Percent'
                    AND [VoucherDiscountValueSnapshot] > 0
                    AND [VoucherDiscountValueSnapshot] <= 100
                )
                OR
                (
                    [VoucherDiscountTypeSnapshot] = 'Fixed'
                    AND [VoucherDiscountValueSnapshot] > 0
                    AND [VoucherDiscountValueSnapshot]
                        = ROUND([VoucherDiscountValueSnapshot], 0)
                )
            )
        )
        """);
        });

        builder.Property(x => x.VoucherCodeSnapshot)
            .HasMaxLength(50);

        builder.Property(x => x.VoucherDiscountTypeSnapshot)
            .HasMaxLength(10);

        builder.Property(x => x.VoucherDiscountValueSnapshot)
            .HasPrecision(18, 2);

        builder.Property(x => x.DiscountAmount)
            .HasPrecision(18, 0)
            .HasDefaultValue(0m);

        builder.HasKey(x => x.OrderId);

        builder.Property(x => x.TotalAmount)
            .HasPrecision(18, 0);

        builder.Property(x => x.Status)
            .HasMaxLength(30);

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