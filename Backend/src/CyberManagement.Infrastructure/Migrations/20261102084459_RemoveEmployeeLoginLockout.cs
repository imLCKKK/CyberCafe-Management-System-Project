using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CyberManagement.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RemoveEmployeeLoginLockout : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FailedLoginCount",
                table: "Employees");

            migrationBuilder.DropColumn(
                name: "LockoutEnd",
                table: "Employees");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "FailedLoginCount",
                table: "Employees",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "LockoutEnd",
                table: "Employees",
                type: "datetime2",
                nullable: true);
        }
    }
}
