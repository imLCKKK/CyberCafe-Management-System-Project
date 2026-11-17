using CyberManagement.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CyberManagement.Infrastructure.Persistence.Configurations;

public sealed class ZaloPayIntentConfiguration : IEntityTypeConfiguration<ZaloPayIntent>
{
    public void Configure(EntityTypeBuilder<ZaloPayIntent> builder)
    {
        builder.ToTable("ZaloPayIntents", table =>
        {
            table.HasCheckConstraint("CK_ZaloPayIntents_Values", "[CustomerId] > 0 AND [ProviderAppId] > 0 AND [ExpectedAmountVnd] BETWEEN 1 AND 999999999 AND [Status] IN (N'Pending',N'Succeeded',N'Failed',N'Expired') AND LEN([OperationKey]) > 0 AND [ExpiresAtUtc] > [CreatedAtUtc]");
            table.HasCheckConstraint("CK_ZaloPayIntents_MerchantId", "LEN([MerchantTransactionId]) = 39 AND SUBSTRING([MerchantTransactionId],1,6) NOT LIKE '%[^0-9]%' AND SUBSTRING([MerchantTransactionId],7,1) = '_' AND SUBSTRING([MerchantTransactionId],8,32) NOT LIKE '%[^0-9a-f]%'");
            table.HasCheckConstraint("CK_ZaloPayIntents_Source", "([Purpose] = N'TopUp' AND [OrderId] IS NULL) OR ([Purpose] = N'Order' AND [OrderId] IS NOT NULL)");
            table.HasCheckConstraint("CK_ZaloPayIntents_Receipt", "([ProviderTransactionId] IS NULL AND [ProviderAmountVnd] IS NULL AND [ProviderOccurredAtUtc] IS NULL AND [VerifiedAtUtc] IS NULL AND [Status] <> N'Succeeded') OR ([ProviderTransactionId] IS NOT NULL AND TRY_CONVERT(bigint,[ProviderTransactionId]) IS NOT NULL AND TRY_CONVERT(bigint,[ProviderTransactionId]) > 0 AND CONVERT(varchar(19),TRY_CONVERT(bigint,[ProviderTransactionId])) = [ProviderTransactionId] AND [ProviderTransactionId] NOT LIKE '%[^0-9]%' AND [ProviderAmountVnd] IS NOT NULL AND [ProviderAmountVnd] > 0 AND [VerifiedAtUtc] IS NOT NULL AND ([Status] = N'Succeeded' OR [RequiresReconciliation] = 1))");
            table.HasCheckConstraint("CK_ZaloPayIntents_ConfirmedAmount", "[Status] <> N'Succeeded' OR ([ProviderAmountVnd] IS NOT NULL AND [ProviderAmountVnd] = [ExpectedAmountVnd])");
            table.HasCheckConstraint("CK_ZaloPayIntents_QueryLease", "([QueryLeaseToken] IS NULL AND [QueryLeaseUntilUtc] IS NULL) OR ([QueryLeaseToken] IS NOT NULL AND [QueryLeaseUntilUtc] IS NOT NULL)");
            table.HasCheckConstraint("CK_ZaloPayIntents_Applied", "([AppliedAtUtc] IS NULL AND [WalletTransactionId] IS NULL AND [PaymentId] IS NULL) OR ([AppliedAtUtc] IS NOT NULL AND [VerifiedAtUtc] IS NOT NULL AND [AppliedAtUtc] >= [VerifiedAtUtc] AND [Status] = N'Succeeded' AND [ProviderAmountVnd] IS NOT NULL AND [ProviderAmountVnd] = [ExpectedAmountVnd] AND (([Purpose] = N'TopUp' AND [WalletTransactionId] IS NOT NULL AND [PaymentId] IS NULL) OR ([Purpose] = N'Order' AND [PaymentId] IS NOT NULL AND [WalletTransactionId] IS NULL)))");
        });
        builder.HasKey(row => row.IntentId);
        builder.Property(row => row.Purpose).HasMaxLength(10).UseCollation("Latin1_General_100_BIN2").IsRequired();
        builder.Property(row => row.OperationKey).HasMaxLength(100).UseCollation("Latin1_General_100_BIN2").IsRequired();
        builder.Property(row => row.ExpectedAmountVnd).HasPrecision(18, 0);
        builder.Property(row => row.ProviderAmountVnd).HasPrecision(18, 0);
        builder.Property(row => row.MerchantTransactionId).HasMaxLength(40).UseCollation("Latin1_General_100_BIN2").IsRequired();
        builder.Property(row => row.Status).HasMaxLength(10).UseCollation("Latin1_General_100_BIN2").IsRequired();
        builder.Property(row => row.PaymentUrl).HasMaxLength(2048);
        builder.Property(row => row.ProviderTransactionId).HasMaxLength(19).UseCollation("Latin1_General_100_BIN2");
        builder.Property(row => row.FailureReason).HasMaxLength(80);
        builder.Property(row => row.SettlementFailureReason).HasMaxLength(80);
        builder.HasIndex(row => row.OperationKey).IsUnique();
        builder.HasIndex(row => row.MerchantTransactionId).IsUnique();
        builder.HasIndex(row => new { row.ProviderAppId, row.ProviderTransactionId }).IsUnique().HasFilter("[ProviderTransactionId] IS NOT NULL");
        builder.HasIndex(row => row.OrderId).IsUnique().HasFilter("[OrderId] IS NOT NULL");
        builder.HasIndex(row => new { row.Status, row.NextQueryAtUtc });
        builder.HasIndex(row => row.WalletTransactionId).IsUnique().HasFilter("[WalletTransactionId] IS NOT NULL");
        builder.HasIndex(row => row.PaymentId).IsUnique().HasFilter("[PaymentId] IS NOT NULL");
        builder.HasIndex(row => new { row.Status, row.AppliedAtUtc });
        builder.HasOne(row => row.Customer).WithMany().HasForeignKey(row => row.CustomerId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(row => row.Order).WithMany().HasForeignKey(row => row.OrderId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(row => row.WalletTransaction).WithMany().HasForeignKey(row => row.WalletTransactionId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne(row => row.Payment).WithMany().HasForeignKey(row => row.PaymentId).OnDelete(DeleteBehavior.NoAction);
    }
}
