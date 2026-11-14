using CyberManagement.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CyberManagement.Infrastructure.Persistence.Configurations;

public class PromotionConfiguration : IEntityTypeConfiguration<Promotion>
{
    public void Configure(EntityTypeBuilder<Promotion> builder)
    {
        builder.ToTable("Promotions", t =>
        {
            t.HasCheckConstraint("CK_Promotions_Code", "LEN([Code]) BETWEEN 1 AND 50 AND [Code] COLLATE Latin1_General_100_BIN2 NOT LIKE '%[^-A-Z0-9_]%' COLLATE Latin1_General_100_BIN2");
            t.HasCheckConstraint("CK_Promotions_Discount", "([DiscountType] = 'Percent' AND [DiscountValue] > 0 AND [DiscountValue] <= 100) OR ([DiscountType] = 'Fixed' AND [DiscountValue] > 0 AND [DiscountValue] = ROUND([DiscountValue], 0))");
            t.HasCheckConstraint("CK_Promotions_Limits", "[MinOrderAmount] >= 0 AND ([MaxDiscountAmount] IS NULL OR [MaxDiscountAmount] > 0) AND ([TotalUsageLimit] IS NULL OR [TotalUsageLimit] > 0) AND ([PerCustomerUsageLimit] IS NULL OR [PerCustomerUsageLimit] > 0)");
            t.HasCheckConstraint("CK_Promotions_Dates", "[StartDate] < [EndDate]");
        });
        builder.HasKey(x => x.PromotionId);
        builder.Property(x => x.Code).HasMaxLength(50).UseCollation("Latin1_General_100_CI_AS").IsRequired();
        builder.HasIndex(x => x.Code).IsUnique();
        builder.Property(x => x.PromoName).HasMaxLength(150).IsRequired();
        builder.Property(x => x.DiscountType).HasMaxLength(10).IsRequired();
        builder.Property(x => x.DiscountValue).HasPrecision(18, 2);
        builder.Property(x => x.MinOrderAmount).HasPrecision(18, 0);
        builder.Property(x => x.MaxDiscountAmount).HasPrecision(18, 0);
    }
}
