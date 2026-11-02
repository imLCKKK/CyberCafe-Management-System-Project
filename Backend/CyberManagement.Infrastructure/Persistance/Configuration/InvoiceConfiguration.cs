namespace CyberManagement.Infrastructure.Persistance.Configuration;

public class InvoiceConfiguration : IEntityTypeConfiguration<Invoice>
{
    public void Configure(EntityTypeBuilder<Invoice> builder)
    {
        builder.ToTable("Invoices", table =>
        {
            // Phải có đúng một nguồn: đơn món ăn hoặc phiên chơi.
            table.HasCheckConstraint(
                "CK_Invoices_Source",
                """
                ([OrderId] IS NOT NULL AND [SessionId] IS NULL)
                OR
                ([OrderId] IS NULL AND [SessionId] IS NOT NULL)
                """);

            table.HasCheckConstraint(
                "CK_Invoices_Amounts",
                """
                [SubTotal] >= 0
                AND [Discount] >= 0
                AND [Discount] <= [SubTotal]
                AND [GrossAmount] = [SubTotal] - [Discount]
                AND [RefundAmount] >= 0
                AND [RefundAmount] <= [GrossAmount]
                AND [PaidAmount] >= 0
                AND [PaidAmount] <= [GrossAmount] - [RefundAmount]
                """);

            table.HasCheckConstraint(
                "CK_Invoices_Status",
                "[Status] IN ('Pending', 'Paid', 'Cancelled')");
        });

        builder.HasKey(x => x.InvoiceId);

        builder.Property(x => x.GrossAmount)
            .HasPrecision(18, 0);

        builder.Property(x => x.RefundAmount)
            .HasPrecision(18, 0);

        builder.Property(x => x.PaidAmount)
            .HasPrecision(18, 0);

        builder.Property(x => x.IssuedAt)
            .HasColumnType("datetimeoffset");

        builder.Property(x => x.Status)
            .IsRequired()
            .HasMaxLength(30);

        // Tạm giữ cấu hình tiền cũ đến khi chuyển xong service.
        builder.Property(x => x.SubTotal).HasPrecision(18, 0);
        builder.Property(x => x.Discount).HasPrecision(18, 0);

        builder.HasOne(x => x.Order)
            .WithOne()
            .HasForeignKey<Invoice>(x => x.OrderId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasIndex(x => x.OrderId)
            .IsUnique()
            .HasFilter("[OrderId] IS NOT NULL");

        builder.HasOne(x => x.Session)
            .WithMany(x => x.Invoices)
            .HasForeignKey(x => x.SessionId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasIndex(x => x.SessionId)
            .IsUnique()
            .HasFilter("[SessionId] IS NOT NULL");

        builder.HasOne(x => x.Customer)
            .WithMany()
            .HasForeignKey(x => x.CustomerId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne(x => x.Employee)
            .WithMany()
            .HasForeignKey(x => x.EmployeeId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne(x => x.Promotion)
            .WithMany()
            .HasForeignKey(x => x.PromotionId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasIndex(x => new { x.CustomerId, x.IssuedAt });
    }
}