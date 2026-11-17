using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CyberManagement.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddEmployeeIdempotency : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CreateRequestKey",
                table: "Employees",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true,
                collation: "Latin1_General_100_BIN2");

            migrationBuilder.AddColumn<string>(
                name: "CreateRequestFingerprint",
                table: "Employees",
                type: "varchar(64)",
                unicode: false,
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Employees_CreateRequestKey",
                table: "Employees",
                column: "CreateRequestKey",
                unique: true,
                filter: "[CreateRequestKey] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Employees_CreateRequestKey",
                table: "Employees");

            migrationBuilder.DropColumn(
                name: "CreateRequestKey",
                table: "Employees");

            migrationBuilder.DropColumn(
                name: "CreateRequestFingerprint",
                table: "Employees");
        }
    }
}

