namespace CyberManagement.Infrastructure.Persistance.Configuration;

public class InventoryTransactionConfiguration
    : IEntityTypeConfiguration<InventoryTransaction>
{
    public void Configure(
        EntityTypeBuilder<InventoryTransaction> builder)
    {
        builder.ToTable("InventoryTransactions", t =>
        {
            t.HasCheckConstraint(
                "CK_Inventory_Stock",
                "[Before] >= 0 AND [After] >= 0");

            t.HasCheckConstraint(
                "CK_Inventory_Delta",
                """
                CAST([After] AS bigint) =
                CAST([Before] AS bigint) + CAST([Quantity] AS bigint)
                """);

            t.HasCheckConstraint(
                "CK_Inventory_Type",
                """
                ([Type] = 'Receive' AND [Quantity] > 0
                    AND [OrderDetailId] IS NULL)
                OR
                ([Type] = 'Adjust' AND [Quantity] <> 0
                    AND [OrderDetailId] IS NULL)
                OR
                ([Type] = 'Sale' AND [Quantity] < 0
                    AND [OrderDetailId] IS NOT NULL)
                OR
                ([Type] = 'Return' AND [Quantity] > 0
                    AND [OrderDetailId] IS NOT NULL)
                """);

            t.HasCheckConstraint(
                "CK_Inventory_Reason",
                "LEN(LTRIM(RTRIM([Reason]))) > 0");
        });

        builder.HasKey(x => x.InvTransId);

        builder.Property(x => x.Type)
            .IsRequired()
            .HasMaxLength(30);

        builder.Property(x => x.Reason)
            .IsRequired()
            .HasMaxLength(200);

        builder.HasOne(x => x.Product)
            .WithMany()
            .HasForeignKey(x => x.ProductId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne<Employees>()
            .WithMany()
            .HasForeignKey(x => x.EmployeeId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne<OrderDetail>()
            .WithMany()
            .HasForeignKey(x => x.OrderDetailId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasIndex(x => new { x.ProductId, x.CreatedAt });

        builder.HasIndex(x => new { x.OrderDetailId, x.Type })
            .IsUnique()
            .HasFilter("[OrderDetailId] IS NOT NULL");
    }
}