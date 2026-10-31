namespace CyberManagement.Infrastructure.Persistance.Configuration;

public class PromotionConfiguration : IEntityTypeConfiguration<Promotions>
{
    public void Configure(EntityTypeBuilder<Promotions> builder)
    {
        builder.ToTable("Promotions");
        builder.HasKey(x => x.PromotionId);

        builder.Property(x => x.PromotionName).HasMaxLength(150);
        builder.Property(x => x.DiscountType).HasMaxLength(30);

        // Có thể biểu diễn phần trăm, nên giữ hai chữ số thập phân.
        builder.Property(x => x.DiscountValue).HasPrecision(18, 2);
    }
}