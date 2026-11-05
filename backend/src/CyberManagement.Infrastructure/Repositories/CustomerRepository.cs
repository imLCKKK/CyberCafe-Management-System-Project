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
    // Lọc/sắp xếp phải làm TRƯỚC bước tạo DTO (record có constructor): EF không dịch được
    // OrderBy/Where trên thuộc tính của record sau Select.
    private static IQueryable<CustomerDto> Project(IQueryable<Customer> source) =>
        source.Select(c => new CustomerDto(
            c.CustomerId, c.FullName, c.Phone, c.Email, c.WalletBalance, c.Status, c.CreatedAt,
            c.GamingSessions.Any(s => s.Status == "Active")));

    public async Task<IReadOnlyList<CustomerDto>> ListAsync(CancellationToken cancellationToken) =>
        await Project(database.Customers.AsNoTracking().OrderByDescending(c => c.CustomerId))
            .ToListAsync(cancellationToken);

    public Task<CustomerDto?> FindDtoAsync(int customerId, CancellationToken cancellationToken) =>
        Project(database.Customers.AsNoTracking().Where(c => c.CustomerId == customerId))
            .FirstOrDefaultAsync(cancellationToken);

    public Task<bool> PhoneExistsAsync(string phone, int? excludeCustomerId, CancellationToken cancellationToken) =>
        database.Customers.AnyAsync(
            c => c.Phone == phone && (excludeCustomerId == null || c.CustomerId != excludeCustomerId), cancellationToken);

    public Task<bool> HasActiveSessionAsync(int customerId, CancellationToken cancellationToken) =>
        database.GamingSessions.AnyAsync(s => s.CustomerId == customerId && s.Status == "Active", cancellationToken);

    public async Task<IReadOnlyList<WalletTransactionDto>> ListWalletTransactionsAsync(
        int customerId, int take, CancellationToken cancellationToken) =>
        await database.WalletTransactions.AsNoTracking()
            .Where(t => t.CustomerId == customerId)
            .OrderByDescending(t => t.CreatedAt).ThenByDescending(t => t.TransactionId)
            .Take(take)
            .Select(t => new WalletTransactionDto(
                t.TransactionId, t.CustomerId, t.Amount, t.BalanceAfter, t.Type, t.Description, t.CreatedAt))
            .ToListAsync(cancellationToken);

    public Task<WalletTransactionDto?> FindWalletTransactionByKeyAsync(
        int customerId, string idempotencyKey, CancellationToken cancellationToken) =>
        database.WalletTransactions.AsNoTracking()
            .Where(t => t.CustomerId == customerId && t.IdempotencyKey == idempotencyKey)
            .Select(t => new WalletTransactionDto(
                t.TransactionId, t.CustomerId, t.Amount, t.BalanceAfter, t.Type, t.Description, t.CreatedAt))
            .FirstOrDefaultAsync(cancellationToken);

    public Task<Customer?> FindTrackedAsync(int customerId, CancellationToken cancellationToken) =>
        database.Customers.FirstOrDefaultAsync(c => c.CustomerId == customerId, cancellationToken);

    public async Task<Customer?> FindTrackedForWalletUpdateAsync(int customerId, CancellationToken cancellationToken)
    {
        if (database.Database.CurrentTransaction is null)
            throw new InvalidOperationException("FindTrackedForWalletUpdateAsync phải chạy trong ExecuteInTransactionAsync.");

        // UPDLOCK giữ khóa dòng tới khi commit/rollback: hai request đồng thời cùng ví phải xếp hàng,
        // nên không mất cập nhật và không thể cùng vượt qua kiểm tra số dư.
        var rows = await database.Customers
            .FromSqlInterpolated($"SELECT * FROM Customers WITH (UPDLOCK, ROWLOCK) WHERE CustomerId = {customerId}")
            .ToListAsync(cancellationToken);
        return rows.SingleOrDefault();
    }

    public void Add(Customer customer) => database.Customers.Add(customer);

    public async Task<T> ExecuteInTransactionAsync<T>(Func<Task<T>> work, CancellationToken cancellationToken)
    {
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        var result = await work();
        await transaction.CommitAsync(cancellationToken); // work ném lỗi thì dispose tự rollback
        return result;
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await database.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 2601 or 2627 } duplicate)
        {
            // Tên chỉ mục/ràng buộc nằm trong thông báo SQL; chỉ nói "số điện thoại" khi đúng là chỉ mục Phone.
            throw new BusinessRuleException(duplicate.Message.Contains("Phone", StringComparison.OrdinalIgnoreCase)
                ? "Số điện thoại đã tồn tại."
                : "Dữ liệu bị trùng với một bản ghi đã có (vi phạm ràng buộc duy nhất).");
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 1205 })
        {
            throw new BusinessRuleException("Xung đột dữ liệu khi xử lý, hãy tải lại và thử lại.");
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new BusinessRuleException("Dữ liệu vừa được người khác thay đổi, hãy tải lại và thử lại.");
        }
    }
}
