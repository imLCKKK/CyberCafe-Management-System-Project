using CyberManagement.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CyberManagement.Infrastructure.Persistence.Configurations;

public class EmployeeConfiguration : IEntityTypeConfiguration<Employee>
{
    public void Configure(EntityTypeBuilder<Employee> builder)
    {
        builder.ToTable("Employees");
        builder.HasKey(x => x.EmployeeId);

        builder.Property(x => x.FullName).HasMaxLength(150).IsRequired();
        builder.Property(x => x.Phone).HasMaxLength(20);
        builder.Property(x => x.Username).HasMaxLength(50).IsRequired();
        builder.Property(x => x.PasswordHash).HasMaxLength(255).IsRequired();
        builder.Property(x => x.Version).IsRowVersion();
        builder.Property(x => x.CreateRequestKey).HasMaxLength(100).UseCollation("Latin1_General_100_BIN2");
        builder.Property(x => x.CreateRequestFingerprint).HasMaxLength(64).IsUnicode(false);

        builder.HasIndex(x => x.Username).IsUnique();
        builder.HasIndex(x => x.CreateRequestKey).IsUnique()
            .HasFilter("[CreateRequestKey] IS NOT NULL");

        builder.HasOne(x => x.Role)
            .WithMany(x => x.Employees)
            .HasForeignKey(x => x.RoleId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
