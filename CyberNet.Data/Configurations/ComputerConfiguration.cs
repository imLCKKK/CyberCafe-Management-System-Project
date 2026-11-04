using CyberNet.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CyberNet.Data.Configurations;

public class ComputerConfiguration : IEntityTypeConfiguration<Computer>
{
    public void Configure(EntityTypeBuilder<Computer> builder)
    {
        builder.ToTable("Computers", t =>
        {
            t.HasCheckConstraint("CK_Computers_Status",
                "[Status] IN (N'Trống', N'Đang chơi', N'Offline', N'Bảo trì')");
            t.HasCheckConstraint("CK_Computers_Price", "[PricePerHour] >= 0");
        });
        builder.HasKey(x => x.ComputerId);

        builder.Property(x => x.ComputerName).IsRequired().HasMaxLength(100);
        builder.Property(x => x.Status).IsRequired().HasMaxLength(50).HasDefaultValue("Trống");
        builder.Property(x => x.PricePerHour).HasColumnType("decimal(12,2)");

        builder.HasIndex(x => x.ComputerName).IsUnique();
    }
}
