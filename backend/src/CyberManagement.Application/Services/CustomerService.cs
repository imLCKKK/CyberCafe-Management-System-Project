using CyberManagement.Application.DTOs.Customers;
using CyberManagement.Application.Exceptions;
using CyberManagement.Application.Interfaces.Repositories;
using CyberManagement.Application.Interfaces.Services;
using CyberManagement.Application.Interfaces.Validation;
using CyberManagement.Application.Wallet;
using CyberManagement.Domain.Entities;

namespace CyberManagement.Application.Services;

public sealed class CustomerService(
    ICustomerRepository repository,
    ICustomerPasswordHasher passwordHasher,
    IRequestValidator<CreateCustomerCommand> createValidator,
    IRequestValidator<UpdateCustomerCommand> updateValidator,
    IRequestValidator<ChangeCustomerStatusCommand> statusValidator,
    IRequestValidator<StaffTopUpCommand> topUpValidator) : ICustomerService
{
    private const int TransactionListLimit = 200;

    public Task<IReadOnlyList<CustomerDto>> ListAsync(CancellationToken cancellationToken) =>
        repository.ListAsync(cancellationToken);

    public async Task<CustomerDto> GetAsync(int customerId, CancellationToken cancellationToken) =>
        await repository.FindDtoAsync(customerId, cancellationToken)
        ?? throw new EntityNotFoundException("Không tìm thấy khách hàng.");

    public async Task<CustomerDto> CreateAsync(CreateCustomerCommand command, CancellationToken cancellationToken)
    {
        createValidator.Validate(command);
        var phone = command.Phone!.Trim();
        if (await repository.PhoneExistsAsync(phone, null, cancellationToken))
            throw new BusinessRuleException("Số điện thoại đã tồn tại.");

        var now = DateTime.UtcNow;
        var customer = new Customer
        {
            FullName = command.FullName!.Trim(),
            Phone = phone,
            Email = NormalizeEmail(command.Email),
            WalletBalance = 0,
            Status = "Active",
            CreatedAt = now,
        };
        // Cùng bộ băm mật khẩu với đăng nhập khách của Đại nên khách mới đăng nhập được ngay.
        customer.PasswordHash = passwordHasher.HashPassword(customer, command.InitialPassword!);
        customer.StatusHistory.Add(new CustomerStatusHistory
        {
            OldStatus = null,
            NewStatus = "Active",
            Reason = "Tạo hồ sơ khách hàng",
            ChangedBy = command.ActorEmployeeId,
            ChangedAt = now,
        });

        repository.Add(customer);
        await repository.SaveChangesAsync(cancellationToken); // một SaveChanges = một transaction
        // Dựng response từ dữ liệu vừa lưu, không truy vấn lại: đã commit thì không được trả 500 vì query phụ.
        return ToDto(customer, hasActiveSession: false);
    }

    public async Task<CustomerDto> UpdateAsync(UpdateCustomerCommand command, CancellationToken cancellationToken)
    {
        updateValidator.Validate(command);
        var customer = await repository.FindTrackedAsync(command.CustomerId, cancellationToken)
            ?? throw new EntityNotFoundException("Không tìm thấy khách hàng.");
        var phone = command.Phone!.Trim();
        if (await repository.PhoneExistsAsync(phone, customer.CustomerId, cancellationToken))
            throw new BusinessRuleException("Số điện thoại đã tồn tại.");

        // Cố ý không có WalletBalance trong command: ví chỉ đổi qua WalletLedger.
        var hasActiveSession = await repository.HasActiveSessionAsync(customer.CustomerId, cancellationToken);
        customer.FullName = command.FullName!.Trim();
        customer.Phone = phone;
        customer.Email = NormalizeEmail(command.Email);
        await repository.SaveChangesAsync(cancellationToken);
        return ToDto(customer, hasActiveSession);
    }

    public async Task<CustomerDto> ChangeStatusAsync(ChangeCustomerStatusCommand command, CancellationToken cancellationToken)
    {
        statusValidator.Validate(command);
        var customer = await repository.FindTrackedAsync(command.CustomerId, cancellationToken)
            ?? throw new EntityNotFoundException("Không tìm thấy khách hàng.");
        if (string.Equals(customer.Status, command.NewStatus, StringComparison.Ordinal))
            throw new BusinessRuleException($"Khách hàng đã ở trạng thái {command.NewStatus}.");

        var hasActiveSession = await repository.HasActiveSessionAsync(customer.CustomerId, cancellationToken);
        if (command.NewStatus == "Locked" && hasActiveSession)
            throw new BusinessRuleException("Khách đang có phiên chơi Active, hãy kết thúc phiên trước khi khóa.");

        var now = DateTime.UtcNow;
        customer.StatusHistory.Add(new CustomerStatusHistory
        {
            OldStatus = customer.Status,
            NewStatus = command.NewStatus!,
            Reason = command.Reason!.Trim(),
            ChangedBy = command.ActorEmployeeId,
            ChangedAt = now,
        });
        customer.Status = command.NewStatus!;
        await repository.SaveChangesAsync(cancellationToken);
        return ToDto(customer, hasActiveSession);
    }

    public async Task<IReadOnlyList<WalletTransactionDto>> GetWalletTransactionsAsync(
        int customerId, CancellationToken cancellationToken)
    {
        if (await repository.FindDtoAsync(customerId, cancellationToken) is null)
            throw new EntityNotFoundException("Không tìm thấy khách hàng.");
        return await repository.ListWalletTransactionsAsync(customerId, TransactionListLimit, cancellationToken);
    }

    public async Task<WalletTransactionDto> TopUpAsync(StaffTopUpCommand command, CancellationToken cancellationToken)
    {
        topUpValidator.Validate(command);
        return await repository.ExecuteInTransactionAsync(async () =>
        {
            // Khóa dòng khách tới hết transaction: hai request đồng thời cùng ví sẽ xử lý lần lượt.
            var customer = await repository.FindTrackedForWalletUpdateAsync(command.CustomerId, cancellationToken)
                ?? throw new EntityNotFoundException("Không tìm thấy khách hàng.");
            var transaction = WalletLedger.Record(
                customer, WalletTransactionTypes.TopUp, command.Amount, DateTime.UtcNow,
                $"Nạp tại quầy ({command.CollectionMethod}) - nhân viên #{command.ActorEmployeeId}");
            await repository.SaveChangesAsync(cancellationToken);
            return new WalletTransactionDto(
                transaction.TransactionId, transaction.CustomerId, transaction.Amount, transaction.BalanceAfter,
                transaction.Type, transaction.Description, transaction.CreatedAt);
        }, cancellationToken);
    }

    private static string? NormalizeEmail(string? email) =>
        string.IsNullOrWhiteSpace(email) ? null : email.Trim();

    private static CustomerDto ToDto(Customer c, bool hasActiveSession) =>
        new(c.CustomerId, c.FullName, c.Phone, c.Email, c.WalletBalance, c.Status, c.CreatedAt, hasActiveSession);
}
