using CyberManagement.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CyberManagement.Infrastructure.Persistence.Configurations;

public class InvoiceDetailConfiguration : IEntityTypeConfiguration<InvoiceDetail>
{
    public void Configure(EntityTypeBuilder<InvoiceDetail> builder)
    {
        builder.ToTable("InvoiceDetails", t =>
        {
            t.HasCheckConstraint("CK_InvoiceDetails_Source", "([OrderDetailId] IS NOT NULL AND [SessionChargeId] IS NULL) OR ([OrderDetailId] IS NULL AND [SessionChargeId] IS NOT NULL)");
            t.HasCheckConstraint("CK_InvoiceDetails_Values", "[Quantity] > 0 AND [UnitPrice] >= 0 AND [Amount] >= 0 AND [Amount] <= [UnitPrice] * [Quantity]");
        });
        builder.HasOne(x => x.OrderDetail).WithMany().HasForeignKey(x => x.OrderDetailId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne(x => x.SessionCharge).WithMany().HasForeignKey(x => x.SessionChargeId).OnDelete(DeleteBehavior.NoAction);
        builder.HasIndex(x => x.OrderDetailId).IsUnique().HasFilter("[OrderDetailId] IS NOT NULL");
        builder.HasIndex(x => x.SessionChargeId).IsUnique().HasFilter("[SessionChargeId] IS NOT NULL");
        builder.HasKey(x => x.InvoiceDetailId);

        builder.Property(x => x.ItemType).HasMaxLength(50).IsRequired();
        builder.Property(x => x.Description).HasMaxLength(255).IsRequired();
        builder.Property(x => x.UnitPrice).HasPrecision(18, 0);
        builder.Property(x => x.Amount).HasPrecision(18, 0);

        builder.HasOne(x => x.Invoice)
            .WithMany(x => x.Details)
            .HasForeignKey(x => x.InvoiceId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}
