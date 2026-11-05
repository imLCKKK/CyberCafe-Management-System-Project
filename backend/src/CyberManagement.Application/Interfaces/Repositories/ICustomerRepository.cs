using CyberManagement.Application.DTOs.Customers;
using CyberManagement.Domain.Entities;

namespace CyberManagement.Application.Interfaces.Repositories;

public interface ICustomerRepository
{
    Task<IReadOnlyList<CustomerDto>> ListAsync(CancellationToken cancellationToken);
    Task<CustomerDto?> FindDtoAsync(int customerId, CancellationToken cancellationToken);
    Task<bool> PhoneExistsAsync(string phone, int? excludeCustomerId, CancellationToken cancellationToken);
    Task<bool> HasActiveSessionAsync(int customerId, CancellationToken cancellationToken);
    Task<IReadOnlyList<WalletTransactionDto>> ListWalletTransactionsAsync(
        int customerId, int take, CancellationToken cancellationToken);

    /// <summary>Tải khách có theo dõi thay đổi (không khóa).</summary>
    Task<Customer?> FindTrackedAsync(int customerId, CancellationToken cancellationToken);

    /// <summary>
    /// Tải khách có theo dõi VÀ khóa dòng (UPDLOCK) tới hết transaction: mọi thay đổi ví phải qua đây.
    /// Chỉ gọi bên trong <see cref="ExecuteInTransactionAsync{T}"/>.
    /// </summary>
    Task<Customer?> FindTrackedForWalletUpdateAsync(int customerId, CancellationToken cancellationToken);

    void Add(Customer customer);

    /// <summary>Chạy work trong một transaction; lỗi thì rollback toàn bộ.</summary>
    Task<T> ExecuteInTransactionAsync<T>(Func<Task<T>> work, CancellationToken cancellationToken);

    /// <summary>Lưu; lỗi trùng khóa/xung đột được đổi thành BusinessRuleException (HTTP 409).</summary>
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
