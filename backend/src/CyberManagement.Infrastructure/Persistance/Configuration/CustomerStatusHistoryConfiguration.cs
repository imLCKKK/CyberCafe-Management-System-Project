using CyberManagement.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CyberManagement.Infrastructure.Persistence.Configurations;

public class CustomerStatusHistoryConfiguration : IEntityTypeConfiguration<CustomerStatusHistory>
{
    public void Configure(EntityTypeBuilder<CustomerStatusHistory> builder)
    {
        builder.ToTable("CustomerStatusHistory");
        builder.HasKey(x => x.HistoryId);

        builder.Property(x => x.OldStatus).HasMaxLength(50);
        builder.Property(x => x.NewStatus).HasMaxLength(50).IsRequired();
        builder.Property(x => x.Reason).HasMaxLength(255);

        builder.HasOne(x => x.Customer)
            .WithMany(x => x.StatusHistory)
            .HasForeignKey(x => x.CustomerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.ChangedByEmployee)
            .WithMany()
            .HasForeignKey(x => x.ChangedBy)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
