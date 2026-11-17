using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CyberManagement.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ZaloPayReceiptSettlement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "AppliedAtUtc",
                table: "ZaloPayIntents",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PaymentId",
                table: "ZaloPayIntents",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SettlementFailureReason",
                table: "ZaloPayIntents",
                type: "nvarchar(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "WalletTransactionId",
                table: "ZaloPayIntents",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "GatewayReservationExpiresAtUtc",
                table: "Payments",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ZaloPayIntentId",
                table: "Payments",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ZaloPayIntents_PaymentId",
                table: "ZaloPayIntents",
                column: "PaymentId",
                unique: true,
                filter: "[PaymentId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ZaloPayIntents_Status_AppliedAtUtc",
                table: "ZaloPayIntents",
                columns: new[] { "Status", "AppliedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ZaloPayIntents_WalletTransactionId",
                table: "ZaloPayIntents",
                column: "WalletTransactionId",
                unique: true,
                filter: "[WalletTransactionId] IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ZaloPayIntents_Applied",
                table: "ZaloPayIntents",
                sql: "([AppliedAtUtc] IS NULL AND [WalletTransactionId] IS NULL AND [PaymentId] IS NULL) OR ([AppliedAtUtc] IS NOT NULL AND [VerifiedAtUtc] IS NOT NULL AND [AppliedAtUtc] >= [VerifiedAtUtc] AND [Status] = N'Succeeded' AND [ProviderAmountVnd] IS NOT NULL AND [ProviderAmountVnd] = [ExpectedAmountVnd] AND (([Purpose] = N'TopUp' AND [WalletTransactionId] IS NOT NULL AND [PaymentId] IS NULL) OR ([Purpose] = N'Order' AND [PaymentId] IS NOT NULL AND [WalletTransactionId] IS NULL)))");

            migrationBuilder.CreateIndex(
                name: "IX_Payments_ZaloPayIntentId",
                table: "Payments",
                column: "ZaloPayIntentId",
                unique: true,
                filter: "[ZaloPayIntentId] IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Payments_ZaloPayIntent",
                table: "Payments",
                sql: "([ZaloPayIntentId] IS NULL AND [GatewayReservationExpiresAtUtc] IS NULL) OR ([ZaloPayIntentId] IS NOT NULL AND [GatewayReservationExpiresAtUtc] IS NOT NULL AND [Method] = 'ZaloPay' AND [Type] = 'Payment' AND [OrderId] IS NOT NULL AND [SessionChargeId] IS NULL)");

            migrationBuilder.AddForeignKey(
                name: "FK_Payments_ZaloPayIntents_ZaloPayIntentId",
                table: "Payments",
                column: "ZaloPayIntentId",
                principalTable: "ZaloPayIntents",
                principalColumn: "IntentId");

            migrationBuilder.AddForeignKey(
                name: "FK_ZaloPayIntents_Payments_PaymentId",
                table: "ZaloPayIntents",
                column: "PaymentId",
                principalTable: "Payments",
                principalColumn: "PaymentId");

            migrationBuilder.AddForeignKey(
                name: "FK_ZaloPayIntents_WalletTransactions_WalletTransactionId",
                table: "ZaloPayIntents",
                column: "WalletTransactionId",
                principalTable: "WalletTransactions",
                principalColumn: "TransactionId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF EXISTS (SELECT 1 FROM [ZaloPayIntents] WHERE [AppliedAtUtc] IS NOT NULL
                    OR [WalletTransactionId] IS NOT NULL OR [PaymentId] IS NOT NULL OR [SettlementFailureReason] IS NOT NULL)
                    OR EXISTS (SELECT 1 FROM [Payments] WHERE [ZaloPayIntentId] IS NOT NULL)
                    THROW 51023, 'Cannot remove ZaloPay settlement links while receipts or checkout registrations exist.', 1;
                """);

            migrationBuilder.DropForeignKey(
                name: "FK_Payments_ZaloPayIntents_ZaloPayIntentId",
                table: "Payments");

            migrationBuilder.DropForeignKey(
                name: "FK_ZaloPayIntents_Payments_PaymentId",
                table: "ZaloPayIntents");

            migrationBuilder.DropForeignKey(
                name: "FK_ZaloPayIntents_WalletTransactions_WalletTransactionId",
                table: "ZaloPayIntents");

            migrationBuilder.DropIndex(
                name: "IX_ZaloPayIntents_PaymentId",
                table: "ZaloPayIntents");

            migrationBuilder.DropIndex(
                name: "IX_ZaloPayIntents_Status_AppliedAtUtc",
                table: "ZaloPayIntents");

            migrationBuilder.DropIndex(
                name: "IX_ZaloPayIntents_WalletTransactionId",
                table: "ZaloPayIntents");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ZaloPayIntents_Applied",
                table: "ZaloPayIntents");

            migrationBuilder.DropIndex(
                name: "IX_Payments_ZaloPayIntentId",
                table: "Payments");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Payments_ZaloPayIntent",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "AppliedAtUtc",
                table: "ZaloPayIntents");

            migrationBuilder.DropColumn(
                name: "PaymentId",
                table: "ZaloPayIntents");

            migrationBuilder.DropColumn(
                name: "SettlementFailureReason",
                table: "ZaloPayIntents");

            migrationBuilder.DropColumn(
                name: "WalletTransactionId",
                table: "ZaloPayIntents");

            migrationBuilder.DropColumn(
                name: "GatewayReservationExpiresAtUtc",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "ZaloPayIntentId",
                table: "Payments");
        }
    }
}
