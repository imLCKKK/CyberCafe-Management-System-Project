using CyberNet.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CyberNet.Data.Configurations;

public class EmployeeConfiguration : IEntityTypeConfiguration<Employee>
{
    public void Configure(EntityTypeBuilder<Employee> builder)
    {
        builder.ToTable("Employees");
        builder.HasKey(x => x.EmployeeId);

        builder.Property(x => x.FullName).IsRequired().HasMaxLength(150);
        builder.Property(x => x.Username).IsRequired().HasMaxLength(100).IsUnicode(false);
        builder.Property(x => x.PasswordHash).IsRequired().HasMaxLength(255).IsUnicode(false);
        builder.Property(x => x.Phone).HasMaxLength(20).IsUnicode(false);

        builder.HasIndex(x => x.Username).IsUnique();

        builder.HasOne(x => x.Role)
               .WithMany(r => r.Employees)
               .HasForeignKey(x => x.RoleId)
               .OnDelete(DeleteBehavior.Restrict);
    }
}
