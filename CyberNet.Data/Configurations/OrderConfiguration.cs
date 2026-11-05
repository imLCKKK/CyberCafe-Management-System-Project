using CyberNet.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CyberNet.Data.Configurations;

public class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> builder)
    {
        builder.ToTable("Orders", t =>
        {
            t.HasCheckConstraint("CK_Orders_Status",
                "[Status] IN (N'Pending', N'Confirmed', N'Paid', N'Cancelled')");
            t.HasCheckConstraint("CK_Orders_Total", "[TotalAmount] >= 0");
        });
        builder.HasKey(x => x.OrderId);

        builder.Property(x => x.OrderTime)
               .HasColumnType("datetime")
               .HasDefaultValueSql("GETDATE()");
        builder.Property(x => x.Status).IsRequired().HasMaxLength(50).HasDefaultValue("Pending");
        builder.Property(x => x.TotalAmount)
               .HasColumnType("decimal(12,2)")
               .HasDefaultValue(0m);

        builder.HasOne(x => x.Customer)
               .WithMany(c => c.Orders)
               .HasForeignKey(x => x.CustomerId)
               .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Employee)
               .WithMany(e => e.Orders)
               .HasForeignKey(x => x.EmployeeId)
               .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Session)
               .WithMany(s => s.Orders)
               .HasForeignKey(x => x.SessionId)
               .OnDelete(DeleteBehavior.Restrict);
    }
}
