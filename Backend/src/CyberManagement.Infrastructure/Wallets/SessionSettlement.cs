using CyberManagement.Application.Exceptions;
using CyberManagement.Application.Interfaces.Repositories;
using CyberManagement.Application.Wallets;

namespace CyberManagement.Infrastructure.Wallets;

public sealed class SessionSettlement(ISessionRepository sessions) : ISessionSettlement
{
    public async Task BeforeWalletDebitAsync(int customerId, DateTime nowUtc, CancellationToken cancellationToken)
    {
        var result = await sessions.SettleInTransactionAsync(customerId, nowUtc, cancellationToken);
        if (result is not null && (result.Status != "Active" || result.EndTime is not null))
            throw new BusinessRuleException("Phiên phải kết thúc trước khi sử dụng ví. Giao dịch chi ví cần được hoàn tác.");
    }
}
