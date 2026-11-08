using CyberManagement.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CyberManagement.Infrastructure.Persistence.Configurations;

public class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> builder)
    {
        builder.ToTable("Orders", t =>
        {
            t.HasCheckConstraint("CK_Orders_Amounts", "[TotalAmount] >= 0 AND [DiscountAmount] >= 0");
            t.HasCheckConstraint("CK_Orders_VoucherSnapshot", "([VoucherCodeSnapshot] IS NULL AND [VoucherDiscountTypeSnapshot] IS NULL AND [VoucherDiscountValueSnapshot] IS NULL AND [DiscountAmount] = 0) OR ([VoucherCodeSnapshot] IS NOT NULL AND LEN(LTRIM(RTRIM([VoucherCodeSnapshot]))) > 0 AND [VoucherDiscountTypeSnapshot] IS NOT NULL AND [VoucherDiscountValueSnapshot] IS NOT NULL AND (([VoucherDiscountTypeSnapshot] = 'Percent' AND [VoucherDiscountValueSnapshot] > 0 AND [VoucherDiscountValueSnapshot] <= 100) OR ([VoucherDiscountTypeSnapshot] = 'Fixed' AND [VoucherDiscountValueSnapshot] > 0 AND [VoucherDiscountValueSnapshot] = ROUND([VoucherDiscountValueSnapshot], 0))))");
        });
        builder.Property(x => x.VoucherCodeSnapshot).HasMaxLength(50);
        builder.Property(x => x.VoucherDiscountTypeSnapshot).HasMaxLength(10);
        builder.Property(x => x.VoucherDiscountValueSnapshot).HasPrecision(18, 2);
        builder.Property(x => x.DiscountAmount).HasPrecision(18, 0);
        builder.HasKey(x => x.OrderId);

        builder.Property(x => x.Status).HasMaxLength(50).IsRequired();
        builder.Property(x => x.TotalAmount).HasPrecision(18, 0);

        builder.HasOne(x => x.Customer)
            .WithMany(x => x.Orders)
            .HasForeignKey(x => x.CustomerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.GamingSession)
            .WithMany()
            .HasForeignKey(x => x.GamingSessionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Employee)
            .WithMany()
            .HasForeignKey(x => x.EmployeeId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
