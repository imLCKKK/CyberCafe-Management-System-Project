namespace CyberManagement.Infrastructure.Persistance.Configuration;

public class CustomerConfiguration : IEntityTypeConfiguration<Customers>
{
    public void Configure(EntityTypeBuilder<Customers> builder)
    {
        builder.ToTable("Customers", t =>
            t.HasCheckConstraint(
                "CK_Customers_Balance", "[WalletBalance] >= 0"));

        builder.HasKey(x => x.CustomerId);

        builder.Property(x => x.FullName).HasMaxLength(100);
        builder.Property(x => x.UserName).HasMaxLength(100);
        builder.Property(x => x.PasswordHash).HasMaxLength(255);
        builder.Property(x => x.Phone).HasMaxLength(20);
        builder.Property(x => x.Email).HasMaxLength(254);
        builder.Property(x => x.Status).HasMaxLength(30);

        builder.Property(x => x.WalletBalance)
            .HasPrecision(18, 0)
            .HasDefaultValue(0m);

        builder.Property(x => x.WalletVersion)
            .HasDefaultValue(0L)
            .IsConcurrencyToken();
    }
}