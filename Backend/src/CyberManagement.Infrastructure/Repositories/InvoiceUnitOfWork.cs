using CyberManagement.Application.Exceptions;
using CyberManagement.Application.Interfaces.Repositories;
using CyberManagement.Infrastructure.Persistance;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace CyberManagement.Infrastructure.Repositories;

public sealed class InvoiceUnitOfWork(ApplicationDbContext db) : IInvoiceUnitOfWork
{
    public bool HasActiveTransaction => db.Database.CurrentTransaction is not null;

    public async Task<T> ExecuteAsync<T>(Func<Task<T>> operation, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ct.ThrowIfCancellationRequested();
        if (HasActiveTransaction) return await operation();
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        try
        {
            var result = await operation();
            await transaction.CommitAsync(ct);
            return result;
        }
        catch
        {
            try { await transaction.RollbackAsync(CancellationToken.None); }
            finally { db.ChangeTracker.Clear(); }
            throw;
        }
    }

    public async Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        if (!HasActiveTransaction) throw new InvalidOperationException("Phải mở transaction trước khi lưu hóa đơn.");
        db.ChangeTracker.DetectChanges();
        try { return await db.SaveChangesAsync(ct); }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException sql && sql.Number is 2601 or 2627
            && (sql.Message.Contains("InvoiceIssueRequests", StringComparison.Ordinal)
                || sql.Message.Contains("IX_InvoiceOrders_OrderId", StringComparison.Ordinal)
                || sql.Message.Contains("IX_InvoiceDetails_", StringComparison.Ordinal)))
        {
            throw new InvoiceWriteConflictException();
        }
    }
}
