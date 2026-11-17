using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CyberManagement.Infrastructure.Migrations;

public partial class ComputerSessionsBilling : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            IF EXISTS (SELECT ComputerId FROM ComputerMaintenance WHERE EndTime IS NULL GROUP BY ComputerId HAVING COUNT(*) > 1)
                THROW 51000, 'Reconcile duplicate open maintenance records before migration.', 1;
            """);
        // PrepareSessionBilling remains immutable; extend its recorded metadata.
        migrationBuilder.AddColumn<string>("ClosedByActorType", "GamingSessions", type: "nvarchar(20)", maxLength: 20, nullable: true);
        migrationBuilder.AddColumn<int>("ClosingInvoiceId", "GamingSessions", type: "int", nullable: true);
        migrationBuilder.DropIndex("IX_ComputerMaintenance_ComputerId", "ComputerMaintenance");
        migrationBuilder.CreateIndex("IX_ComputerMaintenance_ComputerId", "ComputerMaintenance", "ComputerId", unique: true, filter: "[EndTime] IS NULL");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            IF EXISTS (SELECT 1 FROM GamingSessions WHERE ClosedByActorType IS NOT NULL OR ClosingInvoiceId IS NOT NULL)
                THROW 51000, 'Cannot discard recorded session actors or closing results.', 1;
            """);
        migrationBuilder.DropColumn("ClosedByActorType", "GamingSessions");
        migrationBuilder.DropColumn("ClosingInvoiceId", "GamingSessions");
        migrationBuilder.DropIndex("IX_ComputerMaintenance_ComputerId", "ComputerMaintenance");
        migrationBuilder.CreateIndex("IX_ComputerMaintenance_ComputerId", "ComputerMaintenance", "ComputerId");
    }
}
