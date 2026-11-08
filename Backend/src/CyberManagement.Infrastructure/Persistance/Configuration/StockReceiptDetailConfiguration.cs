using CyberManagement.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CyberManagement.Infrastructure.Persistence.Configurations;

public class StockReceiptDetailConfiguration : IEntityTypeConfiguration<StockReceiptDetail>
{
    public void Configure(EntityTypeBuilder<StockReceiptDetail> builder)
    {
        builder.ToTable("StockReceiptDetails");
        builder.HasKey(x => x.ReceiptDetailId);

        builder.Property(x => x.UnitCost).HasPrecision(18, 0);

        builder.HasOne(x => x.StockReceipt)
            .WithMany(x => x.Details)
            .HasForeignKey(x => x.ReceiptId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Product)
            .WithMany()
            .HasForeignKey(x => x.ProductId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
