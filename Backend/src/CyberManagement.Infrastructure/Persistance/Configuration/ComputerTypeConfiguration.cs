using CyberManagement.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CyberManagement.Infrastructure.Persistence.Configurations;

public class ComputerTypeConfiguration : IEntityTypeConfiguration<ComputerType>
{
    public void Configure(EntityTypeBuilder<ComputerType> builder)
    {
        builder.ToTable("ComputerTypes");
        builder.HasKey(x => x.ComputerTypeId);

        builder.Property(x => x.TypeName).HasMaxLength(100).IsRequired();
        builder.Property(x => x.CPU).HasMaxLength(100);
        builder.Property(x => x.RAM).HasMaxLength(50);
        builder.Property(x => x.GPU).HasMaxLength(100);
        builder.Property(x => x.PricePerHour).HasPrecision(18, 0);
        builder.Property(x => x.Description).HasMaxLength(255);
    }
}
