using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CyberManagement.Infrastructure.Migrations;

public partial class RegistrationStatsSnapshots : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "RegistrationStatsSnapshots",
            columns: table => new
            {
                SnapshotId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                RegistrationId = table.Column<int>(type: "int", nullable: false),
                RiotId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                FetchedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                CanonicalJson = table.Column<string>(type: "nvarchar(max)", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_RegistrationStatsSnapshots", x => x.SnapshotId);
                table.ForeignKey("FK_RegistrationStatsSnapshots_TournamentRegistrations_RegistrationId",
                    x => x.RegistrationId, "TournamentRegistrations", "RegistrationId", onDelete: ReferentialAction.Restrict);
            });
        migrationBuilder.CreateIndex(name: "IX_RegistrationStatsSnapshots_RegistrationId_RiotId_FetchedAt",
            table: "RegistrationStatsSnapshots", columns: new[] { "RegistrationId", "RiotId", "FetchedAt" }, unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder) =>
        migrationBuilder.DropTable(name: "RegistrationStatsSnapshots");
}
