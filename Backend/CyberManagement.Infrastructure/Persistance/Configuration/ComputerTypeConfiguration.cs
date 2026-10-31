namespace CyberManagement.Infrastructure.Persistance.Configuration;

public class ComputerTypeConfiguration
    : IEntityTypeConfiguration<ComputerTypes>
{
    public void Configure(EntityTypeBuilder<ComputerTypes> builder)
    {
        builder.ToTable("ComputerTypes", t =>
            t.HasCheckConstraint(
                "CK_ComputerTypes_Price", "[PricePerHour] > 0"));

        builder.HasKey(x => x.ComputerTypeId);

        builder.Property(x => x.ComputerTypeName).HasMaxLength(100);
        builder.Property(x => x.CPU).HasMaxLength(150);
        builder.Property(x => x.RAM).HasMaxLength(150);
        builder.Property(x => x.GPU).HasMaxLength(150);
        builder.Property(x => x.Description).HasMaxLength(500);
        builder.Property(x => x.PricePerHour).HasPrecision(18, 0);
    }
}