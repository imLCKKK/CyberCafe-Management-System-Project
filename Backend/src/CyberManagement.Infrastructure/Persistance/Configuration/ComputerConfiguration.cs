using CyberManagement.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CyberManagement.Infrastructure.Persistence.Configurations;

public class ComputerConfiguration : IEntityTypeConfiguration<Computer>
{
    public void Configure(EntityTypeBuilder<Computer> builder)
    {
        builder.ToTable("Computers");
        builder.HasKey(x => x.ComputerId);

        builder.Property(x => x.ComputerName).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Status).HasMaxLength(50).IsRequired();

        builder.HasIndex(x => new { x.RoomId, x.ComputerName }).IsUnique();

        builder.HasOne(x => x.Room)
            .WithMany(x => x.Computers)
            .HasForeignKey(x => x.RoomId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.ComputerType)
            .WithMany(x => x.Computers)
            .HasForeignKey(x => x.ComputerTypeId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
