using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CyberManagement.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTournamentWalletSettlement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ChargeTransactionId",
                table: "TournamentRegistrations",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "FeeCharged",
                table: "TournamentRegistrations",
                type: "decimal(18,0)",
                precision: 18,
                scale: 0,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PricePerHourSnapshot",
                table: "GamingSessions",
                type: "decimal(18,0)",
                precision: 18,
                scale: 0,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_TournamentRegistrations_ChargeTransactionId",
                table: "TournamentRegistrations",
                column: "ChargeTransactionId",
                unique: true,
                filter: "[ChargeTransactionId] IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_TournamentRegistrations_WalletTransactions_ChargeTransactionId",
                table: "TournamentRegistrations",
                column: "ChargeTransactionId",
                principalTable: "WalletTransactions",
                principalColumn: "TransactionId",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF EXISTS (SELECT 1 FROM [TournamentRegistrations] WHERE [FeeCharged] IS NOT NULL OR [ChargeTransactionId] IS NOT NULL)
                    OR EXISTS (SELECT 1 FROM [GamingSessions] WHERE [PricePerHourSnapshot] IS NOT NULL)
                    THROW 51000, 'Cannot remove tournament fee or session price snapshots containing data.', 1;
                """);
            migrationBuilder.DropForeignKey(
                name: "FK_TournamentRegistrations_WalletTransactions_ChargeTransactionId",
                table: "TournamentRegistrations");

            migrationBuilder.DropIndex(
                name: "IX_TournamentRegistrations_ChargeTransactionId",
                table: "TournamentRegistrations");

            migrationBuilder.DropColumn(
                name: "ChargeTransactionId",
                table: "TournamentRegistrations");

            migrationBuilder.DropColumn(
                name: "FeeCharged",
                table: "TournamentRegistrations");

            migrationBuilder.DropColumn(
                name: "PricePerHourSnapshot",
                table: "GamingSessions");
        }
    }
}
