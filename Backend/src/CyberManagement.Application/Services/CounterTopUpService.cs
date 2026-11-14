using System.ComponentModel.DataAnnotations;
using CyberManagement.Application.DTOs.Customers;
using CyberManagement.Application.DTOs.Realtime;
using CyberManagement.Application.Interfaces.Repositories;
using CyberManagement.Application.Interfaces.Services;

namespace CyberManagement.Application.Services;

public sealed class CounterTopUpService(ICounterTopUpRepository repository, IRealtimePublisher realtime)
{
    private static readonly IReadOnlyList<CounterTopUpOptionDto> Options = Array.AsReadOnly(new[]
    {
        new CounterTopUpOptionDto(10000, 0, 10000), new(20000, 0, 20000), new(30000, 0, 30000),
        new(50000, 5000, 55000), new(100000, 15000, 115000), new(200000, 35000, 235000),
        new(300000, 50000, 350000), new(500000, 100000, 600000)
    });

    public async Task<IReadOnlyList<CounterTopUpOptionDto>> GetOptionsAsync(int customerId, CancellationToken cancellationToken)
    {
        await repository.RequireActiveCustomerAsync(customerId, cancellationToken);
        return Options;
    }

    public async Task<CounterTopUpPageDto> GetOwnAsync(int customerId, int page, int pageSize, CancellationToken cancellationToken)
    {
        ValidatePage(page, pageSize);
        await repository.RequireActiveCustomerAsync(customerId, cancellationToken);
        return await repository.GetAsync(customerId, "All", page, pageSize, cancellationToken);
    }

    public Task<CounterTopUpPageDto> GetAdminAsync(string status, int page, int pageSize, CancellationToken cancellationToken)
    {
        ValidatePage(page, pageSize);
        if (status is not ("Pending" or "Approved" or "All"))
            throw new ValidationException("Trạng thái phải là Pending, Approved hoặc All.");
        return repository.GetAsync(null, status, page, pageSize, cancellationToken);
    }

    public async Task<CounterTopUpResult> CreateAsync(int customerId, Guid key, CreateCounterTopUpCommand command, CancellationToken cancellationToken)
    {
        if (key == Guid.Empty) throw new ValidationException("Idempotency-Key phải là UUID khác rỗng.");
        var quote = Options.SingleOrDefault(option => option.Amount == command.Amount)
            ?? throw new ValidationException("Chọn một trong tám mức nạp tại quầy được hỗ trợ.");
        var result = await repository.CreateAsync(customerId, key, quote, cancellationToken);
        if (!result.Replayed) await PublishAsync(result.Request, false);
        return result;
    }

    public async Task<CounterTopUpDto> ApproveAsync(int requestId, int employeeId, ApproveCounterTopUpCommand command, CancellationToken cancellationToken)
    {
        if (requestId <= 0) throw new ValidationException("Mã yêu cầu phải là số nguyên dương.");
        if (!command.CashReceived) throw new ValidationException("Chỉ xác nhận sau khi đã thu đủ tiền mặt.");
        var result = await repository.ApproveAsync(requestId, employeeId, command.CashAmount, cancellationToken);
        if (!result.Replayed) await PublishAsync(result.Request, true);
        return result.Request;
    }

    private async Task PublishAsync(CounterTopUpDto request, bool walletChanged)
    {
        // The repository has committed before any event is published. Reconnect always refetches SQL.
        var message = new RealtimeEvent(Guid.NewGuid(), "CounterTopUpChanged", request.RequestId, DateTimeOffset.UtcNow);
        await realtime.PublishToPermissionAsync("wallet.topup", message, CancellationToken.None);
        await realtime.PublishToAccountAsync(RealtimeAccountType.Customer, request.CustomerId, message, CancellationToken.None);
        if (walletChanged)
        {
            var walletEvent = new RealtimeEvent(Guid.NewGuid(), "WalletChanged", request.CustomerId, DateTimeOffset.UtcNow);
            await realtime.PublishToAccountAsync(RealtimeAccountType.Customer, request.CustomerId,
                walletEvent, CancellationToken.None);
            await realtime.PublishToPermissionAsync("wallet.view", walletEvent, CancellationToken.None);
        }
    }

    private static void ValidatePage(int page, int pageSize)
    {
        if (page < 1 || pageSize is < 1 or > 100 || (long)(page - 1) * pageSize > int.MaxValue)
            throw new ValidationException("Trang phải từ 1, kích thước trang từ 1 đến 100.");
    }
}
