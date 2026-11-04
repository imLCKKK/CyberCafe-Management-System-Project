using CyberNet.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CyberNet.Data.Configurations;

public class OrderDetailConfiguration : IEntityTypeConfiguration<OrderDetail>
{
    public void Configure(EntityTypeBuilder<OrderDetail> builder)
    {
        builder.ToTable("OrderDetails", t =>
        {
            t.HasCheckConstraint("CK_OrderDetails_Qty", "[Quantity] > 0");
            t.HasCheckConstraint("CK_OrderDetails_Price", "[UnitPrice] >= 0");
        });
        builder.HasKey(x => x.OrderDetailId);

        builder.Property(x => x.UnitPrice).HasColumnType("decimal(12,2)");

        builder.HasOne(x => x.Order)
               .WithMany(o => o.OrderDetails)
               .HasForeignKey(x => x.OrderId)
               .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Product)
               .WithMany(p => p.OrderDetails)
               .HasForeignKey(x => x.ProductId)
               .OnDelete(DeleteBehavior.Restrict);
    }
}
