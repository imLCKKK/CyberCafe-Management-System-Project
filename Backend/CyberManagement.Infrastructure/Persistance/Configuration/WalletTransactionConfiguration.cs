namespace CyberManagement.Infrastructure.Persistance.Configuration;

public class WalletTransactionConfiguration
    : IEntityTypeConfiguration<WalletTransactions>
{
    public void Configure(EntityTypeBuilder<WalletTransactions> builder)
    {
        builder.ToTable("WalletTransactions", t =>
            t.HasCheckConstraint(
                "CK_WalletTransactions_Balance",
                "[BalanceAfter] >= 0"));

        builder.HasKey(x => x.TransactionId);

        builder.Property(x => x.Amount).HasPrecision(18, 0);
        builder.Property(x => x.BalanceAfter).HasPrecision(18, 0);
        builder.Property(x => x.Type).HasMaxLength(30);
        builder.Property(x => x.Description).HasMaxLength(500);

        builder.HasOne(x => x.Customer)
            .WithMany(x => x.WalletTransactions)
            .HasForeignKey(x => x.CustomerId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne<Order>()
            .WithMany()
            .HasForeignKey(x => x.OrderId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne<OrderDetail>()
            .WithMany()
            .HasForeignKey(x => x.OrderDetailId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne<GamingSessions>()
            .WithMany()
            .HasForeignKey(x => x.SessionId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasIndex(x => new { x.CustomerId, x.CreatedAt });

        builder.HasIndex(x => x.OrderId)
            .IsUnique()
            .HasFilter(
                "[OrderId] IS NOT NULL AND [Type] = 'OrderDebit'");

        builder.HasIndex(x => x.OrderDetailId)
            .IsUnique()
            .HasFilter(
                "[OrderDetailId] IS NOT NULL AND [Type] = 'OrderItemRefund'");
    }
}