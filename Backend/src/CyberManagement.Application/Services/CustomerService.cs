using System.ComponentModel.DataAnnotations;
using CyberManagement.Application.DTOs.Customers;
using CyberManagement.Application.Interfaces.Repositories;
using CyberManagement.Application.Interfaces.Services;
using CyberManagement.Domain.Entities;

namespace CyberManagement.Application.Services;

public sealed class CustomerService(
    ICustomerRepository repository,
    ICustomerPasswordHasher passwordHasher) : ICustomerService
{
    public Task<CustomerPageDto> GetCustomersAsync(
        int page, int pageSize, string? search, CancellationToken cancellationToken)
    {
        ValidatePage(page, pageSize);
        search = search?.Trim();
        if (search?.Length > 150)
            throw new ValidationException("Từ khóa tìm kiếm không được vượt quá 150 ký tự.");
        return repository.GetCustomersAsync(page, pageSize, search, cancellationToken);
    }

    public Task<CustomerProfileDto> GetProfileAsync(int customerId, CancellationToken cancellationToken)
    {
        ValidateCustomerId(customerId);
        return repository.GetProfileAsync(customerId, false, cancellationToken);
    }

    public Task<CustomerProfileDto> GetOwnProfileAsync(int customerId, CancellationToken cancellationToken)
    {
        ValidateCustomerId(customerId);
        return repository.GetProfileAsync(customerId, true, cancellationToken);
    }

    public Task<CustomerProfileDto> CreateAsync(CreateCustomerCommand command, CancellationToken cancellationToken)
    {
        var fullName = command.FullName?.Trim();
        var phone = command.Phone?.Trim();
        var email = string.IsNullOrWhiteSpace(command.Email) ? null : command.Email.Trim();
        if (string.IsNullOrWhiteSpace(fullName) || fullName.Length > 150)
            throw new ValidationException("Họ tên là bắt buộc và không được vượt quá 150 ký tự.");
        if (string.IsNullOrEmpty(phone) || phone.Length > 20)
            throw new ValidationException("Số điện thoại phải có từ 8 đến 20 chữ số, có thể bắt đầu bằng +.");
        var digits = phone.StartsWith('+') ? phone.AsSpan(1) : phone.AsSpan();
        if (digits.Length < 8 || digits.Length > 20 || digits.ContainsAnyExceptInRange('0', '9'))
            throw new ValidationException("Số điện thoại phải có từ 8 đến 20 chữ số, có thể bắt đầu bằng +.");
        if (email is not null && (email.Length > 150 || !new EmailAddressAttribute().IsValid(email)))
            throw new ValidationException("Email không hợp lệ hoặc vượt quá 150 ký tự.");
        if (string.IsNullOrWhiteSpace(command.Password) || command.Password.Length is < 12 or > 128)
            throw new ValidationException("Mật khẩu phải có từ 12 đến 128 ký tự.");

        var customer = new Customer
        {
            FullName = fullName,
            Phone = phone,
            Email = email,
            WalletBalance = 0,
            Status = "Active",
            CreatedAt = DateTime.UtcNow
        };
        customer.PasswordHash = passwordHasher.HashPassword(customer, command.Password);
        return repository.CreateAsync(customer, cancellationToken);
    }

    public Task<CustomerWalletDto> GetWalletAsync(
        int customerId, int page, int pageSize, CancellationToken cancellationToken)
    {
        ValidateCustomerId(customerId);
        ValidatePage(page, pageSize);
        return repository.GetWalletAsync(customerId, page, pageSize, false, cancellationToken);
    }

    public Task<CustomerWalletDto> GetOwnWalletAsync(
        int customerId, int page, int pageSize, CancellationToken cancellationToken)
    {
        ValidateCustomerId(customerId);
        ValidatePage(page, pageSize);
        return repository.GetWalletAsync(customerId, page, pageSize, true, cancellationToken);
    }

    private static void ValidateCustomerId(int customerId)
    {
        if (customerId <= 0)
            throw new ValidationException("ID khách hàng phải là số nguyên dương.");
    }

    private static void ValidatePage(int page, int pageSize)
    {
        if (page < 1 || pageSize is < 1 or > 100 || (long)(page - 1) * pageSize > int.MaxValue)
            throw new ValidationException("Trang phải từ 1, kích thước trang từ 1 đến 100 và phạm vi phân trang hợp lệ.");
    }
}
