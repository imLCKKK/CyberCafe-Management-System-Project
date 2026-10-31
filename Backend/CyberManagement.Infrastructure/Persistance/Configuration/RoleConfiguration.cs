namespace CyberManagement.Infrastructure.Persistance.Configuration;

public class RoleConfiguration : IEntityTypeConfiguration<Role>
{
    public void Configure(EntityTypeBuilder<Role> builder)
    {
        builder.ToTable("Roles");
        builder.HasKey(x => x.RoleId);

        builder.Property(x => x.RoleName).HasMaxLength(30);
        builder.Property(x => x.Description).HasMaxLength(500);
    }
}