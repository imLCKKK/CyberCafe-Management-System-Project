namespace CyberManagement.Infrastructure.Persistance.Configuration;

public class EmployeeConfiguration : IEntityTypeConfiguration<Employees>
{
    public void Configure(EntityTypeBuilder<Employees> builder)
    {
        builder.ToTable("Employees");
        builder.HasKey(x => x.EmployeeId);

        builder.Property(x => x.FullName).HasMaxLength(100);
        builder.Property(x => x.Username).HasMaxLength(100);
        builder.Property(x => x.PasswordHash).HasMaxLength(255);
        builder.Property(x => x.Phone).HasMaxLength(20);

        builder.HasOne(x => x.Role)
            .WithMany(x => x.Employees)
            .HasForeignKey(x => x.RoleId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}