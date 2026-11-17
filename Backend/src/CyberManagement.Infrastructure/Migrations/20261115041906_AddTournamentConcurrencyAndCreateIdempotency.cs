using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CyberManagement.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTournamentConcurrencyAndCreateIdempotency : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Tournaments_CreatedBy",
                table: "Tournaments");

            migrationBuilder.AddColumn<string>(
                name: "CreateRequestFingerprint",
                table: "Tournaments",
                type: "varchar(64)",
                unicode: false,
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreateRequestKey",
                table: "Tournaments",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true,
                collation: "Latin1_General_100_BIN2");

            migrationBuilder.AddColumn<byte[]>(
                name: "Version",
                table: "Tournaments",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.CreateIndex(
                name: "IX_Tournaments_CreatedBy_CreateRequestKey",
                table: "Tournaments",
                columns: new[] { "CreatedBy", "CreateRequestKey" },
                unique: true,
                filter: "[CreateRequestKey] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Tournaments_CreatedBy_CreateRequestKey",
                table: "Tournaments");

            migrationBuilder.DropColumn(
                name: "CreateRequestFingerprint",
                table: "Tournaments");

            migrationBuilder.DropColumn(
                name: "CreateRequestKey",
                table: "Tournaments");

            migrationBuilder.DropColumn(
                name: "Version",
                table: "Tournaments");

            migrationBuilder.CreateIndex(
                name: "IX_Tournaments_CreatedBy",
                table: "Tournaments",
                column: "CreatedBy");
        }
    }
}
