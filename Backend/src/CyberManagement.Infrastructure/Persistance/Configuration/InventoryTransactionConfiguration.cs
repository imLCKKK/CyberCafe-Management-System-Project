using CyberManagement.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CyberManagement.Infrastructure.Persistence.Configurations;

public class InventoryTransactionConfiguration : IEntityTypeConfiguration<InventoryTransaction>
{
    public void Configure(EntityTypeBuilder<InventoryTransaction> builder)
    {
        builder.ToTable("InventoryTransactions", t =>
        {
            t.HasCheckConstraint("CK_InventoryTransactions_Values", "[Before] >= 0 AND [After] >= 0 AND CAST([Before] AS bigint) + [Quantity] = [After]");
            t.HasCheckConstraint("CK_InventoryTransactions_Reason", "LEN(LTRIM(RTRIM([Reason]))) BETWEEN 1 AND 200");
            t.HasCheckConstraint("CK_InventoryTransactions_Type", "(([Type] IN ('Receive','Return') AND [Quantity] > 0) OR ([Type] = 'Sale' AND [Quantity] < 0) OR ([Type] = 'Adjust' AND [Quantity] <> 0)) AND (([Type] IN ('Receive','Adjust') AND [OrderDetailId] IS NULL) OR ([Type] IN ('Sale','Return') AND [OrderDetailId] IS NOT NULL AND [OrderId] IS NOT NULL))");
        });
        builder.Property(x => x.Reason).HasMaxLength(200).IsRequired();
        builder.HasIndex(x => new { x.OrderDetailId, x.Type }).IsUnique().HasFilter("[OrderDetailId] IS NOT NULL");
        builder.HasOne(x => x.OrderDetail).WithMany().HasForeignKey(x => x.OrderDetailId).OnDelete(DeleteBehavior.NoAction);
        builder.HasKey(x => x.InvTransId);

        builder.Property(x => x.Type).HasMaxLength(50).IsRequired();
        builder.Property(x => x.ReferenceType).HasMaxLength(50).IsRequired();

        builder.HasOne(x => x.Product)
            .WithMany()
            .HasForeignKey(x => x.ProductId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Employee)
            .WithMany()
            .HasForeignKey(x => x.EmployeeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.StockReceipt)
            .WithMany()
            .HasForeignKey(x => x.ReceiptId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Order)
            .WithMany()
            .HasForeignKey(x => x.OrderId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
