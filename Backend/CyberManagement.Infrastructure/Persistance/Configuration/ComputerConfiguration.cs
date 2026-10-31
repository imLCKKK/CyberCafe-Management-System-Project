namespace CyberManagement.Infrastructure.Persistance.Configuration;

public class ComputerConfiguration : IEntityTypeConfiguration<Computers>
{
    public void Configure(EntityTypeBuilder<Computers> builder)
    {
        builder.ToTable("Computers");
        builder.HasKey(x => x.ComputerId);
        builder.Property(x => x.ComputerName).HasMaxLength(100);

        builder.HasOne(x => x.Room)
            .WithMany(x => x.Computers)
            .HasForeignKey(x => x.RoomId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne(x => x.ComputerType)
            .WithMany(x => x.Computers)
            .HasForeignKey(x => x.ComputerTypeId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}