using CyberManagement.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CyberManagement.Infrastructure.Persistence.Configurations;

public class PaymentConfiguration : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> builder)
    {
        builder.ToTable("Payments");
        builder.HasKey(x => x.PaymentId);

        builder.Property(x => x.Amount).HasPrecision(12, 2);
        builder.Property(x => x.Method).HasMaxLength(50).IsRequired();
        builder.Property(x => x.Status).HasMaxLength(50).IsRequired();

        builder.HasOne(x => x.Invoice)
            .WithMany(x => x.Payments)
            .HasForeignKey(x => x.InvoiceId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.WalletTransaction)
            .WithMany()
            .HasForeignKey(x => x.WalletTransactionId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
