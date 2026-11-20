using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CyberManagement.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ImplementFoodOrderCheckout : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_WalletTransactions_WriterMetadata",
                table: "WalletTransactions");

            migrationBuilder.AlterColumn<int>(
                name: "ChangedBy",
                table: "OrderStatusHistory",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AddColumn<string>(
                name: "ActorType",
                table: "OrderStatusHistory",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: false,
                defaultValue: "Staff");

            migrationBuilder.AddColumn<int>(
                name: "OrderDetailId",
                table: "OrderStatusHistory",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Reason",
                table: "OrderStatusHistory",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "EmployeeId",
                table: "Orders",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AddColumn<string>(
                name: "ActorType",
                table: "Orders",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: false,
                defaultValue: "Staff");

            migrationBuilder.AddColumn<DateTime>(
                name: "LastChangedAtUtc",
                table: "Orders",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<string>(
                name: "PaymentMethod",
                table: "Orders",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "Version",
                table: "Orders",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<DateTime>(
                name: "ConfirmedAtUtc",
                table: "OrderDetails",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ConfirmedByEmployeeId",
                table: "OrderDetails",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LineNumber",
                table: "OrderDetails",
                type: "int",
                nullable: false,
                defaultValue: 0);

            // Giữ nguyên tiền và phương thức lịch sử chưa xác định.
            // Dynamic SQL also works when the idempotent script adds columns in this batch.
            migrationBuilder.Sql("EXEC(N'UPDATE [Orders] SET [LastChangedAtUtc] = [OrderTime]; WITH numbered AS (SELECT [OrderDetailId], ROW_NUMBER() OVER (PARTITION BY [OrderId] ORDER BY [OrderDetailId]) AS n FROM [OrderDetails]) UPDATE d SET [LineNumber] = n.n FROM [OrderDetails] d JOIN numbered n ON n.[OrderDetailId] = d.[OrderDetailId];');");

            migrationBuilder.CreateTable(
                name: "OrderOperations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ActorType = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false, collation: "Latin1_General_100_BIN2"),
                    ActorId = table.Column<int>(type: "int", nullable: false),
                    Route = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false, collation: "Latin1_General_100_BIN2"),
                    Key = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false, collation: "Latin1_General_100_BIN2"),
                    RequestHash = table.Column<string>(type: "nchar(64)", fixedLength: true, maxLength: 64, nullable: false),
                    OrderId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrderOperations", x => x.Id);
                    table.CheckConstraint("CK_OrderOperations_Actor", "[ActorType] IN ('Staff','Customer') AND [ActorId] > 0");
                    table.ForeignKey(
                        name: "FK_OrderOperations_Orders_OrderId",
                        column: x => x.OrderId,
                        principalTable: "Orders",
                        principalColumn: "OrderId");
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_WalletTransactions_WriterMetadata",
                table: "WalletTransactions",
                sql: "([OperationKey] IS NULL AND [Kind] IS NULL AND [SourceType] IS NULL AND [SourceId] IS NULL AND [OriginalDebitId] IS NULL AND [ActorType] IS NULL AND [ActorEmployeeId] IS NULL) OR ([OperationKey] IS NOT NULL AND LEN(LTRIM(RTRIM([OperationKey]))) > 0 AND [Kind] IS NOT NULL AND [Kind] IN ('Credit','Debit','Refund') AND [SourceType] IS NOT NULL AND LEN(LTRIM(RTRIM([SourceType]))) > 0 AND [SourceId] IS NOT NULL AND LEN(LTRIM(RTRIM([SourceId]))) > 0 AND [ActorType] IS NOT NULL AND (([ActorType] = 'Staff' AND [ActorEmployeeId] IS NOT NULL) OR ([ActorType] = 'System' AND [ActorEmployeeId] IS NULL) OR ([ActorType] = 'Customer' AND [ActorEmployeeId] IS NULL AND [Kind] = 'Debit' AND [Type] = 'OrderPayment' AND [SourceType] = 'Order')) AND [BalanceAfter] >= 0 AND (([Kind] = 'Debit' AND [Amount] < 0 AND [OriginalDebitId] IS NULL) OR ([Kind] = 'Credit' AND [Amount] > 0 AND [OriginalDebitId] IS NULL) OR ([Kind] = 'Refund' AND [Amount] > 0 AND [OriginalDebitId] IS NOT NULL AND [OriginalDebitId] <> [TransactionId])))");

            migrationBuilder.CreateIndex(
                name: "IX_OrderStatusHistory_OrderDetailId",
                table: "OrderStatusHistory",
                column: "OrderDetailId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Orders_Actor",
                table: "Orders",
                sql: "([ActorType] = 'Staff' AND [EmployeeId] IS NOT NULL) OR ([ActorType] = 'Customer' AND [EmployeeId] IS NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Orders_CheckoutMethod",
                table: "Orders",
                sql: "[PaymentMethod] IS NULL OR [PaymentMethod] IN ('Wallet','Cash','ZaloPay')");

            migrationBuilder.CreateIndex(
                name: "IX_OrderDetails_ConfirmedByEmployeeId",
                table: "OrderDetails",
                column: "ConfirmedByEmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_OrderOperations_ActorType_ActorId_Route_Key",
                table: "OrderOperations",
                columns: new[] { "ActorType", "ActorId", "Route", "Key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OrderOperations_OrderId",
                table: "OrderOperations",
                column: "OrderId");

            migrationBuilder.AddForeignKey(
                name: "FK_OrderDetails_Employees_ConfirmedByEmployeeId",
                table: "OrderDetails",
                column: "ConfirmedByEmployeeId",
                principalTable: "Employees",
                principalColumn: "EmployeeId");

            migrationBuilder.AddForeignKey(
                name: "FK_OrderStatusHistory_OrderDetails_OrderDetailId",
                table: "OrderStatusHistory",
                column: "OrderDetailId",
                principalTable: "OrderDetails",
                principalColumn: "OrderDetailId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("IF EXISTS (SELECT 1 FROM [OrderOperations]) OR EXISTS (SELECT 1 FROM [Orders] WHERE [PaymentMethod] IS NOT NULL OR [EmployeeId] IS NULL) OR EXISTS (SELECT 1 FROM [WalletTransactions] WHERE [ActorType] = 'Customer') THROW 51000, 'Checkout history exists; reconcile and archive it before downgrade.', 1;");
            migrationBuilder.DropForeignKey(
                name: "FK_OrderDetails_Employees_ConfirmedByEmployeeId",
                table: "OrderDetails");

            migrationBuilder.DropForeignKey(
                name: "FK_OrderStatusHistory_OrderDetails_OrderDetailId",
                table: "OrderStatusHistory");

            migrationBuilder.DropTable(
                name: "OrderOperations");

            migrationBuilder.DropCheckConstraint(
                name: "CK_WalletTransactions_WriterMetadata",
                table: "WalletTransactions");

            migrationBuilder.DropIndex(
                name: "IX_OrderStatusHistory_OrderDetailId",
                table: "OrderStatusHistory");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Orders_Actor",
                table: "Orders");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Orders_CheckoutMethod",
                table: "Orders");

            migrationBuilder.DropIndex(
                name: "IX_OrderDetails_ConfirmedByEmployeeId",
                table: "OrderDetails");

            migrationBuilder.DropColumn(
                name: "ActorType",
                table: "OrderStatusHistory");

            migrationBuilder.DropColumn(
                name: "OrderDetailId",
                table: "OrderStatusHistory");

            migrationBuilder.DropColumn(
                name: "Reason",
                table: "OrderStatusHistory");

            migrationBuilder.DropColumn(
                name: "ActorType",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "LastChangedAtUtc",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "PaymentMethod",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "Version",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "ConfirmedAtUtc",
                table: "OrderDetails");

            migrationBuilder.DropColumn(
                name: "ConfirmedByEmployeeId",
                table: "OrderDetails");

            migrationBuilder.DropColumn(
                name: "LineNumber",
                table: "OrderDetails");

            migrationBuilder.AlterColumn<int>(
                name: "ChangedBy",
                table: "OrderStatusHistory",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "EmployeeId",
                table: "Orders",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_WalletTransactions_WriterMetadata",
                table: "WalletTransactions",
                sql: "([OperationKey] IS NULL AND [Kind] IS NULL AND [SourceType] IS NULL AND [SourceId] IS NULL AND [OriginalDebitId] IS NULL AND [ActorType] IS NULL AND [ActorEmployeeId] IS NULL) OR ([OperationKey] IS NOT NULL AND LEN(LTRIM(RTRIM([OperationKey]))) > 0 AND [Kind] IS NOT NULL AND [Kind] IN ('Credit','Debit','Refund') AND [SourceType] IS NOT NULL AND LEN(LTRIM(RTRIM([SourceType]))) > 0 AND [SourceId] IS NOT NULL AND LEN(LTRIM(RTRIM([SourceId]))) > 0 AND [ActorType] IS NOT NULL AND (([ActorType] = 'Staff' AND [ActorEmployeeId] IS NOT NULL) OR ([ActorType] = 'System' AND [ActorEmployeeId] IS NULL)) AND [BalanceAfter] >= 0 AND (([Kind] = 'Debit' AND [Amount] < 0 AND [OriginalDebitId] IS NULL) OR ([Kind] = 'Credit' AND [Amount] > 0 AND [OriginalDebitId] IS NULL) OR ([Kind] = 'Refund' AND [Amount] > 0 AND [OriginalDebitId] IS NOT NULL AND [OriginalDebitId] <> [TransactionId])))");
        }
    }
}
