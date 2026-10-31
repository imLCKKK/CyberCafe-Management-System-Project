namespace CyberManagement.Infrastructure.Persistance.Configuration;

public class PaymentConfiguration : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> builder)
    {
        builder.ToTable("Payments", t =>
            t.HasCheckConstraint(
                "CK_Payments_Amount", "[Amount] >= 0"));

        builder.HasKey(x => x.PaymentId);

        builder.Property(x => x.Amount).HasPrecision(18, 0);
        builder.Property(x => x.Method).HasMaxLength(30);
        builder.Property(x => x.Status).HasMaxLength(30);

        builder.HasOne(x => x.Invoice)
            .WithMany(x => x.Payments)
            .HasForeignKey(x => x.InvoiceId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne(x => x.WalletTransaction)
            .WithMany(x => x.Payments)
            .HasForeignKey(x => x.WalletTransactionId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}