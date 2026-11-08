using CyberManagement.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CyberManagement.Infrastructure.Persistence.Configurations;

public class WalletTransactionConfiguration : IEntityTypeConfiguration<WalletTransaction>
{
    public void Configure(EntityTypeBuilder<WalletTransaction> builder)
    {
        builder.ToTable("WalletTransactions");
        builder.HasKey(x => x.TransactionId);

        builder.Property(x => x.Amount).HasPrecision(18, 0);
        builder.Property(x => x.BalanceAfter).HasPrecision(18, 0);
        builder.Property(x => x.Type).HasMaxLength(50).IsRequired();
        builder.Property(x => x.Description).HasMaxLength(255);

        builder.HasOne(x => x.Customer)
            .WithMany(x => x.WalletTransactions)
            .HasForeignKey(x => x.CustomerId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
