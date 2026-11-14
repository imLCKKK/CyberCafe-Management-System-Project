using CyberManagement.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CyberManagement.Infrastructure.Persistence.Configurations;

public sealed class CounterTopUpRequestConfiguration : IEntityTypeConfiguration<CounterTopUpRequest>
{
    public void Configure(EntityTypeBuilder<CounterTopUpRequest> builder)
    {
        builder.ToTable("CounterTopUpRequests", table =>
        {
            table.HasCheckConstraint("CK_CounterTopUpRequests_Amount", "[Amount] IN (10000,20000,30000,50000,100000,200000,300000,500000)");
            table.HasCheckConstraint("CK_CounterTopUpRequests_Bonus", "[BonusAmount] >= 0 AND [TotalAmount] = [Amount] + [BonusAmount]");
            table.HasCheckConstraint("CK_CounterTopUpRequests_State", "([Status] = N'Pending' AND [ApprovedAt] IS NULL AND [ApprovedByEmployeeId] IS NULL AND [CashAmountReceived] IS NULL AND [WalletTransactionId] IS NULL) OR ([Status] = N'Approved' AND [ApprovedAt] IS NOT NULL AND [ApprovedAt] >= [CreatedAt] AND [ApprovedByEmployeeId] IS NOT NULL AND [CashAmountReceived] IS NOT NULL AND [CashAmountReceived] = [Amount] AND [WalletTransactionId] IS NOT NULL)");
        });
        builder.HasKey(row => row.RequestId);
        builder.Property(row => row.Amount).HasPrecision(18, 0);
        builder.Property(row => row.BonusAmount).HasPrecision(18, 0);
        builder.Property(row => row.TotalAmount).HasPrecision(18, 0);
        builder.Property(row => row.CashAmountReceived).HasPrecision(18, 0);
        builder.Property(row => row.Status).HasMaxLength(20).UseCollation("Latin1_General_100_BIN2").IsRequired();
        builder.HasIndex(row => new { row.CustomerId, row.RequestKey }).IsUnique();
        builder.HasIndex(row => row.CustomerId).IsUnique().HasFilter("[Status] = N'Pending'").HasDatabaseName("UX_CounterTopUpRequests_PendingCustomer");
        builder.HasIndex(row => row.WalletTransactionId).IsUnique().HasFilter("[WalletTransactionId] IS NOT NULL");
        builder.HasOne(row => row.Customer).WithMany().HasForeignKey(row => row.CustomerId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(row => row.ApprovedByEmployee).WithMany().HasForeignKey(row => row.ApprovedByEmployeeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(row => row.WalletTransaction).WithMany().HasForeignKey(row => row.WalletTransactionId).OnDelete(DeleteBehavior.Restrict);
    }
}
