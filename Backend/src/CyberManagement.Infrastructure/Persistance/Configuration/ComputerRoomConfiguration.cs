using CyberManagement.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CyberManagement.Infrastructure.Persistence.Configurations;

public class ComputerRoomConfiguration : IEntityTypeConfiguration<ComputerRoom>
{
    public void Configure(EntityTypeBuilder<ComputerRoom> builder)
    {
        builder.ToTable("ComputerRooms");
        builder.HasKey(x => x.RoomId);

        builder.Property(x => x.RoomName).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Description).HasMaxLength(255);
    }
}
