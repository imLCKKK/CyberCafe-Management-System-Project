namespace CyberManagement.Infrastructure.Persistance.Configuration;

public class ComputerRoomConfiguration
    : IEntityTypeConfiguration<ComputerRooms>
{
    public void Configure(EntityTypeBuilder<ComputerRooms> builder)
    {
        builder.ToTable("ComputerRooms");
        builder.HasKey(x => x.RoomId);

        builder.Property(x => x.RoomName).HasMaxLength(100);
        builder.Property(x => x.Description).HasMaxLength(500);
    }
}