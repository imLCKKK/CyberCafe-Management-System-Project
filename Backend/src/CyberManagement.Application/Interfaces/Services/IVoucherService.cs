using CyberManagement.Application.DTOs.Vouchers;
using System;
using System.Collections.Generic;
using System.Text;

namespace CyberManagement.Application.Interfaces.Services
{
    // Nghiệp vụ quản lý, kiểm tra và sử dụng voucher.
    public interface IVoucherService
    {
        // Xem danh sách voucher.
        Task<IReadOnlyList<VoucherDto>> GetAllAsync(CancellationToken cancellationToken = default);

        // Xem chi tiết một voucher.
        Task<VoucherDto> GetByIdAsync(int promotionId, CancellationToken cancellationToken = default);

        // Tạo voucher mới sau khi kiểm tra dữ liệu và mã trùng.
        Task<VoucherDto> CreateAsync(SaveVoucherRequest request, CancellationToken cancellationToken = default);

        // Sửa quy tắc cho đơn mới, giữ snapshot của đơn cũ.
        Task<VoucherDto> UpdateAsync(int promotionId, SaveVoucherRequest request, CancellationToken cancellationToken = default);

        // Bật/tắt khả năng áp dụng voucher.
        Task<VoucherDto> UpdateStatusAsync(int promotionId, UpdateVoucherStatusRequest request, CancellationToken cancellationToken = default);

        // Tính giảm cho giỏ của khách, không ghi hoặc giữ lượt.
        Task<VoucherPreviewDto> PreviewAsync(PreviewVoucherRequest request, int customerId, CancellationToken cancellationToken = default);

        // Kiểm tra, áp giảm và ghi lượt cho đơn đã lưu giá từ server.
        // Caller đã khóa nguồn theo thứ tự chung; không commit tại đây.
        Task<VoucherApplicationResult> ValidateAndUseAsync(int orderId, string code, int customerId, CancellationToken cancellationToken = default);

        // Caller xác minh hủy toàn bộ dòng và lưu nguồn Cancelled/Expired trong transaction chung.
        // True: vừa hoàn; false: đã hoàn hoặc đơn không dùng voucher.
        Task<bool> ReleaseForCancelledOrderAsync(int orderId, CancellationToken cancellationToken = default);
    }
}
