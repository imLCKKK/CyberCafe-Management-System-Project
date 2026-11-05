using CyberNet.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CyberNet.Data.Configurations;

public class InvoiceConfiguration : IEntityTypeConfiguration<Invoice>
{
    public void Configure(EntityTypeBuilder<Invoice> builder)
    {
        builder.ToTable("Invoices", t =>
        {
            // Hóa đơn phải gắn với đơn gọi món hoặc phiên chơi (hoặc cả hai)
            t.HasCheckConstraint("CK_Invoices_Source", "[OrderId] IS NOT NULL OR [SessionId] IS NOT NULL");
            t.HasCheckConstraint("CK_Invoices_Total", "[TotalAmount] >= 0");
        });
        builder.HasKey(x => x.InvoiceId);

        builder.Property(x => x.TotalAmount).HasColumnType("decimal(12,2)");
        builder.Property(x => x.PaymentTime).HasColumnType("datetime");

        builder.HasOne(x => x.Order)
               .WithMany(o => o.Invoices)
               .HasForeignKey(x => x.OrderId)
               .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Session)
               .WithMany(s => s.Invoices)
               .HasForeignKey(x => x.SessionId)
               .OnDelete(DeleteBehavior.Restrict);
    }
}
