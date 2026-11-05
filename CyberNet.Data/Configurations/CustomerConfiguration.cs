using CyberNet.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CyberNet.Data.Configurations;

public class CustomerConfiguration : IEntityTypeConfiguration<Customer>
{
    public void Configure(EntityTypeBuilder<Customer> builder)
    {
        builder.ToTable("Customers", t =>
            t.HasCheckConstraint("CK_Customers_Balance", "[WalletBalance] >= 0"));
        builder.HasKey(x => x.CustomerId);

        builder.Property(x => x.FullName).IsRequired().HasMaxLength(150);
        builder.Property(x => x.Phone).HasMaxLength(20).IsUnicode(false);
        builder.Property(x => x.Email).HasMaxLength(150).IsUnicode(false);
        builder.Property(x => x.WalletBalance)
               .HasColumnType("decimal(12,2)")
               .HasDefaultValue(0m);

        // Số điện thoại không trùng nhau (bỏ qua giá trị NULL)
        builder.HasIndex(x => x.Phone)
               .IsUnique()
               .HasFilter("[Phone] IS NOT NULL")
               .HasDatabaseName("UX_Customers_Phone");
    }
}
