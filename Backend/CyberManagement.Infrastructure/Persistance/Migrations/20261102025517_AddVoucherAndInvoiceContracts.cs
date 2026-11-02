using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CyberManagement.Infrastructure.Persistance.Migrations
{
    /// <inheritdoc />
    public partial class AddVoucherAndInvoiceContracts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Orders_Invoices_InvoiceId",
                table: "Orders");

            migrationBuilder.DropIndex(
                name: "IX_Orders_InvoiceId",
                table: "Orders");

            migrationBuilder.DropIndex(
                name: "IX_Invoices_CustomerId_InvoiceDate",
                table: "Invoices");

            migrationBuilder.DropIndex(
                name: "IX_Invoices_SessionId",
                table: "Invoices");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Invoices_Amounts",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "InvoiceId",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "InvoiceDate",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "TotalAmount",
                table: "Invoices");

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "StartDate",
                table: "Promotions",
                type: "datetimeoffset",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "datetime2");

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "EndDate",
                table: "Promotions",
                type: "datetimeoffset",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "datetime2");

            migrationBuilder.AlterColumn<string>(
                name: "DiscountType",
                table: "Promotions",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(30)",
                oldMaxLength: 30);

            migrationBuilder.AddColumn<string>(
                name: "Code",
                table: "Promotions",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "",
                collation: "Latin1_General_100_CI_AS");

            migrationBuilder.AddColumn<decimal>(
                name: "MaxDiscountAmount",
                table: "Promotions",
                type: "decimal(18,0)",
                precision: 18,
                scale: 0,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "MinOrderAmount",
                table: "Promotions",
                type: "decimal(18,0)",
                precision: 18,
                scale: 0,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "PerCustomerUsageLimit",
                table: "Promotions",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TotalUsageLimit",
                table: "Promotions",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "DiscountAmount",
                table: "Orders",
                type: "decimal(18,0)",
                precision: 18,
                scale: 0,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "VoucherCodeSnapshot",
                table: "Orders",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VoucherDiscountTypeSnapshot",
                table: "Orders",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "VoucherDiscountValueSnapshot",
                table: "Orders",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "DiscountAmount",
                table: "OrderDetails",
                type: "decimal(18,0)",
                precision: 18,
                scale: 0,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "GrossAmount",
                table: "Invoices",
                type: "decimal(18,0)",
                precision: 18,
                scale: 0,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "IssuedAt",
                table: "Invoices",
                type: "datetimeoffset",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.AddColumn<long>(
                name: "OrderId",
                table: "Invoices",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PaidAmount",
                table: "Invoices",
                type: "decimal(18,0)",
                precision: 18,
                scale: 0,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "RefundAmount",
                table: "Invoices",
                type: "decimal(18,0)",
                precision: 18,
                scale: 0,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.CreateTable(
                name: "PromotionUsages",
                columns: table => new
                {
                    PromotionUsageId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PromotionId = table.Column<long>(type: "bigint", nullable: false),
                    OrderId = table.Column<long>(type: "bigint", nullable: false),
                    CustomerId = table.Column<long>(type: "bigint", nullable: false),
                    UsedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ReleasedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PromotionUsages", x => x.PromotionUsageId);
                    table.CheckConstraint("CK_PromotionUsages_ReleasedAt", "[ReleasedAt] IS NULL OR [ReleasedAt] >= [UsedAt]");
                    table.ForeignKey(
                        name: "FK_PromotionUsages_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "CustomerId");
                    table.ForeignKey(
                        name: "FK_PromotionUsages_Orders_OrderId",
                        column: x => x.OrderId,
                        principalTable: "Orders",
                        principalColumn: "OrderId");
                    table.ForeignKey(
                        name: "FK_PromotionUsages_Promotions_PromotionId",
                        column: x => x.PromotionId,
                        principalTable: "Promotions",
                        principalColumn: "PromotionId");
                });

            migrationBuilder.CreateIndex(
                name: "IX_Promotions_Code",
                table: "Promotions",
                column: "Code",
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Promotions_Amounts",
                table: "Promotions",
                sql: "[MinOrderAmount] >= 0\r\nAND (\r\n    [MaxDiscountAmount] IS NULL\r\n    OR [MaxDiscountAmount] > 0\r\n)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Promotions_Code",
                table: "Promotions",
                sql: "LEN(LTRIM(RTRIM([Code]))) > 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Promotions_Dates",
                table: "Promotions",
                sql: "[StartDate] < [EndDate]");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Promotions_Discount",
                table: "Promotions",
                sql: "([DiscountType] = 'Percent'\r\n    AND [DiscountValue] > 0\r\n    AND [DiscountValue] <= 100)\r\nOR\r\n([DiscountType] = 'Fixed'\r\n    AND [DiscountValue] > 0\r\n    AND [DiscountValue] = ROUND([DiscountValue], 0))");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Promotions_UsageLimits",
                table: "Promotions",
                sql: "([TotalUsageLimit] IS NULL OR [TotalUsageLimit] > 0)\r\nAND\r\n([PerCustomerUsageLimit] IS NULL\r\n    OR [PerCustomerUsageLimit] > 0)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Orders_DiscountAmount",
                table: "Orders",
                sql: "[DiscountAmount] >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Orders_VoucherSnapshot",
                table: "Orders",
                sql: "(\r\n    [VoucherCodeSnapshot] IS NULL\r\n    AND [VoucherDiscountTypeSnapshot] IS NULL\r\n    AND [VoucherDiscountValueSnapshot] IS NULL\r\n    AND [DiscountAmount] = 0\r\n)\r\nOR\r\n(\r\n    [VoucherCodeSnapshot] IS NOT NULL\r\n    AND LEN(LTRIM(RTRIM([VoucherCodeSnapshot]))) > 0\r\n    AND [VoucherDiscountTypeSnapshot] IS NOT NULL\r\n    AND [VoucherDiscountValueSnapshot] IS NOT NULL\r\n    AND\r\n    (\r\n        (\r\n            [VoucherDiscountTypeSnapshot] = 'Percent'\r\n            AND [VoucherDiscountValueSnapshot] > 0\r\n            AND [VoucherDiscountValueSnapshot] <= 100\r\n        )\r\n        OR\r\n        (\r\n            [VoucherDiscountTypeSnapshot] = 'Fixed'\r\n            AND [VoucherDiscountValueSnapshot] > 0\r\n            AND [VoucherDiscountValueSnapshot]\r\n                = ROUND([VoucherDiscountValueSnapshot], 0)\r\n        )\r\n    )\r\n)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_OrderDetails_Discount",
                table: "OrderDetails",
                sql: "[DiscountAmount] >= 0\r\nAND [DiscountAmount] <= [UnitPrice] * [Quantity]");

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_CustomerId_IssuedAt",
                table: "Invoices",
                columns: new[] { "CustomerId", "IssuedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_OrderId",
                table: "Invoices",
                column: "OrderId",
                unique: true,
                filter: "[OrderId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_SessionId",
                table: "Invoices",
                column: "SessionId",
                unique: true,
                filter: "[SessionId] IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Invoices_Amounts",
                table: "Invoices",
                sql: "[SubTotal] >= 0\r\nAND [Discount] >= 0\r\nAND [Discount] <= [SubTotal]\r\nAND [GrossAmount] = [SubTotal] - [Discount]\r\nAND [RefundAmount] >= 0\r\nAND [RefundAmount] <= [GrossAmount]\r\nAND [PaidAmount] >= 0\r\nAND [PaidAmount] <= [GrossAmount] - [RefundAmount]");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Invoices_Source",
                table: "Invoices",
                sql: "([OrderId] IS NOT NULL AND [SessionId] IS NULL)\r\nOR\r\n([OrderId] IS NULL AND [SessionId] IS NOT NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Invoices_Status",
                table: "Invoices",
                sql: "[Status] IN ('Pending', 'Paid', 'Cancelled')");

            migrationBuilder.CreateIndex(
                name: "IX_PromotionUsages_CustomerId",
                table: "PromotionUsages",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_PromotionUsages_OrderId",
                table: "PromotionUsages",
                column: "OrderId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PromotionUsages_PromotionId_CustomerId",
                table: "PromotionUsages",
                columns: new[] { "PromotionId", "CustomerId" },
                filter: "[ReleasedAt] IS NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_Invoices_Orders_OrderId",
                table: "Invoices",
                column: "OrderId",
                principalTable: "Orders",
                principalColumn: "OrderId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Invoices_Orders_OrderId",
                table: "Invoices");

            migrationBuilder.DropTable(
                name: "PromotionUsages");

            migrationBuilder.DropIndex(
                name: "IX_Promotions_Code",
                table: "Promotions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Promotions_Amounts",
                table: "Promotions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Promotions_Code",
                table: "Promotions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Promotions_Dates",
                table: "Promotions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Promotions_Discount",
                table: "Promotions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Promotions_UsageLimits",
                table: "Promotions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Orders_DiscountAmount",
                table: "Orders");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Orders_VoucherSnapshot",
                table: "Orders");

            migrationBuilder.DropCheckConstraint(
                name: "CK_OrderDetails_Discount",
                table: "OrderDetails");

            migrationBuilder.DropIndex(
                name: "IX_Invoices_CustomerId_IssuedAt",
                table: "Invoices");

            migrationBuilder.DropIndex(
                name: "IX_Invoices_OrderId",
                table: "Invoices");

            migrationBuilder.DropIndex(
                name: "IX_Invoices_SessionId",
                table: "Invoices");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Invoices_Amounts",
                table: "Invoices");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Invoices_Source",
                table: "Invoices");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Invoices_Status",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "Code",
                table: "Promotions");

            migrationBuilder.DropColumn(
                name: "MaxDiscountAmount",
                table: "Promotions");

            migrationBuilder.DropColumn(
                name: "MinOrderAmount",
                table: "Promotions");

            migrationBuilder.DropColumn(
                name: "PerCustomerUsageLimit",
                table: "Promotions");

            migrationBuilder.DropColumn(
                name: "TotalUsageLimit",
                table: "Promotions");

            migrationBuilder.DropColumn(
                name: "DiscountAmount",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "VoucherCodeSnapshot",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "VoucherDiscountTypeSnapshot",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "VoucherDiscountValueSnapshot",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "DiscountAmount",
                table: "OrderDetails");

            migrationBuilder.DropColumn(
                name: "GrossAmount",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "IssuedAt",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "OrderId",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "PaidAmount",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "RefundAmount",
                table: "Invoices");

            migrationBuilder.AlterColumn<DateTime>(
                name: "StartDate",
                table: "Promotions",
                type: "datetime2",
                nullable: false,
                oldClrType: typeof(DateTimeOffset),
                oldType: "datetimeoffset");

            migrationBuilder.AlterColumn<DateTime>(
                name: "EndDate",
                table: "Promotions",
                type: "datetime2",
                nullable: false,
                oldClrType: typeof(DateTimeOffset),
                oldType: "datetimeoffset");

            migrationBuilder.AlterColumn<string>(
                name: "DiscountType",
                table: "Promotions",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(10)",
                oldMaxLength: 10);

            migrationBuilder.AddColumn<long>(
                name: "InvoiceId",
                table: "Orders",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "InvoiceDate",
                table: "Invoices",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "TotalAmount",
                table: "Invoices",
                type: "decimal(18,0)",
                precision: 18,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.CreateIndex(
                name: "IX_Orders_InvoiceId",
                table: "Orders",
                column: "InvoiceId");

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_CustomerId_InvoiceDate",
                table: "Invoices",
                columns: new[] { "CustomerId", "InvoiceDate" });

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_SessionId",
                table: "Invoices",
                column: "SessionId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Invoices_Amounts",
                table: "Invoices",
                sql: "[SubTotal] >= 0\nAND [Discount] >= 0\nAND [Discount] <= [SubTotal]\nAND [TotalAmount] = [SubTotal] - [Discount]");

            migrationBuilder.AddForeignKey(
                name: "FK_Orders_Invoices_InvoiceId",
                table: "Orders",
                column: "InvoiceId",
                principalTable: "Invoices",
                principalColumn: "InvoiceId");
        }
    }
}
