using CyberManagement.Infrastructure.Persistance;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace CyberManagement.Infrastructure.Repositories;

internal static class SqlWork
{
    // Retry the entire transaction after a deadlock, never an isolated SaveChanges.
    public static async Task<T> RunAsync<T>(ApplicationDbContext db, Func<Task<T>> work, CancellationToken ct)
    {
        if (db.Database.CurrentTransaction is not null) return await work();
        for (var attempt = 0; ; attempt++)
        {
            await using var transaction = db.Database.IsRelational()
                ? await db.Database.BeginTransactionAsync(ct) : null;
            try
            {
                var result = await work();
                await db.SaveChangesAsync(ct);
                if (transaction is not null) await transaction.CommitAsync(ct);
                return result;
            }
            catch (Exception error)
            {
                if (transaction is not null) await transaction.RollbackAsync(CancellationToken.None);
                db.ChangeTracker.Clear();
                var sql = error as SqlException ?? error.InnerException as SqlException;
                if (sql?.Number != 1205 || attempt >= 3) throw;
                await Task.Delay(40 * (attempt + 1) + Random.Shared.Next(40), ct);
            }
        }
    }
}
