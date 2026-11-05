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

        builder.Property(x => x.Amount).HasPrecision(12, 2);
        builder.Property(x => x.BalanceAfter).HasPrecision(12, 2);
        builder.Property(x => x.Type).HasMaxLength(50).IsRequired();
        builder.Property(x => x.Description).HasMaxLength(255);
        builder.Property(x => x.IdempotencyKey).HasMaxLength(100);

        // Gửi lại cùng một yêu cầu (cùng khách, cùng khóa) không thể ghi ví lần thứ hai; chỉ áp dụng khi có khóa.
        builder.HasIndex(x => new { x.CustomerId, x.IdempotencyKey })
            .IsUnique()
            .HasFilter("[IdempotencyKey] IS NOT NULL")
            .HasDatabaseName("UX_WalletTransactions_Customer_IdempotencyKey");

        builder.HasOne(x => x.Customer)
            .WithMany(x => x.WalletTransactions)
            .HasForeignKey(x => x.CustomerId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
