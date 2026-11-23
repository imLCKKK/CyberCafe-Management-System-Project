using CyberManagement.Application.DTOs.Computers;

namespace CyberManagement.Application.Interfaces.Services;

public interface ISessionService
{
    Task<IReadOnlyList<SessionDto>> GetSessionsAsync(CancellationToken ct);
    Task<SessionHomeDto> GetHomeAsync(int customerId, CancellationToken ct);
    Task<SessionDto> OpenAsync(OpenSessionCommand command, int employeeId, string key, CancellationToken ct);
    Task<SessionDto> CloseAsync(int sessionId, int employeeId, string key, CancellationToken ct);
}
