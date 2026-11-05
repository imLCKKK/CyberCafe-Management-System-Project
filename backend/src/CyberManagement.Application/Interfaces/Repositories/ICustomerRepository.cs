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

    /// <summary>
    /// Tìm giao dịch ví đã ghi với khóa chống lặp này của khách (không theo dõi thay đổi).
    /// Gọi SAU <see cref="FindTrackedForWalletUpdateAsync"/> trong cùng transaction: khóa dòng khách đảm bảo
    /// hai request cùng khóa xử lý lần lượt nên request sau luôn thấy giao dịch của request trước.
    /// </summary>
    Task<WalletTransactionDto?> FindWalletTransactionByKeyAsync(
        int customerId, string idempotencyKey, CancellationToken cancellationToken);

    void Add(Customer customer);

    /// <summary>Chạy work trong một transaction; lỗi thì rollback toàn bộ.</summary>
    Task<T> ExecuteInTransactionAsync<T>(Func<Task<T>> work, CancellationToken cancellationToken);

    /// <summary>Lưu; lỗi trùng khóa/xung đột được đổi thành BusinessRuleException (HTTP 409).</summary>
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
