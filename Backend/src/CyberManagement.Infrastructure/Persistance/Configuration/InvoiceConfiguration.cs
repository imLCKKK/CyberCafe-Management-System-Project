using CyberManagement.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CyberManagement.Infrastructure.Persistence.Configurations;

public class InvoiceConfiguration : IEntityTypeConfiguration<Invoice>
{
    public void Configure(EntityTypeBuilder<Invoice> builder)
    {
        builder.ToTable("Invoices", t =>
        {
            t.HasCheckConstraint("CK_Invoices_Amounts", "[Subtotal] >= 0 AND [Discount] >= 0 AND [Discount] <= [Subtotal] AND [TotalAmount] = [Subtotal] - [Discount]");
            t.HasCheckConstraint("CK_Invoices_Status", "[Status] IN ('Unpaid','Paid','Cancelled')");
        });
        builder.HasKey(x => x.InvoiceId);

        builder.Property(x => x.Subtotal).HasPrecision(18, 0);
        builder.Property(x => x.Discount).HasPrecision(18, 0);
        builder.Property(x => x.TotalAmount).HasPrecision(18, 0);
        builder.Property(x => x.Status).HasMaxLength(50).IsRequired();

        builder.HasOne(x => x.Customer)
            .WithMany(x => x.Invoices)
            .HasForeignKey(x => x.CustomerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.GamingSession)
            .WithMany()
            .HasForeignKey(x => x.GamingSessionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Employee)
            .WithMany()
            .HasForeignKey(x => x.EmployeeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Promotion)
            .WithMany(x => x.Invoices)
            .HasForeignKey(x => x.PromotionId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
