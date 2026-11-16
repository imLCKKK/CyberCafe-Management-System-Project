using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CyberManagement.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTournamentTransitionIdempotency : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TournamentTransitions",
                columns: table => new
                {
                    TournamentTransitionId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TournamentId = table.Column<int>(type: "int", nullable: false),
                    EmployeeId = table.Column<int>(type: "int", nullable: false),
                    RequestKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false, collation: "Latin1_General_100_BIN2"),
                    ToStatus = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ExpectedVersion = table.Column<string>(type: "nvarchar(12)", maxLength: 12, nullable: false),
                    ResponseJson = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TournamentTransitions", x => x.TournamentTransitionId);
                    table.ForeignKey(
                        name: "FK_TournamentTransitions_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "EmployeeId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TournamentTransitions_Tournaments_TournamentId",
                        column: x => x.TournamentId,
                        principalTable: "Tournaments",
                        principalColumn: "TournamentId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TournamentTransitions_EmployeeId",
                table: "TournamentTransitions",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_TournamentTransitions_TournamentId_EmployeeId_RequestKey",
                table: "TournamentTransitions",
                columns: new[] { "TournamentId", "EmployeeId", "RequestKey" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF EXISTS (SELECT 1 FROM [TournamentTransitions])
                    THROW 51000, 'Cannot remove persisted tournament transition receipts.', 1;
                """);
            migrationBuilder.DropTable(
                name: "TournamentTransitions");
        }
    }
}
