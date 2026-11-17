using CyberManagement.Domain.Entities;

namespace CyberManagement.Application.Wallets;

// Internal server integration point. The caller owns the SQL transaction, saves related
// sources/Payments with these tracked entities, commits once, and publishes after commit.
public interface IWalletLedgerWriter
{
    Task<Customer> LockCustomerAsync(int customerId, CancellationToken cancellationToken);
    Task<WalletMutationResult> RecordAsync(WalletMutation mutation, CancellationToken cancellationToken);
}
