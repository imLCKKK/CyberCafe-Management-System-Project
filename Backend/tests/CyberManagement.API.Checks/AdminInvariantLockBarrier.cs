using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

internal sealed class AdminInvariantLockBarrier : DbCommandInterceptor
{
    private readonly object _sync = new();
    private TaskCompletionSource<bool>? _release;
    private int _expectedArrivals;
    private int _arrivals;

    public void Arm(int expectedArrivals)
    {
        lock (_sync)
        {
            _expectedArrivals = expectedArrivals;
            _arrivals = 0;
            _release = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        }
    }

    public void Disarm()
    {
        lock (_sync)
        {
            _release?.TrySetResult(true);
            _release = null;
        }
    }

    public override async ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        Task? wait = null;
        lock (_sync)
        {
            if (_release is not null && command.CommandText.Contains("sp_getapplock", StringComparison.OrdinalIgnoreCase))
            {
                wait = _release.Task;
                if (++_arrivals >= _expectedArrivals)
                    _release.TrySetResult(true);
            }
        }

        if (wait is not null)
            await wait.WaitAsync(TimeSpan.FromSeconds(15), cancellationToken);

        return result;
    }
}
