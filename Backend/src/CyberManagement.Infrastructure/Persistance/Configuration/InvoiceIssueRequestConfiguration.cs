using CyberManagement.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CyberManagement.Infrastructure.Persistence.Configurations;

public class InvoiceIssueRequestConfiguration : IEntityTypeConfiguration<InvoiceIssueRequest>
{
    public void Configure(EntityTypeBuilder<InvoiceIssueRequest> builder)
    {
        builder.ToTable("InvoiceIssueRequests", t =>
        {
            t.HasCheckConstraint("CK_InvoiceIssueRequests_Key", "LEN([OperationKey]) BETWEEN 1 AND 100 AND [OperationKey] NOT LIKE '%[^-A-Za-z0-9:_]%' COLLATE Latin1_General_100_BIN2");
            t.HasCheckConstraint("CK_InvoiceIssueRequests_Hash", "LEN([RequestHash]) = 64 AND [RequestHash] NOT LIKE '%[^0-9a-f]%' COLLATE Latin1_General_100_BIN2");
        });
        builder.HasKey(x => x.OperationKey);
        builder.Property(x => x.OperationKey).HasMaxLength(100).UseCollation("Latin1_General_100_BIN2");
        // SQL và ChangeTracker cùng so sánh khóa phát hành theo từng ký tự.
        builder.Property(x => x.OperationKey).Metadata.SetValueComparer(new ValueComparer<string>(
            (left, right) => string.Equals(left, right, StringComparison.Ordinal),
            value => value.GetHashCode(), value => value));
        builder.Property(x => x.RequestHash).HasColumnType("char(64)").UseCollation("Latin1_General_100_BIN2").IsRequired();
        builder.HasIndex(x => x.InvoiceId).IsUnique();
        builder.HasOne(x => x.Invoice).WithMany().HasForeignKey(x => x.InvoiceId).OnDelete(DeleteBehavior.NoAction);
    }
}
