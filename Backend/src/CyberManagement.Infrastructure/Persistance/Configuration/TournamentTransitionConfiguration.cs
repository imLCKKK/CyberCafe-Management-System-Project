using CyberManagement.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CyberManagement.Infrastructure.Persistence.Configurations;

public class TournamentTransitionConfiguration : IEntityTypeConfiguration<TournamentTransition>
{
    public void Configure(EntityTypeBuilder<TournamentTransition> builder)
    {
        builder.ToTable("TournamentTransitions");
        builder.HasKey(x => x.TournamentTransitionId);
        builder.Property(x => x.RequestKey).HasMaxLength(100).UseCollation("Latin1_General_100_BIN2").IsRequired();
        builder.Property(x => x.ToStatus).HasMaxLength(50).IsRequired();
        builder.Property(x => x.ExpectedVersion).HasMaxLength(12).IsRequired();
        builder.Property(x => x.ResponseJson).IsRequired();
        builder.HasIndex(x => new { x.TournamentId, x.EmployeeId, x.RequestKey }).IsUnique();
        builder.HasOne(x => x.Tournament).WithMany().HasForeignKey(x => x.TournamentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Employee).WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Restrict);
    }
}
