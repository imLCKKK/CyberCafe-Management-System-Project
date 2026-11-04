using CyberNet.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CyberNet.Data.Configurations;

public class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.ToTable("AuditLogs");
        builder.HasKey(x => x.LogId);

        builder.Property(x => x.Action).IsRequired().HasMaxLength(255);
        builder.Property(x => x.Timestamp)
               .HasColumnType("datetime")
               .HasDefaultValueSql("GETDATE()");

        builder.HasOne(x => x.Employee)
               .WithMany(e => e.AuditLogs)
               .HasForeignKey(x => x.EmployeeId)
               .OnDelete(DeleteBehavior.Restrict);
    }
}
