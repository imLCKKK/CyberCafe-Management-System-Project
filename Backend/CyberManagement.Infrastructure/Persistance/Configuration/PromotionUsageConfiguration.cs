using System;
using System.Collections.Generic;
using System.Text;

namespace CyberManagement.Infrastructure.Persistance.Configuration
{
    public class PromotionUsageConfiguration : IEntityTypeConfiguration<PromotionUsage>
    {
        public void Configure(EntityTypeBuilder<PromotionUsage> builder)
        {
            builder.ToTable("PromotionUsages", table =>
            {
                table.HasCheckConstraint(
                    "CK_PromotionUsages_ReleasedAt",
                    "[ReleasedAt] IS NULL OR [ReleasedAt] >= [UsedAt]");
            });

            builder.HasKey(x => x.PromotionUsageId);

            // Một đơn chỉ có một bản ghi sử dụng voucher.
            builder.HasIndex(x => x.OrderId)
                .IsUnique();

            // Hỗ trợ đếm lượt chưa hoàn theo voucher và khách hàng.
            builder.HasIndex(x => new { x.PromotionId, x.CustomerId })
                .HasFilter("[ReleasedAt] IS NULL");

            builder.Property(x => x.UsedAt)
                .HasColumnType("datetimeoffset");

            builder.Property(x => x.ReleasedAt)
                .HasColumnType("datetimeoffset");

            builder.HasOne(x => x.Promotion)
                .WithMany()
                .HasForeignKey(x => x.PromotionId)
                .OnDelete(DeleteBehavior.NoAction);

            builder.HasOne(x => x.Order)
                .WithMany()
                .HasForeignKey(x => x.OrderId)
                .OnDelete(DeleteBehavior.NoAction);

            builder.HasOne(x => x.Customer)
                .WithMany()
                .HasForeignKey(x => x.CustomerId)
                .OnDelete(DeleteBehavior.NoAction);
        }
    }
}
