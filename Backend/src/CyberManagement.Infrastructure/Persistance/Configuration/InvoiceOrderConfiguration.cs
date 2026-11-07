using CyberManagement.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CyberManagement.Infrastructure.Persistence.Configurations;

public class InvoiceOrderConfiguration : IEntityTypeConfiguration<InvoiceOrder>
{
    public void Configure(EntityTypeBuilder<InvoiceOrder> builder)
    {
        builder.ToTable("InvoiceOrders");
        builder.HasKey(x => new { x.InvoiceId, x.OrderId });
        builder.HasIndex(x => x.OrderId).IsUnique();

        builder.HasOne(x => x.Invoice)
            .WithMany(x => x.InvoiceOrders)
            .HasForeignKey(x => x.InvoiceId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Order)
            .WithMany()
            .HasForeignKey(x => x.OrderId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
