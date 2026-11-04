using CyberNet.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CyberNet.Data.Configurations;

public class ShiftConfiguration : IEntityTypeConfiguration<Shift>
{
    public void Configure(EntityTypeBuilder<Shift> builder)
    {
        builder.ToTable("Shifts", t =>
            t.HasCheckConstraint("CK_Shifts_Time", "[EndTime] IS NULL OR [EndTime] >= [StartTime]"));
        builder.HasKey(x => x.ShiftId);

        builder.Property(x => x.StartTime).HasColumnType("datetime");
        builder.Property(x => x.EndTime).HasColumnType("datetime");

        builder.HasOne(x => x.Employee)
               .WithMany(e => e.Shifts)
               .HasForeignKey(x => x.EmployeeId)
               .OnDelete(DeleteBehavior.Restrict);
    }
}
