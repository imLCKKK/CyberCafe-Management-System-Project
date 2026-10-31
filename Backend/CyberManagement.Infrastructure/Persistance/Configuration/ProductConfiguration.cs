namespace CyberManagement.Infrastructure.Persistance.Configuration;

public class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.ToTable("Products", t =>
        {
            t.HasCheckConstraint("CK_Products_Price", "[Price] >= 0");
            t.HasCheckConstraint("CK_Products_Stock", "[Stock] >= 0");
        });

        builder.HasKey(x => x.ProductId);

        builder.Property(x => x.ProductName)
            .IsRequired()
            .HasMaxLength(120);

        builder.Property(x => x.Price).HasPrecision(18, 0);

        builder.Property(x => x.Stock)
            .HasDefaultValue(0)
            .IsConcurrencyToken();

        builder.Property(x => x.IsActive).HasDefaultValue(true);
    }
}