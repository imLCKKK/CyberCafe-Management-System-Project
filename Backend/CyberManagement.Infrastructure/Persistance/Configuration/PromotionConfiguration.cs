namespace CyberManagement.Infrastructure.Persistance.Configuration;

public class PromotionConfiguration : IEntityTypeConfiguration<Promotions>
{
    public void Configure(EntityTypeBuilder<Promotions> builder)
    {
        builder.ToTable("Promotions", table =>
        {
            table.HasCheckConstraint(
                "CK_Promotions_Code",
                "LEN(LTRIM(RTRIM([Code]))) > 0");

            table.HasCheckConstraint(
                "CK_Promotions_Discount",
                """
                ([DiscountType] = 'Percent'
                    AND [DiscountValue] > 0
                    AND [DiscountValue] <= 100)
                OR
                ([DiscountType] = 'Fixed'
                    AND [DiscountValue] > 0
                    AND [DiscountValue] = ROUND([DiscountValue], 0))
                """);

            table.HasCheckConstraint(
                "CK_Promotions_Amounts",
                """
                [MinOrderAmount] >= 0
                AND (
                    [MaxDiscountAmount] IS NULL
                    OR [MaxDiscountAmount] > 0
                )
                """);

            table.HasCheckConstraint(
                "CK_Promotions_Dates",
                "[StartDate] < [EndDate]");

            table.HasCheckConstraint(
                "CK_Promotions_UsageLimits",
                """
                ([TotalUsageLimit] IS NULL OR [TotalUsageLimit] > 0)
                AND
                ([PerCustomerUsageLimit] IS NULL
                    OR [PerCustomerUsageLimit] > 0)
                """);
        });

        builder.HasKey(x => x.PromotionId);

        builder.Property(x => x.Code)
            .IsRequired()
            .HasMaxLength(50)
            .UseCollation("Latin1_General_100_CI_AS");

        builder.HasIndex(x => x.Code)
            .IsUnique();

        builder.Property(x => x.PromotionName)
            .IsRequired()
            .HasMaxLength(150);

        builder.Property(x => x.DiscountType)
            .IsRequired()
            .HasMaxLength(10);

        builder.Property(x => x.DiscountValue)
            .HasPrecision(18, 2);

        builder.Property(x => x.MinOrderAmount)
            .HasPrecision(18, 0);

        builder.Property(x => x.MaxDiscountAmount)
            .HasPrecision(18, 0);

        builder.Property(x => x.StartDate)
            .HasColumnType("datetimeoffset");

        builder.Property(x => x.EndDate)
            .HasColumnType("datetimeoffset");
    }
}