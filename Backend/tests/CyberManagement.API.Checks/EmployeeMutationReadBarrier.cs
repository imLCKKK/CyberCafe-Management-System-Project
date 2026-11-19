using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

internal sealed class EmployeeMutationReadBarrier : DbCommandInterceptor, IDbTransactionInterceptor
{
    private readonly object _sync = new();
    private string? _requestKey;
    private Guid? _firstContext;
    private Guid? _secondContext;
    private TaskCompletionSource<bool> _receiptsRead = NewSignal();
    private TaskCompletionSource<bool> _firstCommitted = NewSignal();
    private bool _commitObserved;

    public bool ProfileReadAfterCommitObserved { get; private set; }

    public void Arm(string requestKey)
    {
        lock (_sync)
        {
            _requestKey = requestKey;
            _firstContext = _secondContext = null;
            _receiptsRead = NewSignal();
            _firstCommitted = NewSignal();
            _commitObserved = ProfileReadAfterCommitObserved = false;
        }
    }

    public void Disarm()
    {
        lock (_sync)
        {
            _requestKey = null;
            _receiptsRead.TrySetResult(true);
            _firstCommitted.TrySetResult(true);
        }
    }

    public override async ValueTask<DbDataReader> ReaderExecutedAsync(
        DbCommand command, CommandExecutedEventData eventData, DbDataReader result,
        CancellationToken cancellationToken = default)
    {
        Task? wait = null;
        lock (_sync)
        {
            if (_requestKey is not null && command.Transaction is null
                && command.CommandText.Contains("FROM [AuditLogs]", StringComparison.Ordinal)
                && command.Parameters.Cast<DbParameter>().Any(parameter => Equals(parameter.Value, _requestKey)))
            {
                var contextId = eventData.Context!.ContextId.InstanceId;
                if (_firstContext is null)
                    _firstContext = contextId;
                else
                {
                    _secondContext = contextId;
                    _receiptsRead.TrySetResult(true);
                }
                wait = _receiptsRead.Task;
            }
        }
        if (wait is not null)
            await wait.WaitAsync(TimeSpan.FromSeconds(15), cancellationToken);
        return result;
    }

    public override async ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
        DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result,
        CancellationToken cancellationToken = default)
    {
        Task? wait = null;
        lock (_sync)
        {
            // Both receipt reads miss; only the second profile read waits for the winner's commit.
            if (_requestKey is not null && eventData.Context?.ContextId.InstanceId == _secondContext
                && command.CommandText.Contains("FROM [Employees]", StringComparison.Ordinal))
                wait = _firstCommitted.Task;
        }
        if (wait is not null)
        {
            await wait.WaitAsync(TimeSpan.FromSeconds(15), cancellationToken);
            lock (_sync)
                ProfileReadAfterCommitObserved = _commitObserved;
        }
        return result;
    }

    public Task TransactionCommittedAsync(
        DbTransaction transaction, TransactionEndEventData eventData,
        CancellationToken cancellationToken = default)
    {
        lock (_sync)
        {
            if (eventData.Context?.ContextId.InstanceId == _firstContext)
            {
                _commitObserved = true;
                _firstCommitted.TrySetResult(true);
            }
        }
        return Task.CompletedTask;
    }

    private static TaskCompletionSource<bool> NewSignal() =>
        new(TaskCreationOptions.RunContinuationsAsynchronously);
}
