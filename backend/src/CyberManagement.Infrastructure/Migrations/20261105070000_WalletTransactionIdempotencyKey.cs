using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using CyberManagement.Infrastructure.Persistance;

#nullable disable

namespace CyberManagement.Infrastructure.Migrations
{
    /// <summary>
    /// S1-M-1: khóa chống xử lý lặp cho ví. Thêm cột nullable (giao dịch cũ không bị ảnh hưởng) và chỉ mục duy nhất
    /// có lọc theo (CustomerId, IdempotencyKey). Snapshot đã cập nhật; chạy `dotnet ef migrations add` sau này sẽ không sinh thay đổi thừa.
    /// </summary>
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20261105070000_WalletTransactionIdempotencyKey")]
    public partial class WalletTransactionIdempotencyKey : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "IdempotencyKey",
                table: "WalletTransactions",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "UX_WalletTransactions_Customer_IdempotencyKey",
                table: "WalletTransactions",
                columns: new[] { "CustomerId", "IdempotencyKey" },
                unique: true,
                filter: "[IdempotencyKey] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UX_WalletTransactions_Customer_IdempotencyKey",
                table: "WalletTransactions");

            migrationBuilder.DropColumn(
                name: "IdempotencyKey",
                table: "WalletTransactions");
        }
    }
}
