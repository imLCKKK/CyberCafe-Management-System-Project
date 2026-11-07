using CyberManagement.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CyberManagement.Infrastructure.Persistence.Configurations;

public class OrderDetailConfiguration : IEntityTypeConfiguration<OrderDetail>
{
    public void Configure(EntityTypeBuilder<OrderDetail> builder)
    {
        builder.ToTable("OrderDetails", t =>
        {
            t.HasCheckConstraint("CK_OrderDetails_Values", "[Quantity] > 0 AND [UnitPrice] >= 0 AND [DiscountAmount] >= 0 AND [DiscountAmount] <= [UnitPrice] * [Quantity]");
            t.HasCheckConstraint("CK_OrderDetails_NameSnapshot", "LEN(LTRIM(RTRIM([ProductNameSnapshot]))) > 0");
        });
        builder.Property(x => x.ProductNameSnapshot).HasMaxLength(150).IsRequired();
        builder.Property(x => x.DiscountAmount).HasPrecision(18, 0);
        builder.HasKey(x => x.OrderDetailId);

        builder.Property(x => x.UnitPrice).HasPrecision(18, 0);

        builder.HasOne(x => x.Order)
            .WithMany(x => x.Details)
            .HasForeignKey(x => x.OrderId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne(x => x.Product)
            .WithMany()
            .HasForeignKey(x => x.ProductId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
