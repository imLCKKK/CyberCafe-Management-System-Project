namespace CyberManagement.Application.Wallets;

public interface ISessionSettlement
{
    // A settlement that closes the session must reject this debit, even with a positive
    // residual balance. Caller rolls back its whole transaction; the worker bills separately.
    Task BeforeWalletDebitAsync(int customerId, DateTime nowUtc, CancellationToken cancellationToken);
}
