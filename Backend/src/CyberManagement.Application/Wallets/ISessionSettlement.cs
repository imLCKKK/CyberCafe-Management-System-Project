namespace CyberManagement.Application.Wallets;

public interface ISessionSettlement
{
    Task BeforeWalletDebitAsync(int customerId, DateTime nowUtc, CancellationToken cancellationToken);
}
