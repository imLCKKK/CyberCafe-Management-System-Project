using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CyberManagement.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class IntegrateInventoryInvoiceVoucherContracts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Chỉ nâng khi dữ liệu có thể chuyển an toàn; không tạo snapshot/khóa lịch sử giả.
            migrationBuilder.Sql(
                """
                SET ANSI_WARNINGS ON;
                SET ANSI_NULLS ON;
                SET QUOTED_IDENTIFIER ON;
                SET ANSI_PADDING ON;
                SET CONCAT_NULL_YIELDS_NULL ON;
                SET ARITHABORT ON;
                SET NUMERIC_ROUNDABORT OFF;
                IF EXISTS (SELECT 1 FROM [dbo].[WalletTransactions] WHERE [BalanceAfter] <> ROUND([BalanceAfter], 0)) THROW 51001, 'SCRUM13: fractional VND in WalletTransactions.BalanceAfter; reconcile before migration.', 1;
                IF EXISTS (SELECT 1 FROM [dbo].[WalletTransactions] WHERE [Amount] <> ROUND([Amount], 0)) THROW 51001, 'SCRUM13: fractional VND in WalletTransactions.Amount; reconcile before migration.', 1;
                IF EXISTS (SELECT 1 FROM [dbo].[StockReceipts] WHERE [TotalAmount] <> ROUND([TotalAmount], 0)) THROW 51001, 'SCRUM13: fractional VND in StockReceipts.TotalAmount; reconcile before migration.', 1;
                IF EXISTS (SELECT 1 FROM [dbo].[StockReceiptDetails] WHERE [UnitCost] <> ROUND([UnitCost], 0)) THROW 51001, 'SCRUM13: fractional VND in StockReceiptDetails.UnitCost; reconcile before migration.', 1;
                IF EXISTS (SELECT 1 FROM [dbo].[SessionCharges] WHERE [Amount] <> ROUND([Amount], 0)) THROW 51001, 'SCRUM13: fractional VND in SessionCharges.Amount; reconcile before migration.', 1;
                IF EXISTS (SELECT 1 FROM [dbo].[Products] WHERE [Price] <> ROUND([Price], 0)) THROW 51001, 'SCRUM13: fractional VND in Products.Price; reconcile before migration.', 1;
                IF EXISTS (SELECT 1 FROM [dbo].[Payments] WHERE [Amount] <> ROUND([Amount], 0)) THROW 51001, 'SCRUM13: fractional VND in Payments.Amount; reconcile before migration.', 1;
                IF EXISTS (SELECT 1 FROM [dbo].[Orders] WHERE [TotalAmount] <> ROUND([TotalAmount], 0)) THROW 51001, 'SCRUM13: fractional VND in Orders.TotalAmount; reconcile before migration.', 1;
                IF EXISTS (SELECT 1 FROM [dbo].[OrderDetails] WHERE [UnitPrice] <> ROUND([UnitPrice], 0)) THROW 51001, 'SCRUM13: fractional VND in OrderDetails.UnitPrice; reconcile before migration.', 1;
                IF EXISTS (SELECT 1 FROM [dbo].[Invoices] WHERE [TotalAmount] <> ROUND([TotalAmount], 0)) THROW 51001, 'SCRUM13: fractional VND in Invoices.TotalAmount; reconcile before migration.', 1;
                IF EXISTS (SELECT 1 FROM [dbo].[Invoices] WHERE [Subtotal] <> ROUND([Subtotal], 0)) THROW 51001, 'SCRUM13: fractional VND in Invoices.Subtotal; reconcile before migration.', 1;
                IF EXISTS (SELECT 1 FROM [dbo].[Invoices] WHERE [Discount] <> ROUND([Discount], 0)) THROW 51001, 'SCRUM13: fractional VND in Invoices.Discount; reconcile before migration.', 1;
                IF EXISTS (SELECT 1 FROM [dbo].[InvoiceDetails] WHERE [UnitPrice] <> ROUND([UnitPrice], 0)) THROW 51001, 'SCRUM13: fractional VND in InvoiceDetails.UnitPrice; reconcile before migration.', 1;
                IF EXISTS (SELECT 1 FROM [dbo].[InvoiceDetails] WHERE [Amount] <> ROUND([Amount], 0)) THROW 51001, 'SCRUM13: fractional VND in InvoiceDetails.Amount; reconcile before migration.', 1;
                IF EXISTS (SELECT 1 FROM [dbo].[GamingSessions] WHERE [TotalCost] <> ROUND([TotalCost], 0)) THROW 51001, 'SCRUM13: fractional VND in GamingSessions.TotalCost; reconcile before migration.', 1;
                IF EXISTS (SELECT 1 FROM [dbo].[Customers] WHERE [WalletBalance] <> ROUND([WalletBalance], 0)) THROW 51001, 'SCRUM13: fractional VND in Customers.WalletBalance; reconcile before migration.', 1;
                IF EXISTS (SELECT 1 FROM [dbo].[ComputerTypes] WHERE [PricePerHour] <> ROUND([PricePerHour], 0)) THROW 51001, 'SCRUM13: fractional VND in ComputerTypes.PricePerHour; reconcile before migration.', 1;
                IF EXISTS (SELECT 1 FROM [dbo].[InventoryTransactions]) THROW 51002, 'SCRUM13: InventoryTransactions has legacy rows requiring verified source/snapshot/time backfill; migration stopped without resetting data.', 1;
                IF EXISTS (SELECT 1 FROM [dbo].[Promotions]) THROW 51002, 'SCRUM13: Promotions has legacy rows requiring verified source/snapshot/time backfill; migration stopped without resetting data.', 1;
                IF EXISTS (SELECT 1 FROM [dbo].[Orders]) THROW 51002, 'SCRUM13: Orders has legacy rows requiring verified source/snapshot/time backfill; migration stopped without resetting data.', 1;
                IF EXISTS (SELECT 1 FROM [dbo].[OrderDetails]) THROW 51002, 'SCRUM13: OrderDetails has legacy rows requiring verified source/snapshot/time backfill; migration stopped without resetting data.', 1;
                IF EXISTS (SELECT 1 FROM [dbo].[Invoices]) THROW 51002, 'SCRUM13: Invoices has legacy rows requiring verified source/snapshot/time backfill; migration stopped without resetting data.', 1;
                IF EXISTS (SELECT 1 FROM [dbo].[InvoiceDetails]) THROW 51002, 'SCRUM13: InvoiceDetails has legacy rows requiring verified source/snapshot/time backfill; migration stopped without resetting data.', 1;
                IF EXISTS (SELECT 1 FROM [dbo].[Payments]) THROW 51002, 'SCRUM13: Payments has legacy rows requiring verified source/snapshot/time backfill; migration stopped without resetting data.', 1;
                """);

            migrationBuilder.DropForeignKey(
                name: "FK_InvoiceDetails_Invoices_InvoiceId",
                table: "InvoiceDetails");

            migrationBuilder.DropForeignKey(
                name: "FK_OrderDetails_Orders_OrderId",
                table: "OrderDetails");

            migrationBuilder.DropForeignKey(
                name: "FK_Payments_Invoices_InvoiceId",
                table: "Payments");

            migrationBuilder.DropForeignKey(
                name: "FK_Payments_WalletTransactions_WalletTransactionId",
                table: "Payments");

            migrationBuilder.DropIndex(
                name: "IX_Payments_WalletTransactionId",
                table: "Payments");

            migrationBuilder.DropIndex(
                name: "IX_InvoiceOrders_OrderId",
                table: "InvoiceOrders");

            migrationBuilder.AlterColumn<decimal>(
                name: "BalanceAfter",
                table: "WalletTransactions",
                type: "decimal(18,0)",
                precision: 18,
                scale: 0,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(12,2)",
                oldPrecision: 12,
                oldScale: 2);

            migrationBuilder.AlterColumn<decimal>(
                name: "Amount",
                table: "WalletTransactions",
                type: "decimal(18,0)",
                precision: 18,
                scale: 0,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(12,2)",
                oldPrecision: 12,
                oldScale: 2);

            migrationBuilder.AlterColumn<decimal>(
                name: "TotalAmount",
                table: "StockReceipts",
                type: "decimal(18,0)",
                precision: 18,
                scale: 0,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(12,2)",
                oldPrecision: 12,
                oldScale: 2);

            migrationBuilder.AlterColumn<decimal>(
                name: "UnitCost",
                table: "StockReceiptDetails",
                type: "decimal(18,0)",
                precision: 18,
                scale: 0,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(12,2)",
                oldPrecision: 12,
                oldScale: 2);

            migrationBuilder.AlterColumn<decimal>(
                name: "Amount",
                table: "SessionCharges",
                type: "decimal(18,0)",
                precision: 18,
                scale: 0,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(12,2)",
                oldPrecision: 12,
                oldScale: 2);

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

            migrationBuilder.AlterColumn<decimal>(
                name: "DiscountValue",
                table: "Promotions",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(12,2)",
                oldPrecision: 12,
                oldScale: 2);

            migrationBuilder.AlterColumn<string>(
                name: "DiscountType",
                table: "Promotions",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50);

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

            migrationBuilder.AlterColumn<decimal>(
                name: "Price",
                table: "Products",
                type: "decimal(18,0)",
                precision: 18,
                scale: 0,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(12,2)",
                oldPrecision: 12,
                oldScale: 2);

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "Payments",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50);

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "PaymentTime",
                table: "Payments",
                type: "datetimeoffset",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "datetime2");

            migrationBuilder.AlterColumn<string>(
                name: "Method",
                table: "Payments",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50);

            migrationBuilder.AlterColumn<int>(
                name: "InvoiceId",
                table: "Payments",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AlterColumn<decimal>(
                name: "Amount",
                table: "Payments",
                type: "decimal(18,0)",
                precision: 18,
                scale: 0,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(12,2)",
                oldPrecision: 12,
                oldScale: 2);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ConfirmedAt",
                table: "Payments",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CreatedAt",
                table: "Payments",
                type: "datetimeoffset",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.AddColumn<string>(
                name: "GatewayTransactionId",
                table: "Payments",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MerchantTransactionId",
                table: "Payments",
                type: "nvarchar(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OperationKey",
                table: "Payments",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "",
                collation: "Latin1_General_100_BIN2");

            migrationBuilder.AddColumn<int>(
                name: "OrderDetailId",
                table: "Payments",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "OrderId",
                table: "Payments",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PaymentTimeBasis",
                table: "Payments",
                type: "nvarchar(12)",
                maxLength: 12,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ProviderOccurredAt",
                table: "Payments",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Reason",
                table: "Payments",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RefundOfPaymentId",
                table: "Payments",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SessionChargeId",
                table: "Payments",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Type",
                table: "Payments",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AlterColumn<decimal>(
                name: "TotalAmount",
                table: "Orders",
                type: "decimal(18,0)",
                precision: 18,
                scale: 0,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(12,2)",
                oldPrecision: 12,
                oldScale: 2);

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

            migrationBuilder.AlterColumn<decimal>(
                name: "UnitPrice",
                table: "OrderDetails",
                type: "decimal(18,0)",
                precision: 18,
                scale: 0,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(12,2)",
                oldPrecision: 12,
                oldScale: 2);

            migrationBuilder.AddColumn<decimal>(
                name: "DiscountAmount",
                table: "OrderDetails",
                type: "decimal(18,0)",
                precision: 18,
                scale: 0,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "ProductNameSnapshot",
                table: "OrderDetails",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AlterColumn<decimal>(
                name: "TotalAmount",
                table: "Invoices",
                type: "decimal(18,0)",
                precision: 18,
                scale: 0,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(12,2)",
                oldPrecision: 12,
                oldScale: 2);

            migrationBuilder.AlterColumn<decimal>(
                name: "Subtotal",
                table: "Invoices",
                type: "decimal(18,0)",
                precision: 18,
                scale: 0,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(12,2)",
                oldPrecision: 12,
                oldScale: 2);

            migrationBuilder.AlterColumn<decimal>(
                name: "Discount",
                table: "Invoices",
                type: "decimal(18,0)",
                precision: 18,
                scale: 0,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(12,2)",
                oldPrecision: 12,
                oldScale: 2);

            migrationBuilder.AlterColumn<decimal>(
                name: "UnitPrice",
                table: "InvoiceDetails",
                type: "decimal(18,0)",
                precision: 18,
                scale: 0,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(12,2)",
                oldPrecision: 12,
                oldScale: 2);

            migrationBuilder.AlterColumn<decimal>(
                name: "Amount",
                table: "InvoiceDetails",
                type: "decimal(18,0)",
                precision: 18,
                scale: 0,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(12,2)",
                oldPrecision: 12,
                oldScale: 2);

            migrationBuilder.AddColumn<int>(
                name: "OrderDetailId",
                table: "InvoiceDetails",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SessionChargeId",
                table: "InvoiceDetails",
                type: "int",
                nullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "EmployeeId",
                table: "InventoryTransactions",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "CreatedAt",
                table: "InventoryTransactions",
                type: "datetimeoffset",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "datetime2");

            migrationBuilder.AddColumn<int>(
                name: "After",
                table: "InventoryTransactions",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Before",
                table: "InventoryTransactions",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "OrderDetailId",
                table: "InventoryTransactions",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Reason",
                table: "InventoryTransactions",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AlterColumn<decimal>(
                name: "TotalCost",
                table: "GamingSessions",
                type: "decimal(18,0)",
                precision: 18,
                scale: 0,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(12,2)",
                oldPrecision: 12,
                oldScale: 2);

            migrationBuilder.AlterColumn<decimal>(
                name: "WalletBalance",
                table: "Customers",
                type: "decimal(18,0)",
                precision: 18,
                scale: 0,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(12,2)",
                oldPrecision: 12,
                oldScale: 2);

            migrationBuilder.AlterColumn<decimal>(
                name: "PricePerHour",
                table: "ComputerTypes",
                type: "decimal(18,0)",
                precision: 18,
                scale: 0,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(12,2)",
                oldPrecision: 12,
                oldScale: 2);

            migrationBuilder.CreateTable(
                name: "InvoiceIssueRequests",
                columns: table => new
                {
                    OperationKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false, collation: "Latin1_General_100_BIN2"),
                    RequestHash = table.Column<string>(type: "char(64)", nullable: false, collation: "Latin1_General_100_BIN2"),
                    InvoiceId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InvoiceIssueRequests", x => x.OperationKey);
                    table.CheckConstraint("CK_InvoiceIssueRequests_Hash", "LEN([RequestHash]) = 64 AND [RequestHash] NOT LIKE '%[^0-9a-f]%' COLLATE Latin1_General_100_BIN2");
                    table.CheckConstraint("CK_InvoiceIssueRequests_Key", "LEN([OperationKey]) BETWEEN 1 AND 100 AND [OperationKey] NOT LIKE '%[^-A-Za-z0-9:_]%' COLLATE Latin1_General_100_BIN2");
                    table.ForeignKey(
                        name: "FK_InvoiceIssueRequests_Invoices_InvoiceId",
                        column: x => x.InvoiceId,
                        principalTable: "Invoices",
                        principalColumn: "InvoiceId");
                });

            migrationBuilder.CreateTable(
                name: "PromotionUsages",
                columns: table => new
                {
                    PromotionUsageId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PromotionId = table.Column<int>(type: "int", nullable: false),
                    OrderId = table.Column<int>(type: "int", nullable: false),
                    CustomerId = table.Column<int>(type: "int", nullable: false),
                    UsedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ReleasedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PromotionUsages", x => x.PromotionUsageId);
                    table.CheckConstraint("CK_PromotionUsages_Release", "[ReleasedAt] IS NULL OR [ReleasedAt] >= [UsedAt]");
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

            migrationBuilder.CreateTable(
                name: "RefundAttempts",
                columns: table => new
                {
                    RefundAttemptId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PaymentId = table.Column<int>(type: "int", nullable: false),
                    ProviderAppId = table.Column<int>(type: "int", nullable: false),
                    MerchantRefundId = table.Column<string>(type: "nvarchar(45)", maxLength: 45, nullable: false),
                    GatewayRefundId = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LastQueriedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    LastApi = table.Column<string>(type: "nvarchar(12)", maxLength: 12, nullable: true),
                    LastReturnCode = table.Column<int>(type: "int", nullable: true),
                    LastSubReturnCode = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RefundAttempts", x => x.RefundAttemptId);
                    table.CheckConstraint("CK_RefundAttempts_Values", "[ProviderAppId] > 0 AND LEN(LTRIM(RTRIM([MerchantRefundId]))) > 0 AND [Status] IN ('Created','Submitted','Processing','NotFound','Failed','Succeeded') AND ([LastApi] IS NULL OR [LastApi] IN ('Refund','QueryRefund'))");
                    table.ForeignKey(
                        name: "FK_RefundAttempts_Payments_PaymentId",
                        column: x => x.PaymentId,
                        principalTable: "Payments",
                        principalColumn: "PaymentId");
                });

            migrationBuilder.CreateIndex(
                name: "IX_Promotions_Code",
                table: "Promotions",
                column: "Code",
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Promotions_Code",
                table: "Promotions",
                sql: "LEN([Code]) BETWEEN 1 AND 50 AND [Code] COLLATE Latin1_General_100_BIN2 NOT LIKE '%[^-A-Z0-9_]%' COLLATE Latin1_General_100_BIN2");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Promotions_Dates",
                table: "Promotions",
                sql: "[StartDate] < [EndDate]");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Promotions_Discount",
                table: "Promotions",
                sql: "([DiscountType] = 'Percent' AND [DiscountValue] > 0 AND [DiscountValue] <= 100) OR ([DiscountType] = 'Fixed' AND [DiscountValue] > 0 AND [DiscountValue] = ROUND([DiscountValue], 0))");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Promotions_Limits",
                table: "Promotions",
                sql: "[MinOrderAmount] >= 0 AND ([MaxDiscountAmount] IS NULL OR [MaxDiscountAmount] > 0) AND ([TotalUsageLimit] IS NULL OR [TotalUsageLimit] > 0) AND ([PerCustomerUsageLimit] IS NULL OR [PerCustomerUsageLimit] > 0)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Products_Values",
                table: "Products",
                sql: "[Stock] >= 0 AND [Price] >= 0");

            migrationBuilder.CreateIndex(
                name: "IX_Payments_Method_GatewayTransactionId",
                table: "Payments",
                columns: new[] { "Method", "GatewayTransactionId" },
                unique: true,
                filter: "[GatewayTransactionId] IS NOT NULL AND [Type] = 'Payment'");

            migrationBuilder.CreateIndex(
                name: "IX_Payments_Method_MerchantTransactionId",
                table: "Payments",
                columns: new[] { "Method", "MerchantTransactionId" },
                unique: true,
                filter: "[MerchantTransactionId] IS NOT NULL AND [Type] = 'Payment'");

            migrationBuilder.CreateIndex(
                name: "IX_Payments_OperationKey",
                table: "Payments",
                column: "OperationKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Payments_OrderDetailId",
                table: "Payments",
                column: "OrderDetailId");

            migrationBuilder.CreateIndex(
                name: "IX_Payments_OrderId",
                table: "Payments",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "IX_Payments_RefundOfPaymentId_Status",
                table: "Payments",
                columns: new[] { "RefundOfPaymentId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Payments_SessionChargeId",
                table: "Payments",
                column: "SessionChargeId");

            migrationBuilder.CreateIndex(
                name: "IX_Payments_WalletTransactionId",
                table: "Payments",
                column: "WalletTransactionId",
                unique: true,
                filter: "[WalletTransactionId] IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Payments_Method",
                table: "Payments",
                sql: "([Method] <> 'Wallet' OR [Status] <> 'Succeeded' OR [WalletTransactionId] IS NOT NULL) AND ([Method] <> 'ZaloPay' OR [Type] <> 'Payment' OR ([MerchantTransactionId] IS NOT NULL AND LEN(LTRIM(RTRIM([MerchantTransactionId]))) > 0 AND ([Status] <> 'Succeeded' OR ([GatewayTransactionId] IS NOT NULL AND LEN(LTRIM(RTRIM([GatewayTransactionId]))) > 0))))");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Payments_Source",
                table: "Payments",
                sql: "([OrderId] IS NOT NULL AND [SessionChargeId] IS NULL) OR ([OrderId] IS NULL AND [SessionChargeId] IS NOT NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Payments_Time",
                table: "Payments",
                sql: "([Status] = 'Succeeded' AND [PaymentTime] IS NOT NULL AND [ConfirmedAt] IS NOT NULL AND [PaymentTimeBasis] IS NOT NULL AND ([PaymentTimeBasis] = 'Source' OR ([PaymentTimeBasis] = 'Confirmation' AND [PaymentTime] = [ConfirmedAt]))) OR ([Status] <> 'Succeeded' AND [PaymentTime] IS NULL AND [ConfirmedAt] IS NULL AND [PaymentTimeBasis] IS NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Payments_Type",
                table: "Payments",
                sql: "([Type] = 'Payment' AND [RefundOfPaymentId] IS NULL AND [OrderDetailId] IS NULL) OR ([Type] = 'Refund' AND [RefundOfPaymentId] IS NOT NULL AND [RefundOfPaymentId] <> [PaymentId] AND [Reason] IS NOT NULL AND LEN(LTRIM(RTRIM([Reason]))) > 0 AND [Status] <> 'Expired')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Payments_Values",
                table: "Payments",
                sql: "[Amount] > 0 AND [Method] IN ('Wallet','Cash','ZaloPay') AND [Status] IN ('Pending','Succeeded','Failed','Expired') AND LEN(LTRIM(RTRIM([OperationKey]))) > 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Orders_Amounts",
                table: "Orders",
                sql: "[TotalAmount] >= 0 AND [DiscountAmount] >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Orders_VoucherSnapshot",
                table: "Orders",
                sql: "([VoucherCodeSnapshot] IS NULL AND [VoucherDiscountTypeSnapshot] IS NULL AND [VoucherDiscountValueSnapshot] IS NULL AND [DiscountAmount] = 0) OR ([VoucherCodeSnapshot] IS NOT NULL AND LEN(LTRIM(RTRIM([VoucherCodeSnapshot]))) > 0 AND [VoucherDiscountTypeSnapshot] IS NOT NULL AND [VoucherDiscountValueSnapshot] IS NOT NULL AND (([VoucherDiscountTypeSnapshot] = 'Percent' AND [VoucherDiscountValueSnapshot] > 0 AND [VoucherDiscountValueSnapshot] <= 100) OR ([VoucherDiscountTypeSnapshot] = 'Fixed' AND [VoucherDiscountValueSnapshot] > 0 AND [VoucherDiscountValueSnapshot] = ROUND([VoucherDiscountValueSnapshot], 0))))");

            migrationBuilder.AddCheckConstraint(
                name: "CK_OrderDetails_NameSnapshot",
                table: "OrderDetails",
                sql: "LEN(LTRIM(RTRIM([ProductNameSnapshot]))) > 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_OrderDetails_Values",
                table: "OrderDetails",
                sql: "[Quantity] > 0 AND [UnitPrice] >= 0 AND [DiscountAmount] >= 0 AND [DiscountAmount] <= [UnitPrice] * [Quantity]");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Invoices_Amounts",
                table: "Invoices",
                sql: "[Subtotal] >= 0 AND [Discount] >= 0 AND [Discount] <= [Subtotal] AND [TotalAmount] = [Subtotal] - [Discount]");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Invoices_Status",
                table: "Invoices",
                sql: "[Status] IN ('Unpaid','Paid','Cancelled')");

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceOrders_OrderId",
                table: "InvoiceOrders",
                column: "OrderId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceDetails_OrderDetailId",
                table: "InvoiceDetails",
                column: "OrderDetailId",
                unique: true,
                filter: "[OrderDetailId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceDetails_SessionChargeId",
                table: "InvoiceDetails",
                column: "SessionChargeId",
                unique: true,
                filter: "[SessionChargeId] IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_InvoiceDetails_Source",
                table: "InvoiceDetails",
                sql: "([OrderDetailId] IS NOT NULL AND [SessionChargeId] IS NULL) OR ([OrderDetailId] IS NULL AND [SessionChargeId] IS NOT NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_InvoiceDetails_Values",
                table: "InvoiceDetails",
                sql: "[Quantity] > 0 AND [UnitPrice] >= 0 AND [Amount] >= 0 AND [Amount] <= [UnitPrice] * [Quantity]");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryTransactions_OrderDetailId_Type",
                table: "InventoryTransactions",
                columns: new[] { "OrderDetailId", "Type" },
                unique: true,
                filter: "[OrderDetailId] IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_InventoryTransactions_Reason",
                table: "InventoryTransactions",
                sql: "LEN(LTRIM(RTRIM([Reason]))) BETWEEN 1 AND 200");

            migrationBuilder.AddCheckConstraint(
                name: "CK_InventoryTransactions_Type",
                table: "InventoryTransactions",
                sql: "(([Type] IN ('Receive','Return') AND [Quantity] > 0) OR ([Type] = 'Sale' AND [Quantity] < 0) OR ([Type] = 'Adjust' AND [Quantity] <> 0)) AND (([Type] IN ('Receive','Adjust') AND [OrderDetailId] IS NULL) OR ([Type] IN ('Sale','Return') AND [OrderDetailId] IS NOT NULL AND [OrderId] IS NOT NULL))");

            migrationBuilder.AddCheckConstraint(
                name: "CK_InventoryTransactions_Values",
                table: "InventoryTransactions",
                sql: "[Before] >= 0 AND [After] >= 0 AND CAST([Before] AS bigint) + [Quantity] = [After]");

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceIssueRequests_InvoiceId",
                table: "InvoiceIssueRequests",
                column: "InvoiceId",
                unique: true);

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

            migrationBuilder.CreateIndex(
                name: "IX_RefundAttempts_PaymentId",
                table: "RefundAttempts",
                column: "PaymentId");

            migrationBuilder.CreateIndex(
                name: "IX_RefundAttempts_ProviderAppId_GatewayRefundId",
                table: "RefundAttempts",
                columns: new[] { "ProviderAppId", "GatewayRefundId" },
                unique: true,
                filter: "[GatewayRefundId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_RefundAttempts_ProviderAppId_MerchantRefundId",
                table: "RefundAttempts",
                columns: new[] { "ProviderAppId", "MerchantRefundId" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_InventoryTransactions_OrderDetails_OrderDetailId",
                table: "InventoryTransactions",
                column: "OrderDetailId",
                principalTable: "OrderDetails",
                principalColumn: "OrderDetailId");

            migrationBuilder.AddForeignKey(
                name: "FK_InvoiceDetails_Invoices_InvoiceId",
                table: "InvoiceDetails",
                column: "InvoiceId",
                principalTable: "Invoices",
                principalColumn: "InvoiceId");

            migrationBuilder.AddForeignKey(
                name: "FK_InvoiceDetails_OrderDetails_OrderDetailId",
                table: "InvoiceDetails",
                column: "OrderDetailId",
                principalTable: "OrderDetails",
                principalColumn: "OrderDetailId");

            migrationBuilder.AddForeignKey(
                name: "FK_InvoiceDetails_SessionCharges_SessionChargeId",
                table: "InvoiceDetails",
                column: "SessionChargeId",
                principalTable: "SessionCharges",
                principalColumn: "ChargeId");

            migrationBuilder.AddForeignKey(
                name: "FK_OrderDetails_Orders_OrderId",
                table: "OrderDetails",
                column: "OrderId",
                principalTable: "Orders",
                principalColumn: "OrderId");

            migrationBuilder.AddForeignKey(
                name: "FK_Payments_Invoices_InvoiceId",
                table: "Payments",
                column: "InvoiceId",
                principalTable: "Invoices",
                principalColumn: "InvoiceId");

            migrationBuilder.AddForeignKey(
                name: "FK_Payments_OrderDetails_OrderDetailId",
                table: "Payments",
                column: "OrderDetailId",
                principalTable: "OrderDetails",
                principalColumn: "OrderDetailId");

            migrationBuilder.AddForeignKey(
                name: "FK_Payments_Orders_OrderId",
                table: "Payments",
                column: "OrderId",
                principalTable: "Orders",
                principalColumn: "OrderId");

            migrationBuilder.AddForeignKey(
                name: "FK_Payments_Payments_RefundOfPaymentId",
                table: "Payments",
                column: "RefundOfPaymentId",
                principalTable: "Payments",
                principalColumn: "PaymentId");

            migrationBuilder.AddForeignKey(
                name: "FK_Payments_SessionCharges_SessionChargeId",
                table: "Payments",
                column: "SessionChargeId",
                principalTable: "SessionCharges",
                principalColumn: "ChargeId");

            migrationBuilder.AddForeignKey(
                name: "FK_Payments_WalletTransactions_WalletTransactionId",
                table: "Payments",
                column: "WalletTransactionId",
                principalTable: "WalletTransactions",
                principalColumn: "TransactionId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                IF EXISTS (SELECT 1 FROM [dbo].[PromotionUsages]) THROW 51003, 'SCRUM13: downgrade would discard contract data in PromotionUsages; restore a verified backup instead.', 1;
                IF EXISTS (SELECT 1 FROM [dbo].[RefundAttempts]) THROW 51003, 'SCRUM13: downgrade would discard contract data in RefundAttempts; restore a verified backup instead.', 1;
                IF EXISTS (SELECT 1 FROM [dbo].[InvoiceIssueRequests]) THROW 51003, 'SCRUM13: downgrade would discard contract data in InvoiceIssueRequests; restore a verified backup instead.', 1;
                IF EXISTS (SELECT 1 FROM [dbo].[InventoryTransactions]) THROW 51003, 'SCRUM13: downgrade would discard contract data in InventoryTransactions; restore a verified backup instead.', 1;
                IF EXISTS (SELECT 1 FROM [dbo].[Promotions]) THROW 51003, 'SCRUM13: downgrade would discard contract data in Promotions; restore a verified backup instead.', 1;
                IF EXISTS (SELECT 1 FROM [dbo].[Orders]) THROW 51003, 'SCRUM13: downgrade would discard contract data in Orders; restore a verified backup instead.', 1;
                IF EXISTS (SELECT 1 FROM [dbo].[OrderDetails]) THROW 51003, 'SCRUM13: downgrade would discard contract data in OrderDetails; restore a verified backup instead.', 1;
                IF EXISTS (SELECT 1 FROM [dbo].[Invoices]) THROW 51003, 'SCRUM13: downgrade would discard contract data in Invoices; restore a verified backup instead.', 1;
                IF EXISTS (SELECT 1 FROM [dbo].[InvoiceDetails]) THROW 51003, 'SCRUM13: downgrade would discard contract data in InvoiceDetails; restore a verified backup instead.', 1;
                IF EXISTS (SELECT 1 FROM [dbo].[Payments]) THROW 51003, 'SCRUM13: downgrade would discard contract data in Payments; restore a verified backup instead.', 1;
                IF EXISTS (SELECT 1 FROM [dbo].[WalletTransactions] WHERE ABS([BalanceAfter]) > 9999999999.99) THROW 51003, 'SCRUM13: WalletTransactions.BalanceAfter exceeds the old precision; downgrade stopped.', 1;
                IF EXISTS (SELECT 1 FROM [dbo].[WalletTransactions] WHERE ABS([Amount]) > 9999999999.99) THROW 51003, 'SCRUM13: WalletTransactions.Amount exceeds the old precision; downgrade stopped.', 1;
                IF EXISTS (SELECT 1 FROM [dbo].[StockReceipts] WHERE ABS([TotalAmount]) > 9999999999.99) THROW 51003, 'SCRUM13: StockReceipts.TotalAmount exceeds the old precision; downgrade stopped.', 1;
                IF EXISTS (SELECT 1 FROM [dbo].[StockReceiptDetails] WHERE ABS([UnitCost]) > 9999999999.99) THROW 51003, 'SCRUM13: StockReceiptDetails.UnitCost exceeds the old precision; downgrade stopped.', 1;
                IF EXISTS (SELECT 1 FROM [dbo].[SessionCharges] WHERE ABS([Amount]) > 9999999999.99) THROW 51003, 'SCRUM13: SessionCharges.Amount exceeds the old precision; downgrade stopped.', 1;
                IF EXISTS (SELECT 1 FROM [dbo].[Products] WHERE ABS([Price]) > 9999999999.99) THROW 51003, 'SCRUM13: Products.Price exceeds the old precision; downgrade stopped.', 1;
                IF EXISTS (SELECT 1 FROM [dbo].[Payments] WHERE ABS([Amount]) > 9999999999.99) THROW 51003, 'SCRUM13: Payments.Amount exceeds the old precision; downgrade stopped.', 1;
                IF EXISTS (SELECT 1 FROM [dbo].[Orders] WHERE ABS([TotalAmount]) > 9999999999.99) THROW 51003, 'SCRUM13: Orders.TotalAmount exceeds the old precision; downgrade stopped.', 1;
                IF EXISTS (SELECT 1 FROM [dbo].[OrderDetails] WHERE ABS([UnitPrice]) > 9999999999.99) THROW 51003, 'SCRUM13: OrderDetails.UnitPrice exceeds the old precision; downgrade stopped.', 1;
                IF EXISTS (SELECT 1 FROM [dbo].[Invoices] WHERE ABS([TotalAmount]) > 9999999999.99) THROW 51003, 'SCRUM13: Invoices.TotalAmount exceeds the old precision; downgrade stopped.', 1;
                IF EXISTS (SELECT 1 FROM [dbo].[Invoices] WHERE ABS([Subtotal]) > 9999999999.99) THROW 51003, 'SCRUM13: Invoices.Subtotal exceeds the old precision; downgrade stopped.', 1;
                IF EXISTS (SELECT 1 FROM [dbo].[Invoices] WHERE ABS([Discount]) > 9999999999.99) THROW 51003, 'SCRUM13: Invoices.Discount exceeds the old precision; downgrade stopped.', 1;
                IF EXISTS (SELECT 1 FROM [dbo].[InvoiceDetails] WHERE ABS([UnitPrice]) > 9999999999.99) THROW 51003, 'SCRUM13: InvoiceDetails.UnitPrice exceeds the old precision; downgrade stopped.', 1;
                IF EXISTS (SELECT 1 FROM [dbo].[InvoiceDetails] WHERE ABS([Amount]) > 9999999999.99) THROW 51003, 'SCRUM13: InvoiceDetails.Amount exceeds the old precision; downgrade stopped.', 1;
                IF EXISTS (SELECT 1 FROM [dbo].[GamingSessions] WHERE ABS([TotalCost]) > 9999999999.99) THROW 51003, 'SCRUM13: GamingSessions.TotalCost exceeds the old precision; downgrade stopped.', 1;
                IF EXISTS (SELECT 1 FROM [dbo].[Customers] WHERE ABS([WalletBalance]) > 9999999999.99) THROW 51003, 'SCRUM13: Customers.WalletBalance exceeds the old precision; downgrade stopped.', 1;
                IF EXISTS (SELECT 1 FROM [dbo].[ComputerTypes] WHERE ABS([PricePerHour]) > 9999999999.99) THROW 51003, 'SCRUM13: ComputerTypes.PricePerHour exceeds the old precision; downgrade stopped.', 1;
                """);

            migrationBuilder.DropForeignKey(
                name: "FK_InventoryTransactions_OrderDetails_OrderDetailId",
                table: "InventoryTransactions");

            migrationBuilder.DropForeignKey(
                name: "FK_InvoiceDetails_Invoices_InvoiceId",
                table: "InvoiceDetails");

            migrationBuilder.DropForeignKey(
                name: "FK_InvoiceDetails_OrderDetails_OrderDetailId",
                table: "InvoiceDetails");

            migrationBuilder.DropForeignKey(
                name: "FK_InvoiceDetails_SessionCharges_SessionChargeId",
                table: "InvoiceDetails");

            migrationBuilder.DropForeignKey(
                name: "FK_OrderDetails_Orders_OrderId",
                table: "OrderDetails");

            migrationBuilder.DropForeignKey(
                name: "FK_Payments_Invoices_InvoiceId",
                table: "Payments");

            migrationBuilder.DropForeignKey(
                name: "FK_Payments_OrderDetails_OrderDetailId",
                table: "Payments");

            migrationBuilder.DropForeignKey(
                name: "FK_Payments_Orders_OrderId",
                table: "Payments");

            migrationBuilder.DropForeignKey(
                name: "FK_Payments_Payments_RefundOfPaymentId",
                table: "Payments");

            migrationBuilder.DropForeignKey(
                name: "FK_Payments_SessionCharges_SessionChargeId",
                table: "Payments");

            migrationBuilder.DropForeignKey(
                name: "FK_Payments_WalletTransactions_WalletTransactionId",
                table: "Payments");

            migrationBuilder.DropTable(
                name: "InvoiceIssueRequests");

            migrationBuilder.DropTable(
                name: "PromotionUsages");

            migrationBuilder.DropTable(
                name: "RefundAttempts");

            migrationBuilder.DropIndex(
                name: "IX_Promotions_Code",
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
                name: "CK_Promotions_Limits",
                table: "Promotions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Products_Values",
                table: "Products");

            migrationBuilder.DropIndex(
                name: "IX_Payments_Method_GatewayTransactionId",
                table: "Payments");

            migrationBuilder.DropIndex(
                name: "IX_Payments_Method_MerchantTransactionId",
                table: "Payments");

            migrationBuilder.DropIndex(
                name: "IX_Payments_OperationKey",
                table: "Payments");

            migrationBuilder.DropIndex(
                name: "IX_Payments_OrderDetailId",
                table: "Payments");

            migrationBuilder.DropIndex(
                name: "IX_Payments_OrderId",
                table: "Payments");

            migrationBuilder.DropIndex(
                name: "IX_Payments_RefundOfPaymentId_Status",
                table: "Payments");

            migrationBuilder.DropIndex(
                name: "IX_Payments_SessionChargeId",
                table: "Payments");

            migrationBuilder.DropIndex(
                name: "IX_Payments_WalletTransactionId",
                table: "Payments");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Payments_Method",
                table: "Payments");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Payments_Source",
                table: "Payments");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Payments_Time",
                table: "Payments");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Payments_Type",
                table: "Payments");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Payments_Values",
                table: "Payments");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Orders_Amounts",
                table: "Orders");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Orders_VoucherSnapshot",
                table: "Orders");

            migrationBuilder.DropCheckConstraint(
                name: "CK_OrderDetails_NameSnapshot",
                table: "OrderDetails");

            migrationBuilder.DropCheckConstraint(
                name: "CK_OrderDetails_Values",
                table: "OrderDetails");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Invoices_Amounts",
                table: "Invoices");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Invoices_Status",
                table: "Invoices");

            migrationBuilder.DropIndex(
                name: "IX_InvoiceOrders_OrderId",
                table: "InvoiceOrders");

            migrationBuilder.DropIndex(
                name: "IX_InvoiceDetails_OrderDetailId",
                table: "InvoiceDetails");

            migrationBuilder.DropIndex(
                name: "IX_InvoiceDetails_SessionChargeId",
                table: "InvoiceDetails");

            migrationBuilder.DropCheckConstraint(
                name: "CK_InvoiceDetails_Source",
                table: "InvoiceDetails");

            migrationBuilder.DropCheckConstraint(
                name: "CK_InvoiceDetails_Values",
                table: "InvoiceDetails");

            migrationBuilder.DropIndex(
                name: "IX_InventoryTransactions_OrderDetailId_Type",
                table: "InventoryTransactions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_InventoryTransactions_Reason",
                table: "InventoryTransactions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_InventoryTransactions_Type",
                table: "InventoryTransactions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_InventoryTransactions_Values",
                table: "InventoryTransactions");

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
                name: "ConfirmedAt",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "GatewayTransactionId",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "MerchantTransactionId",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "OperationKey",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "OrderDetailId",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "OrderId",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "PaymentTimeBasis",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "ProviderOccurredAt",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "Reason",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "RefundOfPaymentId",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "SessionChargeId",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "Type",
                table: "Payments");

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
                name: "ProductNameSnapshot",
                table: "OrderDetails");

            migrationBuilder.DropColumn(
                name: "OrderDetailId",
                table: "InvoiceDetails");

            migrationBuilder.DropColumn(
                name: "SessionChargeId",
                table: "InvoiceDetails");

            migrationBuilder.DropColumn(
                name: "After",
                table: "InventoryTransactions");

            migrationBuilder.DropColumn(
                name: "Before",
                table: "InventoryTransactions");

            migrationBuilder.DropColumn(
                name: "OrderDetailId",
                table: "InventoryTransactions");

            migrationBuilder.DropColumn(
                name: "Reason",
                table: "InventoryTransactions");

            migrationBuilder.AlterColumn<decimal>(
                name: "BalanceAfter",
                table: "WalletTransactions",
                type: "decimal(12,2)",
                precision: 12,
                scale: 2,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,0)",
                oldPrecision: 18,
                oldScale: 0);

            migrationBuilder.AlterColumn<decimal>(
                name: "Amount",
                table: "WalletTransactions",
                type: "decimal(12,2)",
                precision: 12,
                scale: 2,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,0)",
                oldPrecision: 18,
                oldScale: 0);

            migrationBuilder.AlterColumn<decimal>(
                name: "TotalAmount",
                table: "StockReceipts",
                type: "decimal(12,2)",
                precision: 12,
                scale: 2,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,0)",
                oldPrecision: 18,
                oldScale: 0);

            migrationBuilder.AlterColumn<decimal>(
                name: "UnitCost",
                table: "StockReceiptDetails",
                type: "decimal(12,2)",
                precision: 12,
                scale: 2,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,0)",
                oldPrecision: 18,
                oldScale: 0);

            migrationBuilder.AlterColumn<decimal>(
                name: "Amount",
                table: "SessionCharges",
                type: "decimal(12,2)",
                precision: 12,
                scale: 2,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,0)",
                oldPrecision: 18,
                oldScale: 0);

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

            migrationBuilder.AlterColumn<decimal>(
                name: "DiscountValue",
                table: "Promotions",
                type: "decimal(12,2)",
                precision: 12,
                scale: 2,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,2)",
                oldPrecision: 18,
                oldScale: 2);

            migrationBuilder.AlterColumn<string>(
                name: "DiscountType",
                table: "Promotions",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(10)",
                oldMaxLength: 10);

            migrationBuilder.AlterColumn<decimal>(
                name: "Price",
                table: "Products",
                type: "decimal(12,2)",
                precision: 12,
                scale: 2,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,0)",
                oldPrecision: 18,
                oldScale: 0);

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "Payments",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(10)",
                oldMaxLength: 10);

            migrationBuilder.AlterColumn<DateTime>(
                name: "PaymentTime",
                table: "Payments",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified),
                oldClrType: typeof(DateTimeOffset),
                oldType: "datetimeoffset",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Method",
                table: "Payments",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(10)",
                oldMaxLength: 10);

            migrationBuilder.AlterColumn<int>(
                name: "InvoiceId",
                table: "Payments",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "Amount",
                table: "Payments",
                type: "decimal(12,2)",
                precision: 12,
                scale: 2,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,0)",
                oldPrecision: 18,
                oldScale: 0);

            migrationBuilder.AlterColumn<decimal>(
                name: "TotalAmount",
                table: "Orders",
                type: "decimal(12,2)",
                precision: 12,
                scale: 2,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,0)",
                oldPrecision: 18,
                oldScale: 0);

            migrationBuilder.AlterColumn<decimal>(
                name: "UnitPrice",
                table: "OrderDetails",
                type: "decimal(12,2)",
                precision: 12,
                scale: 2,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,0)",
                oldPrecision: 18,
                oldScale: 0);

            migrationBuilder.AlterColumn<decimal>(
                name: "TotalAmount",
                table: "Invoices",
                type: "decimal(12,2)",
                precision: 12,
                scale: 2,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,0)",
                oldPrecision: 18,
                oldScale: 0);

            migrationBuilder.AlterColumn<decimal>(
                name: "Subtotal",
                table: "Invoices",
                type: "decimal(12,2)",
                precision: 12,
                scale: 2,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,0)",
                oldPrecision: 18,
                oldScale: 0);

            migrationBuilder.AlterColumn<decimal>(
                name: "Discount",
                table: "Invoices",
                type: "decimal(12,2)",
                precision: 12,
                scale: 2,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,0)",
                oldPrecision: 18,
                oldScale: 0);

            migrationBuilder.AlterColumn<decimal>(
                name: "UnitPrice",
                table: "InvoiceDetails",
                type: "decimal(12,2)",
                precision: 12,
                scale: 2,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,0)",
                oldPrecision: 18,
                oldScale: 0);

            migrationBuilder.AlterColumn<decimal>(
                name: "Amount",
                table: "InvoiceDetails",
                type: "decimal(12,2)",
                precision: 12,
                scale: 2,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,0)",
                oldPrecision: 18,
                oldScale: 0);

            migrationBuilder.AlterColumn<int>(
                name: "EmployeeId",
                table: "InventoryTransactions",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedAt",
                table: "InventoryTransactions",
                type: "datetime2",
                nullable: false,
                oldClrType: typeof(DateTimeOffset),
                oldType: "datetimeoffset");

            migrationBuilder.AlterColumn<decimal>(
                name: "TotalCost",
                table: "GamingSessions",
                type: "decimal(12,2)",
                precision: 12,
                scale: 2,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,0)",
                oldPrecision: 18,
                oldScale: 0);

            migrationBuilder.AlterColumn<decimal>(
                name: "WalletBalance",
                table: "Customers",
                type: "decimal(12,2)",
                precision: 12,
                scale: 2,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,0)",
                oldPrecision: 18,
                oldScale: 0);

            migrationBuilder.AlterColumn<decimal>(
                name: "PricePerHour",
                table: "ComputerTypes",
                type: "decimal(12,2)",
                precision: 12,
                scale: 2,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,0)",
                oldPrecision: 18,
                oldScale: 0);

            migrationBuilder.CreateIndex(
                name: "IX_Payments_WalletTransactionId",
                table: "Payments",
                column: "WalletTransactionId");

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceOrders_OrderId",
                table: "InvoiceOrders",
                column: "OrderId");

            migrationBuilder.AddForeignKey(
                name: "FK_InvoiceDetails_Invoices_InvoiceId",
                table: "InvoiceDetails",
                column: "InvoiceId",
                principalTable: "Invoices",
                principalColumn: "InvoiceId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_OrderDetails_Orders_OrderId",
                table: "OrderDetails",
                column: "OrderId",
                principalTable: "Orders",
                principalColumn: "OrderId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Payments_Invoices_InvoiceId",
                table: "Payments",
                column: "InvoiceId",
                principalTable: "Invoices",
                principalColumn: "InvoiceId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Payments_WalletTransactions_WalletTransactionId",
                table: "Payments",
                column: "WalletTransactionId",
                principalTable: "WalletTransactions",
                principalColumn: "TransactionId",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
