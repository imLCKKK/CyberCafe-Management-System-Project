using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System;
using System.Collections.Generic;
using System.Text;
using CyberManagement.Domain.Entities;

namespace CyberManagement.Infrastructure.Persistance.Configuration
{
    public class InvoiceConfiguration : IEntityTypeConfiguration<Invoice>
    {
        public void Configure(EntityTypeBuilder<Invoice> builder)
        {
            builder.ToTable("Invoices");
            builder.HasKey(i => i.InvoiceId); //primary key

            builder.Property(i => i.TotalAmount) 
                .IsRequired()
                .HasPrecision(12,2);

            builder.Property(i => i.PaymentTime) // cho phép PaymentTime có thể null khi không có thông tin thanh toán
                .IsRequired(false);

            builder.HasOne(o => o.Order)
                .WithMany(i => i.Invoices)
                .HasForeignKey(o => o.OrderId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.SetNull); // khi xóa Order thì Invoice sẽ được giữ lại và OrderId sẽ được đặt thành null

            builder.HasMany(p => p.Payments)
                .WithOne(i => i.Invoice)
                .HasForeignKey(p => p.InvoiceId)
                .IsRequired()
                .OnDelete(DeleteBehavior.Cascade); // khi xóa Invoice thì các Payment liên quan sẽ bị xóa theo
        }

    }
}
