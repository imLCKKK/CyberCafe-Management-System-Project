using CyberNet.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CyberNet.Data.Configurations;

public class WalletTransactionConfiguration : IEntityTypeConfiguration<WalletTransaction>
{
    public void Configure(EntityTypeBuilder<WalletTransaction> builder)
    {
        builder.ToTable("WalletTransactions", t =>
        {
            t.HasCheckConstraint("CK_WalletTx_Amount", "[Amount] > 0");
            t.HasCheckConstraint("CK_WalletTx_Type",
                "[Type] IN (N'Deposit', N'Payment', N'Refund')");
        });
        builder.HasKey(x => x.TransactionId);

        builder.Property(x => x.Amount).HasColumnType("decimal(12,2)");
        builder.Property(x => x.Type).IsRequired().HasMaxLength(50);
        builder.Property(x => x.CreatedAt)
               .HasColumnType("datetime")
               .HasDefaultValueSql("GETDATE()");

        builder.HasOne(x => x.Customer)
               .WithMany(c => c.WalletTransactions)
               .HasForeignKey(x => x.CustomerId)
               .OnDelete(DeleteBehavior.Restrict);
    }
}
