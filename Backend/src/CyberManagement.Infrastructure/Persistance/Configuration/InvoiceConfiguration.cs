using CyberManagement.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CyberManagement.Infrastructure.Persistence.Configurations;

public class InvoiceConfiguration : IEntityTypeConfiguration<Invoice>
{
    public void Configure(EntityTypeBuilder<Invoice> builder)
    {
        builder.ToTable("Invoices");
        builder.HasKey(x => x.InvoiceId);

        builder.Property(x => x.Subtotal).HasPrecision(12, 2);
        builder.Property(x => x.Discount).HasPrecision(12, 2);
        builder.Property(x => x.TotalAmount).HasPrecision(12, 2);
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
