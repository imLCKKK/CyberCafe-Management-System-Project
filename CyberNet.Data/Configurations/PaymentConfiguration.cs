using CyberNet.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CyberNet.Data.Configurations;

public class PaymentConfiguration : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> builder)
    {
        builder.ToTable("Payments", t =>
        {
            t.HasCheckConstraint("CK_Payments_Amount", "[Amount] > 0");
            t.HasCheckConstraint("CK_Payments_Method",
                "[Method] IS NULL OR [Method] IN (N'Cash', N'Wallet', N'Transfer')");
        });
        builder.HasKey(x => x.PaymentId);

        builder.Property(x => x.Amount).HasColumnType("decimal(12,2)");
        builder.Property(x => x.Method).HasMaxLength(50);
        builder.Property(x => x.PaymentTime)
               .HasColumnType("datetime")
               .HasDefaultValueSql("GETDATE()");

        builder.HasOne(x => x.Invoice)
               .WithMany(i => i.Payments)
               .HasForeignKey(x => x.InvoiceId)
               .OnDelete(DeleteBehavior.Restrict);
    }
}
