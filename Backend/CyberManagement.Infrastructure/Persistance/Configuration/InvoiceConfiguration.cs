namespace CyberManagement.Infrastructure.Persistance.Configuration;

public class InvoiceConfiguration : IEntityTypeConfiguration<Invoice>
{
    public void Configure(EntityTypeBuilder<Invoice> builder)
    {
        builder.ToTable("Invoices", t =>
            t.HasCheckConstraint(
                "CK_Invoices_Amounts",
                """
                [SubTotal] >= 0
                AND [Discount] >= 0
                AND [Discount] <= [SubTotal]
                AND [TotalAmount] = [SubTotal] - [Discount]
                """));

        builder.HasKey(x => x.InvoiceId);

        builder.Property(x => x.SubTotal).HasPrecision(18, 0);
        builder.Property(x => x.Discount).HasPrecision(18, 0);
        builder.Property(x => x.TotalAmount).HasPrecision(18, 0);
        builder.Property(x => x.Status).HasMaxLength(30);

        builder.HasOne(x => x.Customer)
            .WithMany()
            .HasForeignKey(x => x.CustomerId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne(x => x.Session)
            .WithMany(x => x.Invoices)
            .HasForeignKey(x => x.SessionId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne(x => x.Employee)
            .WithMany()
            .HasForeignKey(x => x.EmployeeId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne(x => x.Promotion)
            .WithMany()
            .HasForeignKey(x => x.PromotionId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasMany(x => x.Orders)
            .WithOne()
            .HasForeignKey(x => x.InvoiceId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasIndex(x => new { x.CustomerId, x.InvoiceDate });
    }
}