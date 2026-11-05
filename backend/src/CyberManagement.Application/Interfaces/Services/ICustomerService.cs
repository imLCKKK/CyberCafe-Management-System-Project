using CyberManagement.Application.DTOs.Customers;

namespace CyberManagement.Application.Interfaces.Services;

// Nhân viên: quyền kiểm tra ở API bằng permission customer.* / wallet.*.
public interface ICustomerService
{
    Task<IReadOnlyList<CustomerDto>> ListAsync(CancellationToken cancellationToken);
    Task<CustomerDto> GetAsync(int customerId, CancellationToken cancellationToken);
    Task<CustomerDto> CreateAsync(CreateCustomerCommand command, CancellationToken cancellationToken);
    Task<CustomerDto> UpdateAsync(UpdateCustomerCommand command, CancellationToken cancellationToken);
    Task<CustomerDto> ChangeStatusAsync(ChangeCustomerStatusCommand command, CancellationToken cancellationToken);
    Task<IReadOnlyList<WalletTransactionDto>> GetWalletTransactionsAsync(int customerId, CancellationToken cancellationToken);
    Task<StaffTopUpResult> TopUpAsync(StaffTopUpCommand command, CancellationToken cancellationToken);
}

// Khách tự xem dữ liệu của mình: customerId luôn lấy từ token, không nhận từ request.
public interface IClientAccountService
{
    Task<CustomerDto> GetProfileAsync(int customerId, CancellationToken cancellationToken);
    Task<ClientWalletDto> GetWalletAsync(int customerId, CancellationToken cancellationToken);
}
