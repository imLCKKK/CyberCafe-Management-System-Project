using CyberManagement.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CyberManagement.Infrastructure.Persistence.Configurations;

public class CustomerConfiguration : IEntityTypeConfiguration<Customer>
{
    public void Configure(EntityTypeBuilder<Customer> builder)
    {
        builder.ToTable("Customers");
        builder.HasKey(x => x.CustomerId);

        builder.Property(x => x.FullName).HasMaxLength(150).IsRequired();
        builder.Property(x => x.Phone).HasMaxLength(20).IsRequired();
        builder.Property(x => x.PasswordHash).HasMaxLength(255);
        builder.Property(x => x.Email).HasMaxLength(150);
        builder.Property(x => x.WalletBalance).HasPrecision(12, 2);
        builder.Property(x => x.Status).HasMaxLength(50).IsRequired();

        builder.HasIndex(x => x.Phone).IsUnique();
    }
}
