using System.Globalization;
using CyberManagement.Application.Exceptions;
using CyberManagement.Application.Wallets;
using CyberManagement.Domain.Entities;
using CyberManagement.Infrastructure.Persistance;
using Microsoft.EntityFrameworkCore;

namespace CyberManagement.Infrastructure.Wallets;

public sealed class SessionSettlement(ApplicationDbContext database, IWalletLedgerWriter wallet) : ISessionSettlement
{
    public async Task BeforeWalletDebitAsync(int customerId, DateTime nowUtc, CancellationToken cancellationToken)
    {
        if (nowUtc.Kind != DateTimeKind.Utc)
            throw new ArgumentException("Thời điểm quyết toán phải là UTC.", nameof(nowUtc));
        var customer = await wallet.LockCustomerAsync(customerId, cancellationToken);
        var candidates = await database.GamingSessions.FromSqlInterpolated(
                $"SELECT * FROM [GamingSessions] WITH (READCOMMITTEDLOCK) WHERE [CustomerId] = {customerId}")
            .AsNoTracking()
            .Where(row => row.CustomerId == customerId && (row.Status == "Active" || row.EndTime == null))
            .Select(row => new { row.GamingSessionId, row.ComputerId })
            .ToListAsync(cancellationToken);
        if (candidates.Count == 0) return;
        if (candidates.Count != 1)
            throw new BusinessRuleException("Khách có nhiều phiên chưa đóng; cần đối soát trước khi sử dụng ví.");

        var candidate = candidates[0];
        await database.Computers.FromSqlInterpolated(
                $"SELECT * FROM [Computers] WITH (UPDLOCK, HOLDLOCK) WHERE [ComputerId] = {candidate.ComputerId}")
            .SingleAsync(cancellationToken);
        var session = await database.GamingSessions.FromSqlInterpolated(
                $"SELECT * FROM [GamingSessions] WITH (UPDLOCK, HOLDLOCK) WHERE [GamingSessionId] = {candidate.GamingSessionId}")
            .SingleAsync(cancellationToken);
        if (session.CustomerId != customerId || session.Status != "Active" || session.EndTime is not null
            || session.PricePerHourSnapshot is not > 0 or > 999999999
            || decimal.Truncate(session.PricePerHourSnapshot.Value) != session.PricePerHourSnapshot.Value
            || session.StartTime > nowUtc)
            throw new BusinessRuleException("Phiên thiếu dữ liệu quyết toán hợp lệ; cần đối soát trước khi sử dụng ví.");

        var collected = await database.SessionCharges.Where(row => row.GamingSessionId == session.GamingSessionId)
            .SumAsync(row => (decimal?)row.Amount, cancellationToken) ?? 0;
        if (collected < 0 || collected != session.TotalCost || (collected > 0 && session.LastChargedAt is null)
            || session.LastChargedAt < session.StartTime || session.LastChargedAt > nowUtc)
            throw new BusinessRuleException("Phí phiên không khớp dữ liệu đã thu; cần đối soát trước khi sử dụng ví.");

        var seconds = (long)Math.Floor((nowUtc - session.StartTime).TotalSeconds);
        var through = session.StartTime.AddSeconds(seconds);
        if (session.LastChargedAt > through)
            throw new BusinessRuleException("Mốc quyết toán phiên không hợp lệ; cần đối soát.");
        var price = session.PricePerHourSnapshot.Value;
        var total = decimal.Ceiling(price * seconds / 3600m);
        var delta = total - collected;
        var affordableSeconds = decimal.Floor((collected + customer.WalletBalance) * 3600m / price);
        if (delta < 0)
            throw new BusinessRuleException("Phí phiên đã thu vượt tổng phí tính được; cần đối soát.");
        if (affordableSeconds <= seconds)
            throw new BusinessRuleException("Phiên cần được quyết toán và kết thúc trước khi sử dụng ví.");
        if (delta > 999999999)
            throw new BusinessRuleException("Khoản phí phiên vượt giới hạn giao dịch; cần đối soát.");

        if (delta > 0)
        {
            var charge = new SessionCharge
            {
                GamingSessionId = session.GamingSessionId, StartTime = session.LastChargedAt ?? session.StartTime,
                EndTime = through, Amount = delta
            };
            database.SessionCharges.Add(charge);
            await database.SaveChangesAsync(cancellationToken);
            var source = charge.ChargeId.ToString(CultureInfo.InvariantCulture);
            var operationKey = $"SessionCharge:{session.GamingSessionId}:{through.Ticks}";
            var debit = await wallet.RecordAsync(new WalletMutation(customerId, WalletMutationKind.Debit,
                delta, "GamingCharge", "SessionCharge", source, operationKey, new WalletActor("System"), nowUtc),
                cancellationToken);
            database.Payments.Add(new Payment
            {
                SessionCharge = charge, WalletTransaction = debit.Entry, Amount = delta,
                Method = "Wallet", Status = "Succeeded", OperationKey = operationKey,
                CreatedAt = nowUtc, PaymentTime = nowUtc, ConfirmedAt = nowUtc, PaymentTimeBasis = "Confirmation"
            });
        }
        session.TotalCost = total;
        session.LastChargedAt = through;
    }
}
