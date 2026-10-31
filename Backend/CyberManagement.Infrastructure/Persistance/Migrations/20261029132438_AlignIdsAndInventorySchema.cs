using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CyberManagement.Infrastructure.Persistance.Migrations
{
    /// <inheritdoc />
    public partial class AlignIdsAndInventorySchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            ConfigureSqlSession(migrationBuilder);
            DropExistingFilteredIndexes(migrationBuilder);

            DropExistingKeys(migrationBuilder);

            migrationBuilder.DropCheckConstraint(
                name: "CK_Inventory_Type",
                table: "InventoryTransactions");

            migrationBuilder.AlterColumn<long>(
                name: "SessionId",
                table: "WalletTransactions",
                type: "bigint",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AlterColumn<long>(
                name: "OrderId",
                table: "WalletTransactions",
                type: "bigint",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AlterColumn<long>(
                name: "OrderDetailId",
                table: "WalletTransactions",
                type: "bigint",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AlterColumn<long>(
                name: "CustomerId",
                table: "WalletTransactions",
                type: "bigint",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AlterColumn<long>(
                name: "TransactionId",
                table: "WalletTransactions",
                type: "bigint",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int")
                .Annotation("SqlServer:Identity", "1, 1")
                .OldAnnotation("SqlServer:Identity", "1, 1");

            migrationBuilder.AlterColumn<long>(
                name: "RoleId",
                table: "Roles",
                type: "bigint",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int")
                .Annotation("SqlServer:Identity", "1, 1")
                .OldAnnotation("SqlServer:Identity", "1, 1");

            migrationBuilder.AlterColumn<long>(
                name: "PromotionId",
                table: "Promotions",
                type: "bigint",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int")
                .Annotation("SqlServer:Identity", "1, 1")
                .OldAnnotation("SqlServer:Identity", "1, 1");

            migrationBuilder.AlterColumn<long>(
                name: "CategoryId",
                table: "Products",
                type: "bigint",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AlterColumn<long>(
                name: "ProductId",
                table: "Products",
                type: "bigint",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int")
                .Annotation("SqlServer:Identity", "1, 1")
                .OldAnnotation("SqlServer:Identity", "1, 1");

            migrationBuilder.AlterColumn<long>(
                name: "WalletTransactionId",
                table: "Payments",
                type: "bigint",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AlterColumn<long>(
                name: "InvoiceId",
                table: "Payments",
                type: "bigint",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AlterColumn<long>(
                name: "PaymentId",
                table: "Payments",
                type: "bigint",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int")
                .Annotation("SqlServer:Identity", "1, 1")
                .OldAnnotation("SqlServer:Identity", "1, 1");

            migrationBuilder.AlterColumn<long>(
                name: "OrderId",
                table: "OrderStatusHistories",
                type: "bigint",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AlterColumn<long>(
                name: "OrderDetailId",
                table: "OrderStatusHistories",
                type: "bigint",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AlterColumn<long>(
                name: "ChangedBy",
                table: "OrderStatusHistories",
                type: "bigint",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AlterColumn<long>(
                name: "HistoryId",
                table: "OrderStatusHistories",
                type: "bigint",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int")
                .Annotation("SqlServer:Identity", "1, 1")
                .OldAnnotation("SqlServer:Identity", "1, 1");

            migrationBuilder.AlterColumn<long>(
                name: "SessionId",
                table: "Orders",
                type: "bigint",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AlterColumn<long>(
                name: "InvoiceId",
                table: "Orders",
                type: "bigint",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AlterColumn<long>(
                name: "EmployeeId",
                table: "Orders",
                type: "bigint",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AlterColumn<long>(
                name: "CustomerId",
                table: "Orders",
                type: "bigint",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AlterColumn<long>(
                name: "OrderId",
                table: "Orders",
                type: "bigint",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int")
                .Annotation("SqlServer:Identity", "1, 1")
                .OldAnnotation("SqlServer:Identity", "1, 1");

            migrationBuilder.AlterColumn<long>(
                name: "RequestedByCustomerId",
                table: "OrderItemCancelRequests",
                type: "bigint",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AlterColumn<long>(
                name: "OrderDetailId",
                table: "OrderItemCancelRequests",
                type: "bigint",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AlterColumn<long>(
                name: "RequestId",
                table: "OrderItemCancelRequests",
                type: "bigint",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int")
                .Annotation("SqlServer:Identity", "1, 1")
                .OldAnnotation("SqlServer:Identity", "1, 1");

            migrationBuilder.AlterColumn<long>(
                name: "ProductId",
                table: "OrderDetails",
                type: "bigint",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AlterColumn<long>(
                name: "OrderId",
                table: "OrderDetails",
                type: "bigint",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AlterColumn<long>(
                name: "OrderDetailId",
                table: "OrderDetails",
                type: "bigint",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int")
                .Annotation("SqlServer:Identity", "1, 1")
                .OldAnnotation("SqlServer:Identity", "1, 1");

            migrationBuilder.AlterColumn<long>(
                name: "SessionId",
                table: "Invoices",
                type: "bigint",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AlterColumn<long>(
                name: "PromotionId",
                table: "Invoices",
                type: "bigint",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AlterColumn<long>(
                name: "EmployeeId",
                table: "Invoices",
                type: "bigint",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AlterColumn<long>(
                name: "CustomerId",
                table: "Invoices",
                type: "bigint",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AlterColumn<long>(
                name: "InvoiceId",
                table: "Invoices",
                type: "bigint",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int")
                .Annotation("SqlServer:Identity", "1, 1")
                .OldAnnotation("SqlServer:Identity", "1, 1");

            migrationBuilder.AlterColumn<long>(
                name: "InvoiceId",
                table: "InvoiceDetails",
                type: "bigint",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AlterColumn<long>(
                name: "InvoiceDetailId",
                table: "InvoiceDetails",
                type: "bigint",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int")
                .Annotation("SqlServer:Identity", "1, 1")
                .OldAnnotation("SqlServer:Identity", "1, 1");

            migrationBuilder.AlterColumn<long>(
                name: "ReferenceId",
                table: "InventoryTransactions",
                type: "bigint",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AlterColumn<string>(
                name: "Reason",
                table: "InventoryTransactions",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(250)",
                oldMaxLength: 250);

            migrationBuilder.AlterColumn<long>(
                name: "ProductId",
                table: "InventoryTransactions",
                type: "bigint",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AlterColumn<long>(
                name: "EmployeeId",
                table: "InventoryTransactions",
                type: "bigint",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "CreatedAt",
                table: "InventoryTransactions",
                type: "datetimeoffset",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "datetime2");

            migrationBuilder.AlterColumn<long>(
                name: "InvTransId",
                table: "InventoryTransactions",
                type: "bigint",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int")
                .Annotation("SqlServer:Identity", "1, 1")
                .OldAnnotation("SqlServer:Identity", "1, 1");

            migrationBuilder.AddColumn<long>(
                name: "OrderDetailId",
                table: "InventoryTransactions",
                type: "bigint",
                nullable: true);

            migrationBuilder.AlterColumn<long>(
                name: "CustomerId",
                table: "GamingSessions",
                type: "bigint",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AlterColumn<long>(
                name: "ComputerId",
                table: "GamingSessions",
                type: "bigint",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AlterColumn<long>(
                name: "SessionId",
                table: "GamingSessions",
                type: "bigint",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int")
                .Annotation("SqlServer:Identity", "1, 1")
                .OldAnnotation("SqlServer:Identity", "1, 1");

            migrationBuilder.AlterColumn<long>(
                name: "RoleId",
                table: "Employees",
                type: "bigint",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AlterColumn<long>(
                name: "EmployeeId",
                table: "Employees",
                type: "bigint",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int")
                .Annotation("SqlServer:Identity", "1, 1")
                .OldAnnotation("SqlServer:Identity", "1, 1");

            migrationBuilder.AlterColumn<long>(
                name: "CustomerId",
                table: "Customers",
                type: "bigint",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int")
                .Annotation("SqlServer:Identity", "1, 1")
                .OldAnnotation("SqlServer:Identity", "1, 1");

            migrationBuilder.AlterColumn<long>(
                name: "ComputerTypeId",
                table: "ComputerTypes",
                type: "bigint",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int")
                .Annotation("SqlServer:Identity", "1, 1")
                .OldAnnotation("SqlServer:Identity", "1, 1");

            migrationBuilder.AlterColumn<long>(
                name: "RoomId",
                table: "Computers",
                type: "bigint",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AlterColumn<long>(
                name: "ComputerTypeId",
                table: "Computers",
                type: "bigint",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AlterColumn<long>(
                name: "ComputerId",
                table: "Computers",
                type: "bigint",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int")
                .Annotation("SqlServer:Identity", "1, 1")
                .OldAnnotation("SqlServer:Identity", "1, 1");

            migrationBuilder.AlterColumn<long>(
                name: "RoomId",
                table: "ComputerRooms",
                type: "bigint",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int")
                .Annotation("SqlServer:Identity", "1, 1")
                .OldAnnotation("SqlServer:Identity", "1, 1");

            migrationBuilder.AlterColumn<long>(
                name: "CategoryId",
                table: "Categories",
                type: "bigint",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int")
                .Annotation("SqlServer:Identity", "1, 1")
                .OldAnnotation("SqlServer:Identity", "1, 1");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryTransactions_OrderDetailId_Type",
                table: "InventoryTransactions",
                columns: new[] { "OrderDetailId", "Type" },
                unique: true,
                filter: "[OrderDetailId] IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Inventory_Reason",
                table: "InventoryTransactions",
                sql: "LEN(LTRIM(RTRIM([Reason]))) > 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Inventory_Type",
                table: "InventoryTransactions",
                sql: "([Type] = 'Receive' AND [Quantity] > 0\r\n    AND [OrderDetailId] IS NULL)\r\nOR\r\n([Type] = 'Adjust' AND [Quantity] <> 0\r\n    AND [OrderDetailId] IS NULL)\r\nOR\r\n([Type] = 'Sale' AND [Quantity] < 0\r\n    AND [OrderDetailId] IS NOT NULL)\r\nOR\r\n([Type] = 'Return' AND [Quantity] > 0\r\n    AND [OrderDetailId] IS NOT NULL)");

            RestoreExistingFilteredIndexes(migrationBuilder);
            RestoreExistingKeys(migrationBuilder);

            migrationBuilder.AddForeignKey(
                name: "FK_InventoryTransactions_OrderDetails_OrderDetailId",
                table: "InventoryTransactions",
                column: "OrderDetailId",
                principalTable: "OrderDetails",
                principalColumn: "OrderDetailId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            ConfigureSqlSession(migrationBuilder);
            DropExistingFilteredIndexes(migrationBuilder);

            migrationBuilder.DropForeignKey(
                name: "FK_InventoryTransactions_OrderDetails_OrderDetailId",
                table: "InventoryTransactions");

            migrationBuilder.DropIndex(
                name: "IX_InventoryTransactions_OrderDetailId_Type",
                table: "InventoryTransactions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Inventory_Reason",
                table: "InventoryTransactions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Inventory_Type",
                table: "InventoryTransactions");

            migrationBuilder.DropColumn(
                name: "OrderDetailId",
                table: "InventoryTransactions");

            DropExistingKeys(migrationBuilder);

            migrationBuilder.AlterColumn<int>(
                name: "SessionId",
                table: "WalletTransactions",
                type: "int",
                nullable: true,
                oldClrType: typeof(long),
                oldType: "bigint",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "OrderId",
                table: "WalletTransactions",
                type: "int",
                nullable: true,
                oldClrType: typeof(long),
                oldType: "bigint",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "OrderDetailId",
                table: "WalletTransactions",
                type: "int",
                nullable: true,
                oldClrType: typeof(long),
                oldType: "bigint",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "CustomerId",
                table: "WalletTransactions",
                type: "int",
                nullable: false,
                oldClrType: typeof(long),
                oldType: "bigint");

            migrationBuilder.AlterColumn<int>(
                name: "TransactionId",
                table: "WalletTransactions",
                type: "int",
                nullable: false,
                oldClrType: typeof(long),
                oldType: "bigint")
                .Annotation("SqlServer:Identity", "1, 1")
                .OldAnnotation("SqlServer:Identity", "1, 1");

            migrationBuilder.AlterColumn<int>(
                name: "RoleId",
                table: "Roles",
                type: "int",
                nullable: false,
                oldClrType: typeof(long),
                oldType: "bigint")
                .Annotation("SqlServer:Identity", "1, 1")
                .OldAnnotation("SqlServer:Identity", "1, 1");

            migrationBuilder.AlterColumn<int>(
                name: "PromotionId",
                table: "Promotions",
                type: "int",
                nullable: false,
                oldClrType: typeof(long),
                oldType: "bigint")
                .Annotation("SqlServer:Identity", "1, 1")
                .OldAnnotation("SqlServer:Identity", "1, 1");

            migrationBuilder.AlterColumn<int>(
                name: "CategoryId",
                table: "Products",
                type: "int",
                nullable: false,
                oldClrType: typeof(long),
                oldType: "bigint");

            migrationBuilder.AlterColumn<int>(
                name: "ProductId",
                table: "Products",
                type: "int",
                nullable: false,
                oldClrType: typeof(long),
                oldType: "bigint")
                .Annotation("SqlServer:Identity", "1, 1")
                .OldAnnotation("SqlServer:Identity", "1, 1");

            migrationBuilder.AlterColumn<int>(
                name: "WalletTransactionId",
                table: "Payments",
                type: "int",
                nullable: true,
                oldClrType: typeof(long),
                oldType: "bigint",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "InvoiceId",
                table: "Payments",
                type: "int",
                nullable: false,
                oldClrType: typeof(long),
                oldType: "bigint");

            migrationBuilder.AlterColumn<int>(
                name: "PaymentId",
                table: "Payments",
                type: "int",
                nullable: false,
                oldClrType: typeof(long),
                oldType: "bigint")
                .Annotation("SqlServer:Identity", "1, 1")
                .OldAnnotation("SqlServer:Identity", "1, 1");

            migrationBuilder.AlterColumn<int>(
                name: "OrderId",
                table: "OrderStatusHistories",
                type: "int",
                nullable: false,
                oldClrType: typeof(long),
                oldType: "bigint");

            migrationBuilder.AlterColumn<int>(
                name: "OrderDetailId",
                table: "OrderStatusHistories",
                type: "int",
                nullable: true,
                oldClrType: typeof(long),
                oldType: "bigint",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "ChangedBy",
                table: "OrderStatusHistories",
                type: "int",
                nullable: false,
                oldClrType: typeof(long),
                oldType: "bigint");

            migrationBuilder.AlterColumn<int>(
                name: "HistoryId",
                table: "OrderStatusHistories",
                type: "int",
                nullable: false,
                oldClrType: typeof(long),
                oldType: "bigint")
                .Annotation("SqlServer:Identity", "1, 1")
                .OldAnnotation("SqlServer:Identity", "1, 1");

            migrationBuilder.AlterColumn<int>(
                name: "SessionId",
                table: "Orders",
                type: "int",
                nullable: true,
                oldClrType: typeof(long),
                oldType: "bigint",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "InvoiceId",
                table: "Orders",
                type: "int",
                nullable: true,
                oldClrType: typeof(long),
                oldType: "bigint",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "EmployeeId",
                table: "Orders",
                type: "int",
                nullable: true,
                oldClrType: typeof(long),
                oldType: "bigint",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "CustomerId",
                table: "Orders",
                type: "int",
                nullable: true,
                oldClrType: typeof(long),
                oldType: "bigint",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "OrderId",
                table: "Orders",
                type: "int",
                nullable: false,
                oldClrType: typeof(long),
                oldType: "bigint")
                .Annotation("SqlServer:Identity", "1, 1")
                .OldAnnotation("SqlServer:Identity", "1, 1");

            migrationBuilder.AlterColumn<int>(
                name: "RequestedByCustomerId",
                table: "OrderItemCancelRequests",
                type: "int",
                nullable: false,
                oldClrType: typeof(long),
                oldType: "bigint");

            migrationBuilder.AlterColumn<int>(
                name: "OrderDetailId",
                table: "OrderItemCancelRequests",
                type: "int",
                nullable: false,
                oldClrType: typeof(long),
                oldType: "bigint");

            migrationBuilder.AlterColumn<int>(
                name: "RequestId",
                table: "OrderItemCancelRequests",
                type: "int",
                nullable: false,
                oldClrType: typeof(long),
                oldType: "bigint")
                .Annotation("SqlServer:Identity", "1, 1")
                .OldAnnotation("SqlServer:Identity", "1, 1");

            migrationBuilder.AlterColumn<int>(
                name: "ProductId",
                table: "OrderDetails",
                type: "int",
                nullable: false,
                oldClrType: typeof(long),
                oldType: "bigint");

            migrationBuilder.AlterColumn<int>(
                name: "OrderId",
                table: "OrderDetails",
                type: "int",
                nullable: false,
                oldClrType: typeof(long),
                oldType: "bigint");

            migrationBuilder.AlterColumn<int>(
                name: "OrderDetailId",
                table: "OrderDetails",
                type: "int",
                nullable: false,
                oldClrType: typeof(long),
                oldType: "bigint")
                .Annotation("SqlServer:Identity", "1, 1")
                .OldAnnotation("SqlServer:Identity", "1, 1");

            migrationBuilder.AlterColumn<int>(
                name: "SessionId",
                table: "Invoices",
                type: "int",
                nullable: true,
                oldClrType: typeof(long),
                oldType: "bigint",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "PromotionId",
                table: "Invoices",
                type: "int",
                nullable: true,
                oldClrType: typeof(long),
                oldType: "bigint",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "EmployeeId",
                table: "Invoices",
                type: "int",
                nullable: true,
                oldClrType: typeof(long),
                oldType: "bigint",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "CustomerId",
                table: "Invoices",
                type: "int",
                nullable: false,
                oldClrType: typeof(long),
                oldType: "bigint");

            migrationBuilder.AlterColumn<int>(
                name: "InvoiceId",
                table: "Invoices",
                type: "int",
                nullable: false,
                oldClrType: typeof(long),
                oldType: "bigint")
                .Annotation("SqlServer:Identity", "1, 1")
                .OldAnnotation("SqlServer:Identity", "1, 1");

            migrationBuilder.AlterColumn<int>(
                name: "InvoiceId",
                table: "InvoiceDetails",
                type: "int",
                nullable: false,
                oldClrType: typeof(long),
                oldType: "bigint");

            migrationBuilder.AlterColumn<int>(
                name: "InvoiceDetailId",
                table: "InvoiceDetails",
                type: "int",
                nullable: false,
                oldClrType: typeof(long),
                oldType: "bigint")
                .Annotation("SqlServer:Identity", "1, 1")
                .OldAnnotation("SqlServer:Identity", "1, 1");

            migrationBuilder.AlterColumn<int>(
                name: "ReferenceId",
                table: "InventoryTransactions",
                type: "int",
                nullable: false,
                oldClrType: typeof(long),
                oldType: "bigint");

            migrationBuilder.AlterColumn<string>(
                name: "Reason",
                table: "InventoryTransactions",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(200)",
                oldMaxLength: 200);

            migrationBuilder.AlterColumn<int>(
                name: "ProductId",
                table: "InventoryTransactions",
                type: "int",
                nullable: false,
                oldClrType: typeof(long),
                oldType: "bigint");

            migrationBuilder.AlterColumn<int>(
                name: "EmployeeId",
                table: "InventoryTransactions",
                type: "int",
                nullable: true,
                oldClrType: typeof(long),
                oldType: "bigint",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedAt",
                table: "InventoryTransactions",
                type: "datetime2",
                nullable: false,
                oldClrType: typeof(DateTimeOffset),
                oldType: "datetimeoffset");

            migrationBuilder.AlterColumn<int>(
                name: "InvTransId",
                table: "InventoryTransactions",
                type: "int",
                nullable: false,
                oldClrType: typeof(long),
                oldType: "bigint")
                .Annotation("SqlServer:Identity", "1, 1")
                .OldAnnotation("SqlServer:Identity", "1, 1");

            migrationBuilder.AlterColumn<int>(
                name: "CustomerId",
                table: "GamingSessions",
                type: "int",
                nullable: false,
                oldClrType: typeof(long),
                oldType: "bigint");

            migrationBuilder.AlterColumn<int>(
                name: "ComputerId",
                table: "GamingSessions",
                type: "int",
                nullable: false,
                oldClrType: typeof(long),
                oldType: "bigint");

            migrationBuilder.AlterColumn<int>(
                name: "SessionId",
                table: "GamingSessions",
                type: "int",
                nullable: false,
                oldClrType: typeof(long),
                oldType: "bigint")
                .Annotation("SqlServer:Identity", "1, 1")
                .OldAnnotation("SqlServer:Identity", "1, 1");

            migrationBuilder.AlterColumn<int>(
                name: "RoleId",
                table: "Employees",
                type: "int",
                nullable: false,
                oldClrType: typeof(long),
                oldType: "bigint");

            migrationBuilder.AlterColumn<int>(
                name: "EmployeeId",
                table: "Employees",
                type: "int",
                nullable: false,
                oldClrType: typeof(long),
                oldType: "bigint")
                .Annotation("SqlServer:Identity", "1, 1")
                .OldAnnotation("SqlServer:Identity", "1, 1");

            migrationBuilder.AlterColumn<int>(
                name: "CustomerId",
                table: "Customers",
                type: "int",
                nullable: false,
                oldClrType: typeof(long),
                oldType: "bigint")
                .Annotation("SqlServer:Identity", "1, 1")
                .OldAnnotation("SqlServer:Identity", "1, 1");

            migrationBuilder.AlterColumn<int>(
                name: "ComputerTypeId",
                table: "ComputerTypes",
                type: "int",
                nullable: false,
                oldClrType: typeof(long),
                oldType: "bigint")
                .Annotation("SqlServer:Identity", "1, 1")
                .OldAnnotation("SqlServer:Identity", "1, 1");

            migrationBuilder.AlterColumn<int>(
                name: "RoomId",
                table: "Computers",
                type: "int",
                nullable: false,
                oldClrType: typeof(long),
                oldType: "bigint");

            migrationBuilder.AlterColumn<int>(
                name: "ComputerTypeId",
                table: "Computers",
                type: "int",
                nullable: false,
                oldClrType: typeof(long),
                oldType: "bigint");

            migrationBuilder.AlterColumn<int>(
                name: "ComputerId",
                table: "Computers",
                type: "int",
                nullable: false,
                oldClrType: typeof(long),
                oldType: "bigint")
                .Annotation("SqlServer:Identity", "1, 1")
                .OldAnnotation("SqlServer:Identity", "1, 1");

            migrationBuilder.AlterColumn<int>(
                name: "RoomId",
                table: "ComputerRooms",
                type: "int",
                nullable: false,
                oldClrType: typeof(long),
                oldType: "bigint")
                .Annotation("SqlServer:Identity", "1, 1")
                .OldAnnotation("SqlServer:Identity", "1, 1");

            migrationBuilder.AlterColumn<int>(
                name: "CategoryId",
                table: "Categories",
                type: "int",
                nullable: false,
                oldClrType: typeof(long),
                oldType: "bigint")
                .Annotation("SqlServer:Identity", "1, 1")
                .OldAnnotation("SqlServer:Identity", "1, 1");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Inventory_Type",
                table: "InventoryTransactions",
                sql: "([Type] IN ('In', 'Receive', 'Return')\r\n    AND [Quantity] > 0)\r\nOR\r\n([Type] IN ('Out', 'Sale') AND [Quantity] < 0)\r\nOR\r\n([Type] = 'Adjust' AND [Quantity] <> 0)");
            RestoreExistingFilteredIndexes(migrationBuilder);
            RestoreExistingKeys(migrationBuilder);
        }

        // Giữ nguyên tên và quan hệ từ InitialCreate; không xóa bảng hoặc dữ liệu.
        private static readonly (string Table, string Name, string Column)[] ExistingPrimaryKeys =
        [
            ("Categories", "PK_Categories", "CategoryId"),
            ("ComputerRooms", "PK_ComputerRooms", "RoomId"),
            ("ComputerTypes", "PK_ComputerTypes", "ComputerTypeId"),
            ("Customers", "PK_Customers", "CustomerId"),
            ("Promotions", "PK_Promotions", "PromotionId"),
            ("Roles", "PK_Roles", "RoleId"),
            ("Products", "PK_Products", "ProductId"),
            ("Computers", "PK_Computers", "ComputerId"),
            ("Employees", "PK_Employees", "EmployeeId"),
            ("GamingSessions", "PK_GamingSessions", "SessionId"),
            ("InventoryTransactions", "PK_InventoryTransactions", "InvTransId"),
            ("Invoices", "PK_Invoices", "InvoiceId"),
            ("InvoiceDetails", "PK_InvoiceDetails", "InvoiceDetailId"),
            ("Orders", "PK_Orders", "OrderId"),
            ("OrderDetails", "PK_OrderDetails", "OrderDetailId"),
            ("OrderItemCancelRequests", "PK_OrderItemCancelRequests", "RequestId"),
            ("OrderStatusHistories", "PK_OrderStatusHistories", "HistoryId"),
            ("WalletTransactions", "PK_WalletTransactions", "TransactionId"),
            ("Payments", "PK_Payments", "PaymentId")
        ];

        private static readonly (string Table, string Name, string Column, string PrincipalTable, string PrincipalColumn)[] ExistingForeignKeys =
        [
            ("Products", "FK_Products_Categories_CategoryId", "CategoryId", "Categories", "CategoryId"),
            ("Computers", "FK_Computers_ComputerRooms_RoomId", "RoomId", "ComputerRooms", "RoomId"),
            ("Computers", "FK_Computers_ComputerTypes_ComputerTypeId", "ComputerTypeId", "ComputerTypes", "ComputerTypeId"),
            ("Employees", "FK_Employees_Roles_RoleId", "RoleId", "Roles", "RoleId"),
            ("GamingSessions", "FK_GamingSessions_Computers_ComputerId", "ComputerId", "Computers", "ComputerId"),
            ("GamingSessions", "FK_GamingSessions_Customers_CustomerId", "CustomerId", "Customers", "CustomerId"),
            ("InventoryTransactions", "FK_InventoryTransactions_Employees_EmployeeId", "EmployeeId", "Employees", "EmployeeId"),
            ("InventoryTransactions", "FK_InventoryTransactions_Products_ProductId", "ProductId", "Products", "ProductId"),
            ("Invoices", "FK_Invoices_Customers_CustomerId", "CustomerId", "Customers", "CustomerId"),
            ("Invoices", "FK_Invoices_Employees_EmployeeId", "EmployeeId", "Employees", "EmployeeId"),
            ("Invoices", "FK_Invoices_GamingSessions_SessionId", "SessionId", "GamingSessions", "SessionId"),
            ("Invoices", "FK_Invoices_Promotions_PromotionId", "PromotionId", "Promotions", "PromotionId"),
            ("InvoiceDetails", "FK_InvoiceDetails_Invoices_InvoiceId", "InvoiceId", "Invoices", "InvoiceId"),
            ("Orders", "FK_Orders_Customers_CustomerId", "CustomerId", "Customers", "CustomerId"),
            ("Orders", "FK_Orders_Employees_EmployeeId", "EmployeeId", "Employees", "EmployeeId"),
            ("Orders", "FK_Orders_GamingSessions_SessionId", "SessionId", "GamingSessions", "SessionId"),
            ("Orders", "FK_Orders_Invoices_InvoiceId", "InvoiceId", "Invoices", "InvoiceId"),
            ("OrderDetails", "FK_OrderDetails_Orders_OrderId", "OrderId", "Orders", "OrderId"),
            ("OrderDetails", "FK_OrderDetails_Products_ProductId", "ProductId", "Products", "ProductId"),
            ("OrderItemCancelRequests", "FK_OrderItemCancelRequests_Customers_RequestedByCustomerId", "RequestedByCustomerId", "Customers", "CustomerId"),
            ("OrderItemCancelRequests", "FK_OrderItemCancelRequests_OrderDetails_OrderDetailId", "OrderDetailId", "OrderDetails", "OrderDetailId"),
            ("OrderStatusHistories", "FK_OrderStatusHistories_Employees_ChangedBy", "ChangedBy", "Employees", "EmployeeId"),
            ("OrderStatusHistories", "FK_OrderStatusHistories_OrderDetails_OrderDetailId", "OrderDetailId", "OrderDetails", "OrderDetailId"),
            ("OrderStatusHistories", "FK_OrderStatusHistories_Orders_OrderId", "OrderId", "Orders", "OrderId"),
            ("WalletTransactions", "FK_WalletTransactions_Customers_CustomerId", "CustomerId", "Customers", "CustomerId"),
            ("WalletTransactions", "FK_WalletTransactions_GamingSessions_SessionId", "SessionId", "GamingSessions", "SessionId"),
            ("WalletTransactions", "FK_WalletTransactions_OrderDetails_OrderDetailId", "OrderDetailId", "OrderDetails", "OrderDetailId"),
            ("WalletTransactions", "FK_WalletTransactions_Orders_OrderId", "OrderId", "Orders", "OrderId"),
            ("Payments", "FK_Payments_Invoices_InvoiceId", "InvoiceId", "Invoices", "InvoiceId"),
            ("Payments", "FK_Payments_WalletTransactions_WalletTransactionId", "WalletTransactionId", "WalletTransactions", "TransactionId")
        ];

        // Các SET option bắt buộc khi thay đổi bảng có filtered index.
        private static readonly (string Table, string Name, string Column, string Filter)[] ExistingFilteredIndexes =
        [
            ("GamingSessions", "IX_GamingSessions_ComputerId", "ComputerId", "[Status] = 'Active'"),
            ("GamingSessions", "IX_GamingSessions_CustomerId", "CustomerId", "[Status] = 'Active'"),
            ("OrderItemCancelRequests", "IX_OrderItemCancelRequests_OrderDetailId", "OrderDetailId", "[Status] = 'Pending'"),
            ("WalletTransactions", "IX_WalletTransactions_OrderDetailId", "OrderDetailId", "[OrderDetailId] IS NOT NULL AND [Type] = 'OrderItemRefund'"),
            ("WalletTransactions", "IX_WalletTransactions_OrderId", "OrderId", "[OrderId] IS NOT NULL AND [Type] = 'OrderDebit'")
        ];

        private static void DropExistingFilteredIndexes(MigrationBuilder migrationBuilder)
        {
            foreach (var index in ExistingFilteredIndexes)
                migrationBuilder.DropIndex(name: index.Name, table: index.Table);
        }

        private static void RestoreExistingFilteredIndexes(MigrationBuilder migrationBuilder)
        {
            foreach (var index in ExistingFilteredIndexes)
                migrationBuilder.CreateIndex(
                    name: index.Name, table: index.Table, column: index.Column,
                    unique: true, filter: index.Filter);
        }

        private static void ConfigureSqlSession(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                SET ANSI_NULLS ON;
                SET ANSI_PADDING ON;
                SET ANSI_WARNINGS ON;
                SET ARITHABORT ON;
                SET CONCAT_NULL_YIELDS_NULL ON;
                SET QUOTED_IDENTIFIER ON;
                SET NUMERIC_ROUNDABORT OFF;
                """);
        }
        private static void DropExistingKeys(MigrationBuilder migrationBuilder)
        {
            foreach (var key in ExistingForeignKeys)
                migrationBuilder.DropForeignKey(name: key.Name, table: key.Table);

            foreach (var key in ExistingPrimaryKeys)
                migrationBuilder.DropPrimaryKey(name: key.Name, table: key.Table);
        }

        private static void RestoreExistingKeys(MigrationBuilder migrationBuilder)
        {
            foreach (var key in ExistingPrimaryKeys)
                migrationBuilder.AddPrimaryKey(name: key.Name, table: key.Table, column: key.Column);

            foreach (var key in ExistingForeignKeys)
                migrationBuilder.AddForeignKey(
                    name: key.Name, table: key.Table, column: key.Column,
                    principalTable: key.PrincipalTable, principalColumn: key.PrincipalColumn,
                    onDelete: ReferentialAction.NoAction);
        }
    }
}
