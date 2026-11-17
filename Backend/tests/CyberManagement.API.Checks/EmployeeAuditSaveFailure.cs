using CyberManagement.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

internal sealed class EmployeeAuditSaveFailure : SaveChangesInterceptor
{
    public int FailNextAuditSave;

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        if (eventData.Context!.ChangeTracker.Entries<AuditLog>()
            .Any(entry => entry.State == EntityState.Added && entry.Entity.EntityType == "Employee")
            && Interlocked.Exchange(ref FailNextAuditSave, 0) == 1)
            throw new InvalidOperationException("Injected employee audit save failure.");

        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }
}
