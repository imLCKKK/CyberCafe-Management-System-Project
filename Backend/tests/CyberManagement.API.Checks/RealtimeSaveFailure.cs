using Microsoft.EntityFrameworkCore.Diagnostics;

internal sealed class RealtimeSaveFailure : SaveChangesInterceptor
{
    public int FailNextSave;

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        if (Interlocked.Exchange(ref FailNextSave, 0) == 1)
            throw new InvalidOperationException("Injected save failure for realtime checks.");
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }
}
