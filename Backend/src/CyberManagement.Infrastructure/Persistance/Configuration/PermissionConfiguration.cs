using CyberManagement.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CyberManagement.Infrastructure.Persistence.Configurations;

public class PermissionConfiguration : IEntityTypeConfiguration<Permission>
{
    public void Configure(EntityTypeBuilder<Permission> builder)
    {
        builder.ToTable("Permissions");
        builder.HasKey(x => x.PermissionId);

        builder.Property(x => x.PermissionName).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Description).HasMaxLength(255);

        builder.HasIndex(x => x.PermissionName).IsUnique();
    }
}
