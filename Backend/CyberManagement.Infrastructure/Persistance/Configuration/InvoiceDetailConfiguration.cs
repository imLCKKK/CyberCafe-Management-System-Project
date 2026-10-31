namespace CyberManagement.Infrastructure.Persistance.Configuration;

public class InvoiceDetailConfiguration : IEntityTypeConfiguration<InvoiceDetail>
{
    public void Configure(EntityTypeBuilder<InvoiceDetail> builder)
    {
        builder.ToTable("InvoiceDetails", t =>
            t.HasCheckConstraint(
                "CK_InvoiceDetails_Amounts",
                """
                [Quantity] > 0 AND [UnitPrice] >= 0
                AND [Amount] = [Quantity] * [UnitPrice]
                """));

        builder.HasKey(x => x.InvoiceDetailId);

        builder.Property(x => x.ItemType).HasMaxLength(30);
        builder.Property(x => x.Description).HasMaxLength(500);
        builder.Property(x => x.UnitPrice).HasPrecision(18, 0);
        builder.Property(x => x.Amount).HasPrecision(18, 0);

        builder.HasOne(x => x.Invoice)
            .WithMany(x => x.InvoiceDetails)
            .HasForeignKey(x => x.InvoiceId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}