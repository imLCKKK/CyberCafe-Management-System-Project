using CyberManagement.Application.DTOs.Vouchers;
using CyberManagement.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Text;

namespace CyberManagement.Application.Interfaces.Repositories
{
    // Đọc/lưu voucher và dữ liệu phục vụ tính giảm.
    public interface IVoucherRepository
    {
        // Đọc danh sách voucher để quản trị.
        Task<IReadOnlyList<VoucherDto>> GetAllAsync(CancellationToken cancellationToken = default);

        // Đọc thông tin voucher, không sửa dữ liệu.
        Task<Promotion?> GetByIdAsync(int promotionId, CancellationToken cancellationToken = default);

        // Đọc voucher theo mã đã chuẩn hóa.
        Task<Promotion?> GetByCodeAsync(string normalizedCode, CancellationToken cancellationToken = default);

        // Lấy dữ liệu mới dưới khóa; yêu cầu có transaction.
        Task<Promotion?> GetByIdForUpdateAsync(int promotionId, CancellationToken cancellationToken = default);

        // Khóa voucher theo mã để kiểm tra hạn mức và ghi lượt.
        Task<Promotion?> GetByCodeForUpdateAsync(string normalizedCode, CancellationToken cancellationToken = default);

        // Kiểm tra mã trùng; khi sửa thì loại chính voucher đang sửa.
        Task<bool> CodeExistsAsync(string normalizedCode, int? excludedPromotionId = null, CancellationToken cancellationToken = default);

        // Đọc sản phẩm kèm Category để tính preview bằng giá server.
        Task<IReadOnlyList<Product>> GetProductsForPreviewAsync(IReadOnlyCollection<int> productIds, CancellationToken cancellationToken = default);

        // Khóa đơn và đọc các dòng món để đối chiếu nguồn/snapshot.
        Task<Order?> GetOrderForUpdateAsync(int orderId, CancellationToken cancellationToken = default);

        // Đưa voucher mới vào DbContext để chờ lưu.
        Task AddAsync(Promotion promotion, CancellationToken cancellationToken = default);
    }
}
