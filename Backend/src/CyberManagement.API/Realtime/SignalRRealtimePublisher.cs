using CyberManagement.Application.DTOs.Realtime;
using CyberManagement.Application.Interfaces.Services;
using Microsoft.AspNetCore.SignalR;

namespace CyberManagement.API.Realtime;

public sealed class SignalRRealtimePublisher(
    IHubContext<RealtimeHub> hub, ILogger<SignalRRealtimePublisher> logger) : IRealtimePublisher
{
    public Task PublishToPermissionAsync(string permission, RealtimeEvent message, CancellationToken cancellationToken) =>
        PublishAsync(RealtimeGroups.ForPermission(permission), message, cancellationToken);

    public Task PublishToAccountAsync(
        RealtimeAccountType accountType, int accountId, RealtimeEvent message, CancellationToken cancellationToken) =>
        PublishAsync(RealtimeGroups.ForAccount(accountType, accountId), message, cancellationToken);

    private async Task PublishAsync(string group, RealtimeEvent message, CancellationToken cancellationToken)
    {
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(2));
            await hub.Clients.Group(group).SendAsync(message.Type, message, timeout.Token);
        }
        catch (Exception exception)
        {
            // SQL already committed. Reconnect/refetch recovers state when notification delivery fails.
            logger.LogWarning(exception, "Could not publish {EventType} {EventId} to {Group}",
                message.Type, message.EventId, group);
        }
    }
}
