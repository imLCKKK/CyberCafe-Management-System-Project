using CyberManagement.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CyberManagement.Infrastructure.Persistence.Configurations;

public class ComputerMaintenanceConfiguration : IEntityTypeConfiguration<ComputerMaintenance>
{
    public void Configure(EntityTypeBuilder<ComputerMaintenance> builder)
    {
        builder.ToTable("ComputerMaintenance");
        builder.HasKey(x => x.MaintenanceId);

        builder.Property(x => x.Description).HasMaxLength(255);
        builder.Property(x => x.Status).HasMaxLength(50).IsRequired();

        builder.HasOne(x => x.Computer)
            .WithMany(x => x.Maintenances)
            .HasForeignKey(x => x.ComputerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Employee)
            .WithMany()
            .HasForeignKey(x => x.EmployeeId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
