using CyberManagement.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CyberManagement.Infrastructure.Persistence.Configurations;

public class RefundAttemptConfiguration : IEntityTypeConfiguration<RefundAttempt>
{
    public void Configure(EntityTypeBuilder<RefundAttempt> builder)
    {
        builder.ToTable("RefundAttempts", t =>
        {
            t.HasCheckConstraint("CK_RefundAttempts_Values", "[ProviderAppId] > 0 AND LEN(LTRIM(RTRIM([MerchantRefundId]))) > 0 AND [Status] IN ('Created','Submitted','Processing','NotFound','Failed','Succeeded') AND ([LastApi] IS NULL OR [LastApi] IN ('Refund','QueryRefund'))");
        });
        builder.HasKey(x => x.RefundAttemptId);
        builder.Property(x => x.MerchantRefundId).HasMaxLength(45).IsRequired();
        builder.Property(x => x.GatewayRefundId).HasMaxLength(50);
        builder.Property(x => x.Status).HasMaxLength(10).IsRequired();
        builder.Property(x => x.LastApi).HasMaxLength(12);
        builder.HasIndex(x => new { x.ProviderAppId, x.MerchantRefundId }).IsUnique();
        builder.HasIndex(x => new { x.ProviderAppId, x.GatewayRefundId }).IsUnique().HasFilter("[GatewayRefundId] IS NOT NULL");
        builder.HasOne(x => x.Payment).WithMany().HasForeignKey(x => x.PaymentId).OnDelete(DeleteBehavior.NoAction);
    }
}
