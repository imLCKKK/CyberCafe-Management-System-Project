using CyberManagement.Application.DTOs.Customers;
using CyberManagement.Application.Exceptions;
using CyberManagement.Application.Interfaces.Repositories;
using CyberManagement.Application.Wallets;
using CyberManagement.Domain.Entities;
using CyberManagement.Infrastructure.Persistance;
using Microsoft.EntityFrameworkCore;

namespace CyberManagement.Infrastructure.Repositories;

public sealed class CounterTopUpRepository(ApplicationDbContext database, IWalletLedgerWriter wallet) : ICounterTopUpRepository
{
    public async Task RequireActiveCustomerAsync(int customerId, CancellationToken cancellationToken)
    {
        var status = await database.Customers.Where(row => row.CustomerId == customerId)
            .Select(row => row.Status).SingleOrDefaultAsync(cancellationToken);
        if (status != "Active") throw new UnauthorizedAccessException("Tài khoản khách hàng hiện không hoạt động.");
    }

    public async Task<CounterTopUpPageDto> GetAsync(int? customerId, string status, int page, int pageSize, CancellationToken cancellationToken)
    {
        var query = database.CounterTopUpRequests.AsNoTracking();
        if (customerId is not null) query = query.Where(row => row.CustomerId == customerId);
        if (status != "All") query = query.Where(row => row.Status == status);
        var count = await query.CountAsync(cancellationToken);
        var rows = await query.Include(row => row.Customer).Include(row => row.WalletTransaction)
            .OrderByDescending(row => row.CreatedAt).ThenByDescending(row => row.RequestId)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);
        return new(rows.Select(ToDto).ToList(), page, pageSize, count);
    }

    public async Task<CounterTopUpResult> CreateAsync(int customerId, Guid requestKey, CounterTopUpOptionDto quote, CancellationToken cancellationToken)
    {
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        var customer = await wallet.LockCustomerAsync(customerId, cancellationToken);
        if (customer.Status != "Active") throw new UnauthorizedAccessException("Tài khoản khách hàng hiện không hoạt động.");
        var prior = await database.CounterTopUpRequests.Include(row => row.WalletTransaction)
            .SingleOrDefaultAsync(row => row.CustomerId == customerId && row.RequestKey == requestKey, cancellationToken);
        if (prior is not null)
        {
            if (prior.Amount != quote.Amount) throw new BusinessRuleException("Khóa yêu cầu đã được dùng với số tiền khác.");
            prior.Customer = customer;
            await transaction.CommitAsync(cancellationToken);
            return new(ToDto(prior), true);
        }
        if (await database.CounterTopUpRequests.AnyAsync(row => row.CustomerId == customerId && row.Status == "Pending", cancellationToken))
            throw new BusinessRuleException("Bạn đã có một yêu cầu nạp tại quầy đang chờ xử lý.");
        var request = new CounterTopUpRequest
        {
            Customer = customer, CustomerId = customerId, RequestKey = requestKey,
            Amount = quote.Amount, BonusAmount = quote.BonusAmount, TotalAmount = quote.TotalAmount
        };
        database.CounterTopUpRequests.Add(request);
        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new(ToDto(request), false);
    }

    public async Task<CounterTopUpResult> ApproveAsync(int requestId, int employeeId, decimal cashAmount, CancellationToken cancellationToken)
    {
        if (!await database.Employees.AnyAsync(row => row.EmployeeId == employeeId && row.IsActive, cancellationToken))
            throw new UnauthorizedAccessException("Tài khoản nhân viên hiện không hoạt động.");
        var customerId = await database.CounterTopUpRequests.AsNoTracking().Where(row => row.RequestId == requestId)
            .Select(row => (int?)row.CustomerId).SingleOrDefaultAsync(cancellationToken)
            ?? throw new EntityNotFoundException("Không tìm thấy yêu cầu nạp tiền.");
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        var customer = await wallet.LockCustomerAsync(customerId, cancellationToken);
        var request = await database.CounterTopUpRequests.Include(row => row.WalletTransaction)
            .SingleAsync(row => row.RequestId == requestId, cancellationToken);
        request.Customer = customer;
        if (cashAmount != request.Amount)
            throw new System.ComponentModel.DataAnnotations.ValidationException("Tiền mặt thực nhận phải đúng số tiền nạp, không bao gồm tiền tặng.");
        if (request.Status == "Approved")
        {
            await transaction.CommitAsync(cancellationToken);
            return new(ToDto(request), true);
        }
        if (customer.Status != "Active")
            throw new UnauthorizedAccessException("Tài khoản khách hàng hiện không hoạt động.");
        var now = DateTime.UtcNow;
        var result = await wallet.RecordAsync(new WalletMutation(customerId, WalletMutationKind.Credit,
            request.TotalAmount, "TopUp", "CounterTopUp", requestId.ToString(System.Globalization.CultureInfo.InvariantCulture),
            $"CounterTopUp:{requestId}", new WalletActor("Staff", employeeId), now,
            Description: $"Nạp tại quầy #{requestId}: tiền mặt {request.Amount:0}, tặng {request.BonusAmount:0}"), cancellationToken);
        request.Status = "Approved";
        request.ApprovedAt = now;
        request.ApprovedByEmployeeId = employeeId;
        request.CashAmountReceived = request.Amount;
        request.WalletTransaction = result.Entry;
        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new(ToDto(request), result.Replayed);
    }

    private static CounterTopUpDto ToDto(CounterTopUpRequest row) => new(row.RequestId, row.CustomerId,
        row.Customer.FullName, row.Customer.Phone, row.Amount, row.BonusAmount, row.TotalAmount, row.Status,
        DateTime.SpecifyKind(row.CreatedAt, DateTimeKind.Utc),
        row.ApprovedAt is { } time ? DateTime.SpecifyKind(time, DateTimeKind.Utc) : null,
        row.ApprovedByEmployeeId, row.WalletTransactionId, row.WalletTransaction?.BalanceAfter);
}
