using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CyberManagement.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ZaloPayGatewayIntents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ZaloPayIntents",
                columns: table => new
                {
                    IntentId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CustomerId = table.Column<int>(type: "int", nullable: false),
                    OrderId = table.Column<int>(type: "int", nullable: true),
                    Purpose = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false, collation: "Latin1_General_100_BIN2"),
                    OperationKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false, collation: "Latin1_General_100_BIN2"),
                    ExpectedAmountVnd = table.Column<decimal>(type: "decimal(18,0)", precision: 18, scale: 0, nullable: false),
                    ProviderAppId = table.Column<int>(type: "int", nullable: false),
                    MerchantTransactionId = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false, collation: "Latin1_General_100_BIN2"),
                    Status = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false, collation: "Latin1_General_100_BIN2"),
                    PaymentUrl = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ExpiresAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ProviderTransactionId = table.Column<string>(type: "nvarchar(19)", maxLength: 19, nullable: true, collation: "Latin1_General_100_BIN2"),
                    ProviderAmountVnd = table.Column<decimal>(type: "decimal(18,0)", precision: 18, scale: 0, nullable: true),
                    ProviderOccurredAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    VerifiedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    RequiresReconciliation = table.Column<bool>(type: "bit", nullable: false),
                    FailureReason = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    QueryLeaseToken = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    QueryLeaseUntilUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    NextQueryAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    LastQueryAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ZaloPayIntents", x => x.IntentId);
                    table.CheckConstraint("CK_ZaloPayIntents_ConfirmedAmount", "[Status] <> N'Succeeded' OR ([ProviderAmountVnd] IS NOT NULL AND [ProviderAmountVnd] = [ExpectedAmountVnd])");
                    table.CheckConstraint("CK_ZaloPayIntents_MerchantId", "LEN([MerchantTransactionId]) = 39 AND SUBSTRING([MerchantTransactionId],1,6) NOT LIKE '%[^0-9]%' AND SUBSTRING([MerchantTransactionId],7,1) = '_' AND SUBSTRING([MerchantTransactionId],8,32) NOT LIKE '%[^0-9a-f]%'");
                    table.CheckConstraint("CK_ZaloPayIntents_QueryLease", "([QueryLeaseToken] IS NULL AND [QueryLeaseUntilUtc] IS NULL) OR ([QueryLeaseToken] IS NOT NULL AND [QueryLeaseUntilUtc] IS NOT NULL)");
                    table.CheckConstraint("CK_ZaloPayIntents_Receipt", "([ProviderTransactionId] IS NULL AND [ProviderAmountVnd] IS NULL AND [ProviderOccurredAtUtc] IS NULL AND [VerifiedAtUtc] IS NULL AND [Status] <> N'Succeeded') OR ([ProviderTransactionId] IS NOT NULL AND TRY_CONVERT(bigint,[ProviderTransactionId]) IS NOT NULL AND TRY_CONVERT(bigint,[ProviderTransactionId]) > 0 AND CONVERT(varchar(19),TRY_CONVERT(bigint,[ProviderTransactionId])) = [ProviderTransactionId] AND [ProviderTransactionId] NOT LIKE '%[^0-9]%' AND [ProviderAmountVnd] IS NOT NULL AND [ProviderAmountVnd] > 0 AND [VerifiedAtUtc] IS NOT NULL AND ([Status] = N'Succeeded' OR [RequiresReconciliation] = 1))");
                    table.CheckConstraint("CK_ZaloPayIntents_Source", "([Purpose] = N'TopUp' AND [OrderId] IS NULL) OR ([Purpose] = N'Order' AND [OrderId] IS NOT NULL)");
                    table.CheckConstraint("CK_ZaloPayIntents_Values", "[CustomerId] > 0 AND [ProviderAppId] > 0 AND [ExpectedAmountVnd] BETWEEN 1 AND 999999999 AND [Status] IN (N'Pending',N'Succeeded',N'Failed',N'Expired') AND LEN([OperationKey]) > 0 AND [ExpiresAtUtc] > [CreatedAtUtc]");
                    table.ForeignKey(
                        name: "FK_ZaloPayIntents_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "CustomerId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ZaloPayIntents_Orders_OrderId",
                        column: x => x.OrderId,
                        principalTable: "Orders",
                        principalColumn: "OrderId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ZaloPayIntents_CustomerId",
                table: "ZaloPayIntents",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_ZaloPayIntents_MerchantTransactionId",
                table: "ZaloPayIntents",
                column: "MerchantTransactionId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ZaloPayIntents_OperationKey",
                table: "ZaloPayIntents",
                column: "OperationKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ZaloPayIntents_OrderId",
                table: "ZaloPayIntents",
                column: "OrderId",
                unique: true,
                filter: "[OrderId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ZaloPayIntents_ProviderAppId_ProviderTransactionId",
                table: "ZaloPayIntents",
                columns: new[] { "ProviderAppId", "ProviderTransactionId" },
                unique: true,
                filter: "[ProviderTransactionId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ZaloPayIntents_Status_NextQueryAtUtc",
                table: "ZaloPayIntents",
                columns: new[] { "Status", "NextQueryAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("IF EXISTS (SELECT 1 FROM [ZaloPayIntents]) THROW 51023, 'Cannot discard saved ZaloPay intents or receipts.', 1;");
            migrationBuilder.DropTable(
                name: "ZaloPayIntents");
        }
    }
}
