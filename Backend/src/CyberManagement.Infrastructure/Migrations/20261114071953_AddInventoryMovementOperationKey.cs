using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CyberManagement.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddInventoryMovementOperationKey : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "OperationKey",
                table: "InventoryTransactions",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true,
                collation: "Latin1_General_100_BIN2");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryTransactions_OperationKey",
                table: "InventoryTransactions",
                column: "OperationKey",
                unique: true,
                filter: "[OperationKey] IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_InventoryTransactions_OperationKey",
                table: "InventoryTransactions",
                sql: "[OperationKey] IS NULL OR LEN(LTRIM(RTRIM([OperationKey]))) > 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_InventoryTransactions_OperationKey",
                table: "InventoryTransactions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_InventoryTransactions_OperationKey",
                table: "InventoryTransactions");

            migrationBuilder.DropColumn(
                name: "OperationKey",
                table: "InventoryTransactions");
        }
    }
}
