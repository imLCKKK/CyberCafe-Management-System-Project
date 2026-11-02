using CyberManagement.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CyberManagement.Infrastructure.Persistence.Configurations;

public class InvoiceDetailConfiguration : IEntityTypeConfiguration<InvoiceDetail>
{
    public void Configure(EntityTypeBuilder<InvoiceDetail> builder)
    {
        builder.ToTable("InvoiceDetails");
        builder.HasKey(x => x.InvoiceDetailId);

        builder.Property(x => x.ItemType).HasMaxLength(50).IsRequired();
        builder.Property(x => x.Description).HasMaxLength(255).IsRequired();
        builder.Property(x => x.UnitPrice).HasPrecision(12, 2);
        builder.Property(x => x.Amount).HasPrecision(12, 2);

        builder.HasOne(x => x.Invoice)
            .WithMany(x => x.Details)
            .HasForeignKey(x => x.InvoiceId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
