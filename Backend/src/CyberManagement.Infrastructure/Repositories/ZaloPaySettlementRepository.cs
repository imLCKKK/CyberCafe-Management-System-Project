using System.Globalization;
using CyberManagement.Application.DTOs.Payments;
using CyberManagement.Application.Exceptions;
using CyberManagement.Application.Interfaces.Repositories;
using CyberManagement.Application.Interfaces.Services;
using CyberManagement.Application.Wallets;
using CyberManagement.Domain.Entities;
using CyberManagement.Infrastructure.Payments;
using CyberManagement.Infrastructure.Persistance;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace CyberManagement.Infrastructure.Repositories;

public sealed class ZaloPaySettlementRepository(ApplicationDbContext database, IWalletLedgerWriter wallet,
    IZaloPayOrderPaymentBridge orders) : IZaloPaySettlementRepository
{
    public async Task<ZaloPaySettlementResult> ApplyAsync(int intentId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        if (intentId <= 0) throw new System.ComponentModel.DataAnnotations.ValidationException("Mã giao dịch không hợp lệ.");
        RequireCleanUnitOfWork();
        try
        {
            await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
            var (customer, _, intent) = await ZaloPaySettlementLocks.LoadAsync(database, wallet, intentId, cancellationToken);
            ZaloPaySettlementResult result;
            if (intent.AppliedAtUtc is not null) result = new(intent, false);
            else if (!ZaloPaySettlementLocks.VerifiedReceipt(intent)) result = new(intent, false);
            else if (now < intent.VerifiedAtUtc) result = ZaloPaySettlementLocks.Review(intent, "InvalidConfirmationTime");
            else if (intent.RequiresReconciliation)
            {
                intent.SettlementFailureReason ??= intent.FailureReason ?? "ManualReconciliationRequired";
                result = new(intent, false);
            }
            else if (intent.Purpose == "Order") result = await orders.ConfirmAsync(intentId, now, cancellationToken);
            else if (intent.Purpose != "TopUp" || intent.OrderId is not null)
                result = ZaloPaySettlementLocks.Review(intent, "SourceMismatch");
            else result = await ApplyTopUpAsync(customer, intent, now, cancellationToken);
            if (database.ChangeTracker.HasChanges()) await database.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return result;
        }
        catch (DbUpdateException error) when (error.InnerException is SqlException { Number: 2601 or 2627 })
        {
            // The failed unit of work has rolled back completely. Re-read before flagging review.
            database.ChangeTracker.Clear();
            await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
            var (_, _, intent) = await ZaloPaySettlementLocks.LoadAsync(database, wallet, intentId, cancellationToken);
            var result = intent.AppliedAtUtc is null
                ? ZaloPaySettlementLocks.Review(intent, "SettlementConflict") : new ZaloPaySettlementResult(intent, false);
            if (database.ChangeTracker.HasChanges()) await database.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return result;
        }
    }

    private async Task<ZaloPaySettlementResult> ApplyTopUpAsync(Customer customer, ZaloPayIntent intent,
        DateTimeOffset now, CancellationToken cancellationToken)
    {
        if (customer.Status != "Active") return ZaloPaySettlementLocks.Review(intent, "CustomerLocked");
        // Recovery may run after the QR expires; the durable provider confirmation determines lateness.
        if (intent.VerifiedAtUtc >= intent.ExpiresAtUtc) return ZaloPaySettlementLocks.Review(intent, "LatePayment");
        if (customer.WalletBalance < 0 || customer.WalletBalance + intent.ExpectedAmountVnd > 999_999_999_999_999_999m)
            return ZaloPaySettlementLocks.Review(intent, "BalanceLimit");
        if (await ZaloPaySettlementLocks.HasGatewayPaymentConflictAsync(database, intent, null, cancellationToken))
            return ZaloPaySettlementLocks.Review(intent, "GatewayTransactionConflict");
        try
        {
            var credited = await wallet.RecordAsync(new WalletMutation(intent.CustomerId, WalletMutationKind.Credit,
                intent.ExpectedAmountVnd, "TopUp", "ZaloPayTopUp", intent.MerchantTransactionId,
                "ZaloPayTopUp:" + intent.MerchantTransactionId, new WalletActor("System"), now,
                Description: "Nạp ZaloPay Sandbox #" + intent.IntentId.ToString(CultureInfo.InvariantCulture)), cancellationToken);
            intent.WalletTransaction = credited.Entry;
            intent.AppliedAtUtc = now;
            intent.SettlementFailureReason = null;
            return new(intent, !credited.Replayed);
        }
        catch (BusinessRuleException)
        {
            return ZaloPaySettlementLocks.Review(intent, "LedgerConflict");
        }
    }

    public async Task<IReadOnlyList<int>> GetDueIdsAsync(DateTimeOffset now, int take, CancellationToken cancellationToken)
    {
        if (take <= 0) return [];
        return await database.Set<ZaloPayIntent>().AsNoTracking()
            .Where(row => row.Status == "Succeeded" && row.AppliedAtUtc == null && !row.RequiresReconciliation)
            .OrderBy(row => row.SettlementFailureReason == "CheckoutRegistrationMissing" ? 1 : 0)
            .ThenBy(row => row.VerifiedAtUtc).ThenBy(row => row.IntentId)
            .Take(Math.Min(take, 5)).Select(row => row.IntentId).ToListAsync(cancellationToken);
    }

    private void RequireCleanUnitOfWork()
    {
        if (!database.Database.IsSqlServer() || database.Database.CurrentTransaction is not null)
            throw new InvalidOperationException("Gateway settlement repository owns an independent SQL Server transaction.");
        if (database.ChangeTracker.HasChanges())
            throw new InvalidOperationException("Save or discard caller changes before applying a gateway receipt.");
        database.ChangeTracker.Clear();
    }
}
