using System.Data;
using CyberManagement.Application.DTOs.Customers;
using CyberManagement.Application.Exceptions;
using CyberManagement.Application.Interfaces.Repositories;
using CyberManagement.Domain.Entities;
using CyberManagement.Infrastructure.Persistance;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace CyberManagement.Infrastructure.Repositories;

public sealed class CustomerRepository(ApplicationDbContext database) : ICustomerRepository
{
    public async Task<CustomerPageDto> GetCustomersAsync(
        int page, int pageSize, string? search, CancellationToken cancellationToken)
    {
        var query = database.Customers.AsNoTracking();
        if (!string.IsNullOrEmpty(search))
            query = query.Where(customer => customer.FullName.Contains(search)
                || customer.Phone.Contains(search)
                || (customer.Email != null && customer.Email.Contains(search)));

        var count = await query.CountAsync(cancellationToken);
        var customers = await query.OrderByDescending(customer => customer.CustomerId)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .ToListAsync(cancellationToken);
        return new CustomerPageDto(customers.Select(ToProfile).ToList(), page, pageSize, count);
    }

    public async Task<CustomerProfileDto> GetProfileAsync(
        int customerId, bool requireActive, CancellationToken cancellationToken) =>
        ToProfile(await FindCustomerAsync(customerId, requireActive, cancellationToken));

    public async Task<CustomerProfileDto> CreateAsync(Customer customer, CancellationToken cancellationToken)
    {
        if (await database.Customers.AnyAsync(existing => existing.Phone == customer.Phone, cancellationToken))
            throw new BusinessRuleException("Số điện thoại đã được sử dụng.");

        database.Customers.Add(customer);
        try
        {
            await database.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (exception.InnerException is SqlException { Number: 2601 or 2627 })
        {
            // The SQL unique index also protects two simultaneous create requests.
            throw new BusinessRuleException("Số điện thoại đã được sử dụng.");
        }
        return ToProfile(customer);
    }

    public async Task<CustomerWalletDto> GetWalletAsync(
        int customerId, int page, int pageSize, bool requireActive, CancellationToken cancellationToken)
    {
        // Hold a consistent SQL view of the balance and ledger while writers commit atomically.
        // InMemory checks exercise behavior only, not SQL isolation.
        await using var transaction = database.Database.IsRelational()
            ? await database.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken)
            : null;
        var customer = await FindCustomerAsync(customerId, requireActive, cancellationToken);
        var query = database.WalletTransactions.AsNoTracking()
            .Where(entry => entry.CustomerId == customerId);
        var count = await query.CountAsync(cancellationToken);
        var entries = await query.OrderByDescending(entry => entry.CreatedAt)
            .ThenByDescending(entry => entry.TransactionId)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .ToListAsync(cancellationToken);
        var result = new CustomerWalletDto(customerId, customer.WalletBalance,
            entries.Select(entry => new WalletTransactionDto(
                entry.TransactionId, entry.Amount, entry.BalanceAfter, entry.Type,
                entry.Description, AsUtc(entry.CreatedAt))).ToList(), page, pageSize, count);
        if (transaction is not null)
            await transaction.CommitAsync(cancellationToken);
        return result;
    }

    private async Task<Customer> FindCustomerAsync(
        int customerId, bool requireActive, CancellationToken cancellationToken)
    {
        var customer = await database.Customers.AsNoTracking()
            .SingleOrDefaultAsync(customer => customer.CustomerId == customerId, cancellationToken)
            ?? throw new EntityNotFoundException("Không tìm thấy khách hàng.");
        if (requireActive && customer.Status != "Active")
            throw new UnauthorizedAccessException("Tài khoản khách hàng hiện không hoạt động.");
        return customer;
    }

    private static DateTime AsUtc(DateTime value) => DateTime.SpecifyKind(value, DateTimeKind.Utc);

    private static CustomerProfileDto ToProfile(Customer customer) => new(
        customer.CustomerId, customer.FullName, customer.Phone, customer.Email,
        customer.Status, AsUtc(customer.CreatedAt));
}
