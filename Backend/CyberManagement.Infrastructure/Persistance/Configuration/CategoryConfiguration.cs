namespace CyberManagement.Infrastructure.Persistance.Configuration;

public class CategoryConfiguration : IEntityTypeConfiguration<Category>
{
    public void Configure(EntityTypeBuilder<Category> builder)
    {
        builder.ToTable("Categories");
        builder.HasKey(x => x.CategoryId);

        builder.Property(x => x.CategoryName)
            .IsRequired()
            .HasMaxLength(150);

        builder.Property(x => x.IsActive).HasDefaultValue(true);

        builder.HasMany(x => x.Products)
            .WithOne(x => x.Category)
            .HasForeignKey(x => x.CategoryId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}