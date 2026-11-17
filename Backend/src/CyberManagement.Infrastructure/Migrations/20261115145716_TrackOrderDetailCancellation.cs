using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CyberManagement.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class TrackOrderDetailCancellation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "OrderDetails",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "Pending");

            migrationBuilder.AddCheckConstraint(
                name: "CK_OrderDetails_Status",
                table: "OrderDetails",
                sql: "[Status] IN ('Pending','Confirmed','Cancelled')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("IF EXISTS (SELECT 1 FROM dbo.OrderDetails WHERE Status <> 'Pending') THROW 51000, 'Order line status exists; reconcile before downgrade.', 1;");

            migrationBuilder.DropCheckConstraint(
                name: "CK_OrderDetails_Status",
                table: "OrderDetails");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "OrderDetails");
        }
    }
}
