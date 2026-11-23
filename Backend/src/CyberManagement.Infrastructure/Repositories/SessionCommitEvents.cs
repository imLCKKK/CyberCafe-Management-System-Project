using System.Data.Common;
using CyberManagement.Application.DTOs.Computers;
using CyberManagement.Application.DTOs.Realtime;
using CyberManagement.Application.Interfaces.Services;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;

namespace CyberManagement.Infrastructure.Repositories;

public sealed class SessionCommitEvents(IRealtimePublisher publisher, ILogger<SessionCommitEvents> logger) : DbTransactionInterceptor
{
    private readonly Dictionary<Guid, HashSet<SessionChange>> pending = [];
    public void Enqueue(Guid transactionId, SessionChange change)
    {
        if (!pending.TryGetValue(transactionId, out var changes)) pending[transactionId] = changes = [];
        changes.Add(change);
    }

    public override async Task TransactionCommittedAsync(DbTransaction transaction, TransactionEndEventData eventData,
        CancellationToken cancellationToken = default)
    {
        if (!pending.Remove(eventData.TransactionId, out var changes)) return;
        foreach (var change in changes)
        {
            try
            {
                var message = new RealtimeEvent(Guid.NewGuid(), "SessionChanged", change.GamingSessionId, DateTimeOffset.UtcNow);
                await publisher.PublishToPermissionAsync("session.view", message, CancellationToken.None);
                await publisher.PublishToAccountAsync(RealtimeAccountType.Customer, change.CustomerId, message, CancellationToken.None);
                await publisher.PublishToAccountAsync(RealtimeAccountType.Customer, change.CustomerId,
                    new RealtimeEvent(Guid.NewGuid(), "WalletChanged", change.CustomerId, DateTimeOffset.UtcNow), CancellationToken.None);
                await publisher.PublishToPermissionAsync("computer.view",
                    new RealtimeEvent(Guid.NewGuid(), RealtimeEvent.ComputerChanged, change.ComputerId, DateTimeOffset.UtcNow), CancellationToken.None);
            }
            catch (Exception error)
            {
                // A committed payment must stay successful even when realtime delivery is unavailable.
                logger.LogWarning(error, "Could not publish session {SessionId} after commit", change.GamingSessionId);
            }
        }
    }

    public override Task TransactionRolledBackAsync(DbTransaction transaction, TransactionEndEventData eventData,
        CancellationToken cancellationToken = default)
    { pending.Remove(eventData.TransactionId); return Task.CompletedTask; }

    public override Task TransactionFailedAsync(DbTransaction transaction, TransactionErrorEventData eventData,
        CancellationToken cancellationToken = default)
    { pending.Remove(eventData.TransactionId); return Task.CompletedTask; }
}
