using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CyberManagement.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddInvoiceIssuingSnapshots : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "EmployeeId",
                table: "Invoices",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AddColumn<string>(
                name: "ActorType",
                table: "Invoices",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: false,
                defaultValue: "Staff");

            migrationBuilder.AddColumn<decimal>(
                name: "CancelledAmount",
                table: "InvoiceDetails",
                type: "decimal(18,0)",
                precision: 18,
                scale: 0,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "VoucherCodeSnapshot",
                table: "InvoiceDetails",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VoucherDiscountTypeSnapshot",
                table: "InvoiceDetails",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "VoucherDiscountValueSnapshot",
                table: "InvoiceDetails",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Invoices_Actor",
                table: "Invoices",
                sql: "([ActorType] = 'Staff' AND [EmployeeId] IS NOT NULL) OR ([ActorType] = 'System' AND [EmployeeId] IS NULL AND [GamingSessionId] IS NOT NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_InvoiceDetails_Cancelled",
                table: "InvoiceDetails",
                sql: "[CancelledAmount] >= 0 AND [CancelledAmount] <= [Amount]");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Không xóa metadata mới hoặc gán nhân viên giả cho hóa đơn System.
            migrationBuilder.Sql("IF EXISTS (SELECT 1 FROM dbo.Invoices WHERE ActorType = 'System') OR EXISTS (SELECT 1 FROM dbo.InvoiceDetails WHERE CancelledAmount <> 0 OR VoucherCodeSnapshot IS NOT NULL OR VoucherDiscountTypeSnapshot IS NOT NULL OR VoucherDiscountValueSnapshot IS NOT NULL) THROW 51000, 'Invoice metadata exists; reconcile and archive it before downgrade.', 1;");
            migrationBuilder.DropCheckConstraint(
                name: "CK_Invoices_Actor",
                table: "Invoices");

            migrationBuilder.DropCheckConstraint(
                name: "CK_InvoiceDetails_Cancelled",
                table: "InvoiceDetails");

            migrationBuilder.DropColumn(
                name: "ActorType",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "CancelledAmount",
                table: "InvoiceDetails");

            migrationBuilder.DropColumn(
                name: "VoucherCodeSnapshot",
                table: "InvoiceDetails");

            migrationBuilder.DropColumn(
                name: "VoucherDiscountTypeSnapshot",
                table: "InvoiceDetails");

            migrationBuilder.DropColumn(
                name: "VoucherDiscountValueSnapshot",
                table: "InvoiceDetails");

            migrationBuilder.AlterColumn<int>(
                name: "EmployeeId",
                table: "Invoices",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);
        }
    }
}
