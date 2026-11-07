using CyberManagement.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CyberManagement.Infrastructure.Persistence.Configurations;

public class PromotionUsageConfiguration : IEntityTypeConfiguration<PromotionUsage>
{
    public void Configure(EntityTypeBuilder<PromotionUsage> builder)
    {
        builder.ToTable("PromotionUsages", t => t.HasCheckConstraint("CK_PromotionUsages_Release", "[ReleasedAt] IS NULL OR [ReleasedAt] >= [UsedAt]"));
        builder.HasKey(x => x.PromotionUsageId);
        builder.HasIndex(x => x.OrderId).IsUnique();
        builder.HasIndex(x => new { x.PromotionId, x.CustomerId }).HasFilter("[ReleasedAt] IS NULL");
        builder.HasOne(x => x.Promotion).WithMany().HasForeignKey(x => x.PromotionId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne(x => x.Order).WithMany().HasForeignKey(x => x.OrderId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne(x => x.Customer).WithMany().HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.NoAction);
    }
}
