using CyberManagement.Application.DTOs.Realtime;

namespace CyberManagement.Application.Interfaces.Services;

public interface IRealtimePublisher
{
    Task PublishToPermissionAsync(string permission, RealtimeEvent message, CancellationToken cancellationToken);
    Task PublishToAccountAsync(
        RealtimeAccountType accountType, int accountId, RealtimeEvent message, CancellationToken cancellationToken);
}
