using CyberManagement.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CyberManagement.Infrastructure.Persistence.Configurations;

public class SessionChargeConfiguration : IEntityTypeConfiguration<SessionCharge>
{
    public void Configure(EntityTypeBuilder<SessionCharge> builder)
    {
        builder.ToTable("SessionCharges", table => table.HasCheckConstraint("CK_SessionCharges_BillingMetadata",
            "[OperationKey] IS NULL OR (LEN(LTRIM(RTRIM([OperationKey]))) > 0 AND [Amount] > 0 AND [EndTime] > [StartTime])"));
        builder.HasKey(x => x.ChargeId);

        builder.Property(x => x.Amount).HasPrecision(18, 0);
        builder.Property(x => x.OperationKey).HasMaxLength(100).UseCollation("Latin1_General_100_BIN2");
        builder.HasIndex(x => x.OperationKey).IsUnique().HasFilter("[OperationKey] IS NOT NULL");
        builder.HasIndex(x => new { x.GamingSessionId, x.EndTime }).IsUnique();
        builder.HasIndex(x => x.WalletTransactionId).IsUnique().HasFilter("[WalletTransactionId] IS NOT NULL");
        builder.HasOne(x => x.WalletTransaction).WithMany().HasForeignKey(x => x.WalletTransactionId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne(x => x.GamingSession)
            .WithMany(x => x.Charges)
            .HasForeignKey(x => x.GamingSessionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
