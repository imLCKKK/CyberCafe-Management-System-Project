using CyberManagement.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CyberManagement.Infrastructure.Persistence.Configurations;

public sealed class OrderOperationConfiguration : IEntityTypeConfiguration<OrderOperation>
{
    public void Configure(EntityTypeBuilder<OrderOperation> builder)
    {
        builder.ToTable("OrderOperations", t => t.HasCheckConstraint("CK_OrderOperations_Actor",
            "[ActorType] IN ('Staff','Customer') AND [ActorId] > 0"));
        builder.HasKey(x => x.Id);
        builder.Property(x => x.ActorType).HasMaxLength(10).UseCollation("Latin1_General_100_BIN2");
        builder.Property(x => x.Route).HasMaxLength(100).UseCollation("Latin1_General_100_BIN2");
        builder.Property(x => x.Key).HasMaxLength(100).UseCollation("Latin1_General_100_BIN2");
        builder.Property(x => x.RequestHash).HasMaxLength(64).IsFixedLength();
        builder.HasIndex(x => new { x.ActorType, x.ActorId, x.Route, x.Key }).IsUnique();
        builder.HasOne(x => x.Order).WithMany().HasForeignKey(x => x.OrderId).OnDelete(DeleteBehavior.NoAction);
    }
}
