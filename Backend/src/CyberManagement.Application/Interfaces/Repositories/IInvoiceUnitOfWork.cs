namespace CyberManagement.Application.Interfaces.Repositories;

public interface IInvoiceUnitOfWork
{
    bool HasActiveTransaction { get; }
    Task<T> ExecuteAsync<T>(Func<Task<T>> operation, CancellationToken ct = default);
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}
