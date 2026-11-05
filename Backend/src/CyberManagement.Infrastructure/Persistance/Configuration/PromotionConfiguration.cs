using CyberManagement.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CyberManagement.Infrastructure.Persistence.Configurations;

public class PromotionConfiguration : IEntityTypeConfiguration<Promotion>
{
    public void Configure(EntityTypeBuilder<Promotion> builder)
    {
        builder.ToTable("Promotions");
        builder.HasKey(x => x.PromotionId);

        builder.Property(x => x.PromoName).HasMaxLength(150).IsRequired();
        builder.Property(x => x.DiscountType).HasMaxLength(50).IsRequired();
        builder.Property(x => x.DiscountValue).HasPrecision(12, 2);
    }
}
