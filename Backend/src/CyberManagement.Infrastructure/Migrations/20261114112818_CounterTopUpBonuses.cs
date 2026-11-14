using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CyberManagement.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CounterTopUpBonuses : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "BonusAmount",
                table: "CounterTopUpRequests",
                type: "decimal(18,0)",
                precision: 18,
                scale: 0,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "TotalAmount",
                table: "CounterTopUpRequests",
                type: "decimal(18,0)",
                precision: 18,
                scale: 0,
                nullable: false,
                defaultValue: 0m);

            // Pending requests adopt the agreed offer; settled receipts retain their original credit.
            migrationBuilder.Sql("""
                UPDATE [CounterTopUpRequests]
                SET [BonusAmount] = CASE WHEN [Status] = N'Pending' THEN
                    CASE [Amount] WHEN 50000 THEN 5000 WHEN 100000 THEN 15000
                        WHEN 200000 THEN 35000 WHEN 300000 THEN 50000 WHEN 500000 THEN 100000 ELSE 0 END
                    ELSE 0 END;
                UPDATE [CounterTopUpRequests] SET [TotalAmount] = [Amount] + [BonusAmount];
                """);

            migrationBuilder.AddCheckConstraint(
                name: "CK_CounterTopUpRequests_Bonus",
                table: "CounterTopUpRequests",
                sql: "[BonusAmount] >= 0 AND [TotalAmount] = [Amount] + [BonusAmount]");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("IF EXISTS (SELECT 1 FROM [CounterTopUpRequests] WHERE [BonusAmount] <> 0) THROW 51022, 'Cannot discard recorded counter top-up bonus snapshots.', 1;");
            migrationBuilder.DropCheckConstraint(
                name: "CK_CounterTopUpRequests_Bonus",
                table: "CounterTopUpRequests");

            migrationBuilder.DropColumn(
                name: "BonusAmount",
                table: "CounterTopUpRequests");

            migrationBuilder.DropColumn(
                name: "TotalAmount",
                table: "CounterTopUpRequests");
        }
    }
}
