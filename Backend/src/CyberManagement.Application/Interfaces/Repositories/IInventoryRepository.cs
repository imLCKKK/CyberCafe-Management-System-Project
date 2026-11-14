using CyberManagement.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Text;

namespace CyberManagement.Application.Interfaces.Repositories
{
    public interface IInventoryRepository
    {
        // Lấy và khóa sản phẩm trong transaction trước khi thay đổi tồn.
        Task<Product?> GetProductForUpdateAsync(
            int productId,
            CancellationToken cancellationToken = default);

        // Khóa nhiều sản phẩm theo ProductId tăng dần.
        Task<IReadOnlyList<Product>> GetProductsForUpdateAsync(
            IReadOnlyCollection<int> productIds,
            CancellationToken cancellationToken = default);

        // Lấy dòng món để đối chiếu sản phẩm, số lượng và đơn nguồn.
        Task<OrderDetail?> GetOrderDetailAsync(
            int orderDetailId,
            CancellationToken cancellationToken = default);
    }
}
