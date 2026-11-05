using CyberManagement.Application.DTOs.Customers;
using CyberManagement.Application.Exceptions;
using CyberManagement.Application.Interfaces.Repositories;
using CyberManagement.Application.Interfaces.Services;

namespace CyberManagement.Application.Services;

public sealed class ClientAccountService(ICustomerRepository repository) : IClientAccountService
{
    private const int TransactionListLimit = 200;

    public async Task<CustomerDto> GetProfileAsync(int customerId, CancellationToken cancellationToken) =>
        await repository.FindDtoAsync(customerId, cancellationToken)
        ?? throw new EntityNotFoundException("Không tìm thấy hồ sơ khách hàng.");

    public async Task<ClientWalletDto> GetWalletAsync(int customerId, CancellationToken cancellationToken)
    {
        var profile = await GetProfileAsync(customerId, cancellationToken);
        // Luôn dùng số dư từ DB; danh sách bị giới hạn nên không dùng để cộng lại số dư.
        var transactions = await repository.ListWalletTransactionsAsync(customerId, TransactionListLimit, cancellationToken);
        return new ClientWalletDto(profile.WalletBalance, transactions);
    }
}
