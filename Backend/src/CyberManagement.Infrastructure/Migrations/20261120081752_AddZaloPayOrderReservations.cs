using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CyberManagement.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddZaloPayOrderReservations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ApprovedAtUtc",
                table: "Payments",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ApprovedByEmployeeId",
                table: "Payments",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ReservedUntilUtc",
                table: "Orders",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Payments_ApprovedByEmployeeId",
                table: "Payments",
                column: "ApprovedByEmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_Orders_Status_ReservedUntilUtc",
                table: "Orders",
                columns: new[] { "Status", "ReservedUntilUtc" });

            migrationBuilder.AddForeignKey(
                name: "FK_Payments_Employees_ApprovedByEmployeeId",
                table: "Payments",
                column: "ApprovedByEmployeeId",
                principalTable: "Employees",
                principalColumn: "EmployeeId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("IF EXISTS (SELECT 1 FROM [Orders] WHERE [ReservedUntilUtc] IS NOT NULL) OR EXISTS (SELECT 1 FROM [Payments] WHERE [ApprovedAtUtc] IS NOT NULL OR [ApprovedByEmployeeId] IS NOT NULL) THROW 51000, 'Cannot discard durable checkout reservation or refund approval history.', 1;");
            migrationBuilder.DropForeignKey(
                name: "FK_Payments_Employees_ApprovedByEmployeeId",
                table: "Payments");

            migrationBuilder.DropIndex(
                name: "IX_Payments_ApprovedByEmployeeId",
                table: "Payments");

            migrationBuilder.DropIndex(
                name: "IX_Orders_Status_ReservedUntilUtc",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "ApprovedAtUtc",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "ApprovedByEmployeeId",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "ReservedUntilUtc",
                table: "Orders");
        }
    }
}
