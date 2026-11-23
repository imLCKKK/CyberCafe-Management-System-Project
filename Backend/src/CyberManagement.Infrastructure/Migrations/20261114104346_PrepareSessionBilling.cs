using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CyberManagement.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class PrepareSessionBilling : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Never invent historical prices/actors or silently choose which duplicate session was real.
            migrationBuilder.Sql(
                """
                SET ANSI_WARNINGS ON;
                SET ANSI_NULLS ON;
                SET QUOTED_IDENTIFIER ON;
                SET ANSI_PADDING ON;
                SET CONCAT_NULL_YIELDS_NULL ON;
                SET ARITHABORT ON;
                SET NUMERIC_ROUNDABORT OFF;
                IF EXISTS (SELECT 1 FROM [GamingSessions] WHERE [Status] = N'Active' OR [EndTime] IS NULL GROUP BY [ComputerId] HAVING COUNT_BIG(*) > 1)
                    THROW 51024, 'SCRUM24: duplicate open sessions per computer; reconcile before migration.', 1;
                IF EXISTS (SELECT 1 FROM [GamingSessions] WHERE [Status] = N'Active' OR [EndTime] IS NULL GROUP BY [CustomerId] HAVING COUNT_BIG(*) > 1)
                    THROW 51024, 'SCRUM24: duplicate open sessions per customer; reconcile before migration.', 1;
                IF EXISTS (SELECT 1 FROM [SessionCharges] GROUP BY [GamingSessionId], [EndTime] HAVING COUNT_BIG(*) > 1)
                    THROW 51024, 'SCRUM24: duplicate session charge settlement times; reconcile before migration.', 1;
                """);

            migrationBuilder.DropIndex(
                name: "IX_SessionCharges_GamingSessionId",
                table: "SessionCharges");

            migrationBuilder.DropIndex(
                name: "IX_GamingSessions_ComputerId",
                table: "GamingSessions");

            migrationBuilder.DropIndex(
                name: "IX_GamingSessions_CustomerId",
                table: "GamingSessions");

            migrationBuilder.AddColumn<string>(
                name: "OperationKey",
                table: "SessionCharges",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true,
                collation: "Latin1_General_100_BIN2");

            migrationBuilder.AddColumn<int>(
                name: "WalletTransactionId",
                table: "SessionCharges",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CloseOperationKey",
                table: "GamingSessions",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true,
                collation: "Latin1_General_100_BIN2");

            migrationBuilder.AddColumn<string>(
                name: "CloseReason",
                table: "GamingSessions",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ClosedByEmployeeId",
                table: "GamingSessions",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "HourlyRateSnapshot",
                table: "GamingSessions",
                type: "decimal(18,0)",
                precision: 18,
                scale: 0,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OpenOperationKey",
                table: "GamingSessions",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true,
                collation: "Latin1_General_100_BIN2");

            migrationBuilder.AddColumn<int>(
                name: "OpenedByEmployeeId",
                table: "GamingSessions",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "OpenSessionSlot",
                table: "GamingSessions",
                type: "int",
                nullable: false,
                computedColumnSql: "CASE WHEN [Status] = N'Active' OR [EndTime] IS NULL THEN 0 ELSE [GamingSessionId] END",
                stored: true);

            migrationBuilder.CreateIndex(
                name: "IX_SessionCharges_GamingSessionId_EndTime",
                table: "SessionCharges",
                columns: new[] { "GamingSessionId", "EndTime" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SessionCharges_OperationKey",
                table: "SessionCharges",
                column: "OperationKey",
                unique: true,
                filter: "[OperationKey] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_SessionCharges_WalletTransactionId",
                table: "SessionCharges",
                column: "WalletTransactionId",
                unique: true,
                filter: "[WalletTransactionId] IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_SessionCharges_BillingMetadata",
                table: "SessionCharges",
                sql: "[OperationKey] IS NULL OR (LEN(LTRIM(RTRIM([OperationKey]))) > 0 AND [Amount] > 0 AND [EndTime] > [StartTime])");

            migrationBuilder.CreateIndex(
                name: "IX_GamingSessions_ClosedByEmployeeId",
                table: "GamingSessions",
                column: "ClosedByEmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_GamingSessions_CloseOperationKey",
                table: "GamingSessions",
                column: "CloseOperationKey",
                unique: true,
                filter: "[CloseOperationKey] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_GamingSessions_OpenedByEmployeeId",
                table: "GamingSessions",
                column: "OpenedByEmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_GamingSessions_OpenOperationKey",
                table: "GamingSessions",
                column: "OpenOperationKey",
                unique: true,
                filter: "[OpenOperationKey] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "UX_GamingSessions_OpenComputer",
                table: "GamingSessions",
                columns: new[] { "ComputerId", "OpenSessionSlot" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_GamingSessions_OpenCustomer",
                table: "GamingSessions",
                columns: new[] { "CustomerId", "OpenSessionSlot" },
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_GamingSessions_OperationKeys",
                table: "GamingSessions",
                sql: "([OpenOperationKey] IS NULL OR LEN(LTRIM(RTRIM([OpenOperationKey]))) > 0) AND ([CloseOperationKey] IS NULL OR LEN(LTRIM(RTRIM([CloseOperationKey]))) > 0)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_GamingSessions_RateSnapshot",
                table: "GamingSessions",
                sql: "[HourlyRateSnapshot] IS NULL OR [HourlyRateSnapshot] > 0");

            migrationBuilder.AddForeignKey(
                name: "FK_GamingSessions_Employees_ClosedByEmployeeId",
                table: "GamingSessions",
                column: "ClosedByEmployeeId",
                principalTable: "Employees",
                principalColumn: "EmployeeId");

            migrationBuilder.AddForeignKey(
                name: "FK_GamingSessions_Employees_OpenedByEmployeeId",
                table: "GamingSessions",
                column: "OpenedByEmployeeId",
                principalTable: "Employees",
                principalColumn: "EmployeeId");

            migrationBuilder.AddForeignKey(
                name: "FK_SessionCharges_WalletTransactions_WalletTransactionId",
                table: "SessionCharges",
                column: "WalletTransactionId",
                principalTable: "WalletTransactions",
                principalColumn: "TransactionId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                IF EXISTS (SELECT 1 FROM [GamingSessions] WHERE [HourlyRateSnapshot] IS NOT NULL OR [OpenedByEmployeeId] IS NOT NULL OR [ClosedByEmployeeId] IS NOT NULL OR [OpenOperationKey] IS NOT NULL OR [CloseOperationKey] IS NOT NULL OR [CloseReason] IS NOT NULL)
                    THROW 51025, 'SCRUM24: session billing metadata exists; verified reconciliation is required before downgrade.', 1;
                IF EXISTS (SELECT 1 FROM [SessionCharges] WHERE [OperationKey] IS NOT NULL OR [WalletTransactionId] IS NOT NULL)
                    THROW 51025, 'SCRUM24: session charge billing metadata exists; verified reconciliation is required before downgrade.', 1;
                """);

            migrationBuilder.DropForeignKey(
                name: "FK_GamingSessions_Employees_ClosedByEmployeeId",
                table: "GamingSessions");

            migrationBuilder.DropForeignKey(
                name: "FK_GamingSessions_Employees_OpenedByEmployeeId",
                table: "GamingSessions");

            migrationBuilder.DropForeignKey(
                name: "FK_SessionCharges_WalletTransactions_WalletTransactionId",
                table: "SessionCharges");

            migrationBuilder.DropIndex(
                name: "IX_SessionCharges_GamingSessionId_EndTime",
                table: "SessionCharges");

            migrationBuilder.DropIndex(
                name: "IX_SessionCharges_OperationKey",
                table: "SessionCharges");

            migrationBuilder.DropIndex(
                name: "IX_SessionCharges_WalletTransactionId",
                table: "SessionCharges");

            migrationBuilder.DropCheckConstraint(
                name: "CK_SessionCharges_BillingMetadata",
                table: "SessionCharges");

            migrationBuilder.DropIndex(
                name: "IX_GamingSessions_ClosedByEmployeeId",
                table: "GamingSessions");

            migrationBuilder.DropIndex(
                name: "IX_GamingSessions_CloseOperationKey",
                table: "GamingSessions");

            migrationBuilder.DropIndex(
                name: "IX_GamingSessions_OpenedByEmployeeId",
                table: "GamingSessions");

            migrationBuilder.DropIndex(
                name: "IX_GamingSessions_OpenOperationKey",
                table: "GamingSessions");

            migrationBuilder.DropIndex(
                name: "UX_GamingSessions_OpenComputer",
                table: "GamingSessions");

            migrationBuilder.DropIndex(
                name: "UX_GamingSessions_OpenCustomer",
                table: "GamingSessions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_GamingSessions_OperationKeys",
                table: "GamingSessions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_GamingSessions_RateSnapshot",
                table: "GamingSessions");

            migrationBuilder.DropColumn(
                name: "OpenSessionSlot",
                table: "GamingSessions");

            migrationBuilder.DropColumn(
                name: "OperationKey",
                table: "SessionCharges");

            migrationBuilder.DropColumn(
                name: "WalletTransactionId",
                table: "SessionCharges");

            migrationBuilder.DropColumn(
                name: "CloseOperationKey",
                table: "GamingSessions");

            migrationBuilder.DropColumn(
                name: "CloseReason",
                table: "GamingSessions");

            migrationBuilder.DropColumn(
                name: "ClosedByEmployeeId",
                table: "GamingSessions");

            migrationBuilder.DropColumn(
                name: "HourlyRateSnapshot",
                table: "GamingSessions");

            migrationBuilder.DropColumn(
                name: "OpenOperationKey",
                table: "GamingSessions");

            migrationBuilder.DropColumn(
                name: "OpenedByEmployeeId",
                table: "GamingSessions");

            migrationBuilder.CreateIndex(
                name: "IX_SessionCharges_GamingSessionId",
                table: "SessionCharges",
                column: "GamingSessionId");

            migrationBuilder.CreateIndex(
                name: "IX_GamingSessions_ComputerId",
                table: "GamingSessions",
                column: "ComputerId");

            migrationBuilder.CreateIndex(
                name: "IX_GamingSessions_CustomerId",
                table: "GamingSessions",
                column: "CustomerId");
        }
    }
}
