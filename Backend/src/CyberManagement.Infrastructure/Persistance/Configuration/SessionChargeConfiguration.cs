using CyberManagement.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CyberManagement.Infrastructure.Persistence.Configurations;

public class SessionChargeConfiguration : IEntityTypeConfiguration<SessionCharge>
{
    public void Configure(EntityTypeBuilder<SessionCharge> builder)
    {
        builder.ToTable("SessionCharges");
        builder.HasKey(x => x.ChargeId);

        builder.Property(x => x.Amount).HasPrecision(18, 0);

        builder.HasOne(x => x.GamingSession)
            .WithMany(x => x.Charges)
            .HasForeignKey(x => x.GamingSessionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
