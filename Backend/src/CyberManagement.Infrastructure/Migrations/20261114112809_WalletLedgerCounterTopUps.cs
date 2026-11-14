using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CyberManagement.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class WalletLedgerCounterTopUps : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Type",
                table: "WalletTransactions",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                collation: "Latin1_General_100_BIN2",
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50);

            migrationBuilder.AddColumn<int>(
                name: "ActorEmployeeId",
                table: "WalletTransactions",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ActorType",
                table: "WalletTransactions",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Kind",
                table: "WalletTransactions",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OperationKey",
                table: "WalletTransactions",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true,
                collation: "Latin1_General_100_BIN2");

            migrationBuilder.AddColumn<int>(
                name: "OriginalDebitId",
                table: "WalletTransactions",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SourceId",
                table: "WalletTransactions",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true,
                collation: "Latin1_General_100_BIN2");

            migrationBuilder.AddColumn<string>(
                name: "SourceType",
                table: "WalletTransactions",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true,
                collation: "Latin1_General_100_BIN2");

            migrationBuilder.CreateTable(
                name: "CounterTopUpRequests",
                columns: table => new
                {
                    RequestId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CustomerId = table.Column<int>(type: "int", nullable: false),
                    RequestKey = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,0)", precision: 18, scale: 0, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, collation: "Latin1_General_100_BIN2"),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ApprovedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ApprovedByEmployeeId = table.Column<int>(type: "int", nullable: true),
                    CashAmountReceived = table.Column<decimal>(type: "decimal(18,0)", precision: 18, scale: 0, nullable: true),
                    WalletTransactionId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CounterTopUpRequests", x => x.RequestId);
                    table.CheckConstraint("CK_CounterTopUpRequests_Amount", "[Amount] IN (10000,20000,30000,50000,100000,200000,300000,500000)");
                    table.CheckConstraint("CK_CounterTopUpRequests_State", "([Status] = N'Pending' AND [ApprovedAt] IS NULL AND [ApprovedByEmployeeId] IS NULL AND [CashAmountReceived] IS NULL AND [WalletTransactionId] IS NULL) OR ([Status] = N'Approved' AND [ApprovedAt] IS NOT NULL AND [ApprovedAt] >= [CreatedAt] AND [ApprovedByEmployeeId] IS NOT NULL AND [CashAmountReceived] IS NOT NULL AND [CashAmountReceived] = [Amount] AND [WalletTransactionId] IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_CounterTopUpRequests_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "CustomerId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CounterTopUpRequests_Employees_ApprovedByEmployeeId",
                        column: x => x.ApprovedByEmployeeId,
                        principalTable: "Employees",
                        principalColumn: "EmployeeId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CounterTopUpRequests_WalletTransactions_WalletTransactionId",
                        column: x => x.WalletTransactionId,
                        principalTable: "WalletTransactions",
                        principalColumn: "TransactionId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WalletTransactions_ActorEmployeeId",
                table: "WalletTransactions",
                column: "ActorEmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_WalletTransactions_OperationKey",
                table: "WalletTransactions",
                column: "OperationKey",
                unique: true,
                filter: "[OperationKey] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_WalletTransactions_OriginalDebitId",
                table: "WalletTransactions",
                column: "OriginalDebitId");

            migrationBuilder.CreateIndex(
                name: "IX_WalletTransactions_SourceType_SourceId_Type",
                table: "WalletTransactions",
                columns: new[] { "SourceType", "SourceId", "Type" },
                unique: true,
                filter: "[SourceType] IS NOT NULL AND [SourceId] IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_WalletTransactions_WriterMetadata",
                table: "WalletTransactions",
                sql: "([OperationKey] IS NULL AND [Kind] IS NULL AND [SourceType] IS NULL AND [SourceId] IS NULL AND [OriginalDebitId] IS NULL AND [ActorType] IS NULL AND [ActorEmployeeId] IS NULL) OR ([OperationKey] IS NOT NULL AND LEN(LTRIM(RTRIM([OperationKey]))) > 0 AND [Kind] IS NOT NULL AND [Kind] IN ('Credit','Debit','Refund') AND [SourceType] IS NOT NULL AND LEN(LTRIM(RTRIM([SourceType]))) > 0 AND [SourceId] IS NOT NULL AND LEN(LTRIM(RTRIM([SourceId]))) > 0 AND [ActorType] IS NOT NULL AND (([ActorType] = 'Staff' AND [ActorEmployeeId] IS NOT NULL) OR ([ActorType] = 'System' AND [ActorEmployeeId] IS NULL)) AND [BalanceAfter] >= 0 AND (([Kind] = 'Debit' AND [Amount] < 0 AND [OriginalDebitId] IS NULL) OR ([Kind] = 'Credit' AND [Amount] > 0 AND [OriginalDebitId] IS NULL) OR ([Kind] = 'Refund' AND [Amount] > 0 AND [OriginalDebitId] IS NOT NULL AND [OriginalDebitId] <> [TransactionId])))");

            migrationBuilder.CreateIndex(
                name: "IX_CounterTopUpRequests_ApprovedByEmployeeId",
                table: "CounterTopUpRequests",
                column: "ApprovedByEmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_CounterTopUpRequests_CustomerId_RequestKey",
                table: "CounterTopUpRequests",
                columns: new[] { "CustomerId", "RequestKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CounterTopUpRequests_WalletTransactionId",
                table: "CounterTopUpRequests",
                column: "WalletTransactionId",
                unique: true,
                filter: "[WalletTransactionId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "UX_CounterTopUpRequests_PendingCustomer",
                table: "CounterTopUpRequests",
                column: "CustomerId",
                unique: true,
                filter: "[Status] = N'Pending'");

            migrationBuilder.AddForeignKey(
                name: "FK_WalletTransactions_Employees_ActorEmployeeId",
                table: "WalletTransactions",
                column: "ActorEmployeeId",
                principalTable: "Employees",
                principalColumn: "EmployeeId");

            migrationBuilder.AddForeignKey(
                name: "FK_WalletTransactions_WalletTransactions_OriginalDebitId",
                table: "WalletTransactions",
                column: "OriginalDebitId",
                principalTable: "WalletTransactions",
                principalColumn: "TransactionId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("IF EXISTS (SELECT 1 FROM [CounterTopUpRequests]) OR EXISTS (SELECT 1 FROM [WalletTransactions] WHERE [OperationKey] IS NOT NULL) THROW 51021, 'Cannot discard counter receipts or wallet audit metadata.', 1;");
            migrationBuilder.DropForeignKey(
                name: "FK_WalletTransactions_Employees_ActorEmployeeId",
                table: "WalletTransactions");

            migrationBuilder.DropForeignKey(
                name: "FK_WalletTransactions_WalletTransactions_OriginalDebitId",
                table: "WalletTransactions");

            migrationBuilder.DropTable(
                name: "CounterTopUpRequests");

            migrationBuilder.DropIndex(
                name: "IX_WalletTransactions_ActorEmployeeId",
                table: "WalletTransactions");

            migrationBuilder.DropIndex(
                name: "IX_WalletTransactions_OperationKey",
                table: "WalletTransactions");

            migrationBuilder.DropIndex(
                name: "IX_WalletTransactions_OriginalDebitId",
                table: "WalletTransactions");

            migrationBuilder.DropIndex(
                name: "IX_WalletTransactions_SourceType_SourceId_Type",
                table: "WalletTransactions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_WalletTransactions_WriterMetadata",
                table: "WalletTransactions");

            migrationBuilder.DropColumn(
                name: "ActorEmployeeId",
                table: "WalletTransactions");

            migrationBuilder.DropColumn(
                name: "ActorType",
                table: "WalletTransactions");

            migrationBuilder.DropColumn(
                name: "Kind",
                table: "WalletTransactions");

            migrationBuilder.DropColumn(
                name: "OperationKey",
                table: "WalletTransactions");

            migrationBuilder.DropColumn(
                name: "OriginalDebitId",
                table: "WalletTransactions");

            migrationBuilder.DropColumn(
                name: "SourceId",
                table: "WalletTransactions");

            migrationBuilder.DropColumn(
                name: "SourceType",
                table: "WalletTransactions");

            migrationBuilder.AlterColumn<string>(
                name: "Type",
                table: "WalletTransactions",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50,
                oldCollation: "Latin1_General_100_BIN2");
        }
    }
}
