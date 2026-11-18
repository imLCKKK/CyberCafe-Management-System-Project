using CyberManagement.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CyberManagement.Infrastructure.Persistence.Configurations;

public class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.ToTable("AuditLogs");
        builder.HasKey(x => x.LogId);

        builder.Property(x => x.EntityType).HasMaxLength(50).IsRequired();
        builder.Property(x => x.Action).HasMaxLength(255).IsRequired();
        builder.Property(x => x.RequestKey).HasMaxLength(100).UseCollation("Latin1_General_100_BIN2");
        builder.Property(x => x.RequestFingerprint).HasMaxLength(64).IsUnicode(false);
        builder.Property(x => x.ResponseJson);
        builder.HasIndex(x => new { x.EmployeeId, x.RequestKey })
            .IsUnique()
            .HasFilter("[RequestKey] IS NOT NULL");

        builder.HasOne(x => x.Employee)
            .WithMany()
            .HasForeignKey(x => x.EmployeeId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
