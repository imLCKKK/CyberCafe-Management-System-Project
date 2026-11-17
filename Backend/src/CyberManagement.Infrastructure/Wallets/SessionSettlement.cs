using CyberManagement.Application.Interfaces.Repositories;
using CyberManagement.Application.Wallets;

namespace CyberManagement.Infrastructure.Wallets;

public sealed class SessionSettlement(ISessionRepository sessions) : ISessionSettlement
{
    public Task BeforeWalletDebitAsync(int customerId, DateTime nowUtc, CancellationToken cancellationToken) =>
        sessions.SettleInTransactionAsync(customerId, nowUtc, cancellationToken);
}
