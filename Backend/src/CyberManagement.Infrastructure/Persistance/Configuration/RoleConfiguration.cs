using CyberManagement.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CyberManagement.Infrastructure.Persistence.Configurations;

public class RoleConfiguration : IEntityTypeConfiguration<Role>
{
    public void Configure(EntityTypeBuilder<Role> builder)
    {
        builder.ToTable("Roles");
        builder.HasKey(x => x.RoleId);

        builder.Property(x => x.RoleName).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Description).HasMaxLength(255);

        builder.HasIndex(x => x.RoleName).IsUnique();
    }
}
