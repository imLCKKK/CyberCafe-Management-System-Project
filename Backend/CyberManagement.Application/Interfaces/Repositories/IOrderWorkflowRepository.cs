using CyberManagement.Domain.Entities;

namespace CyberManagement.Application.Interfaces.Repositories;

/// <summary>
/// Tất cả thao tác ghi, kể cả qua repository khác, phải dùng chung một transaction
/// và DbContext. ExecuteAsync chỉ commit khi thành công, rollback nếu có bất kỳ lỗi nào.
/// Giữ khóa dữ liệu đã đọc đến khi commit, hoặc kiểm soát đồng thời bằng phiên bản và thử lại.
/// Khóa khách hàng, đơn hàng và sản phẩm theo cùng thứ tự; không bỏ qua xung đột dữ liệu.
/// Database cũng phải đảm bảo mỗi OrderId chỉ có một OrderDebit, mỗi OrderDetailId
/// chỉ có một OrderItemRefund và tối đa một yêu cầu hủy đang Pending.
/// </summary>
public interface IOrderWorkflowRepository
{
    Task<T> ExecuteAsync<T>(Func<Task<T>> operation);
    Task<Customers?> GetCustomerForUpdateAsync(long customerId);
    Task<GamingSessions?> GetSessionForUpdateAsync(long sessionId);
    Task<GamingSessions?> GetSessionAsync(long sessionId);
    Task<Product?> GetProductForUpdateAsync(long productId);
    // Phải Include OrderDetails.Product và các CancellationRequests đang Pending.
    Task<Order?> GetOrderForUpdateAsync(long orderId);
    Task UpdateCustomerAsync(Customers customer);
    Task AddWalletTransactionAsync(WalletTransactions transaction);
    Task<WalletTransactions?> GetOrderDebitAsync(long orderId);
    Task<bool> HasItemRefundAsync(long orderDetailId);
    // Lấy GamingCharge theo SessionId, OrderDebit qua Order.SessionId,
    // và khoản hoàn qua OrderDetail.Order.SessionId; chỉ lấy giao dịch thuộc phiên này.
    Task<IEnumerable<WalletTransactions>> GetSessionWalletTransactionsAsync(long sessionId);
    Task AddCancellationRequestAsync(OrderItemCancelRequest request);
    Task UpdateCancellationRequestAsync(OrderItemCancelRequest request);
}
