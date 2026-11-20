using CyberManagement.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CyberManagement.Infrastructure.Persistence.Configurations;

public class WalletTransactionConfiguration : IEntityTypeConfiguration<WalletTransaction>
{
    public void Configure(EntityTypeBuilder<WalletTransaction> builder)
    {
        builder.ToTable("WalletTransactions", table =>
        {
            table.HasCheckConstraint("CK_WalletTransactions_WriterMetadata", "([OperationKey] IS NULL AND [Kind] IS NULL AND [SourceType] IS NULL AND [SourceId] IS NULL AND [OriginalDebitId] IS NULL AND [ActorType] IS NULL AND [ActorEmployeeId] IS NULL) OR ([OperationKey] IS NOT NULL AND LEN(LTRIM(RTRIM([OperationKey]))) > 0 AND [Kind] IS NOT NULL AND [Kind] IN ('Credit','Debit','Refund') AND [SourceType] IS NOT NULL AND LEN(LTRIM(RTRIM([SourceType]))) > 0 AND [SourceId] IS NOT NULL AND LEN(LTRIM(RTRIM([SourceId]))) > 0 AND [ActorType] IS NOT NULL AND (([ActorType] = 'Staff' AND [ActorEmployeeId] IS NOT NULL) OR ([ActorType] = 'System' AND [ActorEmployeeId] IS NULL) OR ([ActorType] = 'Customer' AND [ActorEmployeeId] IS NULL AND [Kind] = 'Debit' AND [Type] = 'OrderPayment' AND [SourceType] = 'Order')) AND [BalanceAfter] >= 0 AND (([Kind] = 'Debit' AND [Amount] < 0 AND [OriginalDebitId] IS NULL) OR ([Kind] = 'Credit' AND [Amount] > 0 AND [OriginalDebitId] IS NULL) OR ([Kind] = 'Refund' AND [Amount] > 0 AND [OriginalDebitId] IS NOT NULL AND [OriginalDebitId] <> [TransactionId])))");
        });
        builder.HasKey(x => x.TransactionId);

        builder.Property(x => x.Amount).HasPrecision(18, 0);
        builder.Property(x => x.BalanceAfter).HasPrecision(18, 0);
        builder.Property(x => x.Type).HasMaxLength(50).IsRequired();
        builder.Property(x => x.Description).HasMaxLength(255);
        builder.Property(x => x.OperationKey).HasMaxLength(100).UseCollation("Latin1_General_100_BIN2");
        builder.Property(x => x.Kind).HasMaxLength(10);
        builder.Property(x => x.SourceType).HasMaxLength(50).UseCollation("Latin1_General_100_BIN2");
        builder.Property(x => x.SourceId).HasMaxLength(100).UseCollation("Latin1_General_100_BIN2");
        builder.Property(x => x.Type).UseCollation("Latin1_General_100_BIN2");
        builder.Property(x => x.ActorType).HasMaxLength(10);
        builder.HasIndex(x => x.OperationKey).IsUnique().HasFilter("[OperationKey] IS NOT NULL");
        builder.HasIndex(x => new { x.SourceType, x.SourceId, x.Type }).IsUnique()
            .HasFilter("[SourceType] IS NOT NULL AND [SourceId] IS NOT NULL");
        builder.HasIndex(x => x.OriginalDebitId);
        builder.HasOne<WalletTransaction>().WithMany().HasForeignKey(x => x.OriginalDebitId)
            .OnDelete(DeleteBehavior.NoAction);
        builder.HasOne<Employee>().WithMany().HasForeignKey(x => x.ActorEmployeeId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne(x => x.Customer)
            .WithMany(x => x.WalletTransactions)
            .HasForeignKey(x => x.CustomerId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
