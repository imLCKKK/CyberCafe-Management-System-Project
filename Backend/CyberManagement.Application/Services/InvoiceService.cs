using CyberManagement.Application.DTOs;
using CyberManagement.Application.Exceptions;
using CyberManagement.Application.Interfaces;
using CyberManagement.Application.Interfaces.Repositories;
using CyberManagement.Application.Interfaces.Services;
using CyberManagement.Domain.Entities;

namespace CyberManagement.Application.Services;

public class InvoiceService : IInvoiceService
{
    private readonly IInvoiceRepository _invoiceRepository;
    private readonly ICurrentUser _currentUser;

    public InvoiceService(IInvoiceRepository invoiceRepository, ICurrentUser currentUser)
    {
        _invoiceRepository = invoiceRepository;
        _currentUser = currentUser;
    }

    // Chỉ cho phép tài khoản đã đăng nhập và đang hoạt động.
    private void RequireActiveAccount()
    {
        if (_currentUser.AccountId <= 0 || !_currentUser.IsActive)
        {
            throw new UnauthorizedAccessException(
                "Tài khoản chưa đăng nhập hoặc đã bị khóa.");
        }
    }

    // Admin/Staff được xem danh sách hóa đơn toàn hệ thống.
    public async Task<IReadOnlyList<InvoiceResponse>>GetAdminInvoicesAsync(CancellationToken cancellationToken = default)
    {
        RequireActiveAccount();

        if (_currentUser.Role is not ("Admin" or "Staff"))
        {
            throw new UnauthorizedAccessException(
                "Chỉ Admin hoặc Staff được xem danh sách hóa đơn quản trị.");
        }

        return await _invoiceRepository.GetRecentAsync(cancellationToken);
    }

    // Khách chỉ được xem hóa đơn của chính mình.
    public async Task<IReadOnlyList<InvoiceResponse>>GetMyInvoicesAsync(CancellationToken cancellationToken = default)
    {
        RequireActiveAccount();

        if (_currentUser.Role != "Customer")
        {
            throw new UnauthorizedAccessException(
                "Chỉ Customer được sử dụng chức năng này.");
        }

        // Lấy ID khách từ danh tính đăng nhập, không lấy từ request.
        var customerId = _currentUser.CustomerId;

        if (customerId is null || customerId.Value <= 0)
        {
            throw new UnauthorizedAccessException(
                "Tài khoản chưa có thông tin khách hàng hợp lệ.");
        }

        return await _invoiceRepository.GetByCustomerIdAsync(customerId.Value, cancellationToken);
    }
}
