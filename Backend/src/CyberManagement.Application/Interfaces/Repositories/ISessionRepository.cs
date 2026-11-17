using CyberManagement.Application.DTOs.Computers;

namespace CyberManagement.Application.Interfaces.Repositories;

public interface ISessionRepository
{
    Task<IReadOnlyList<SessionDto>> GetSessionsAsync(CancellationToken ct);
    Task<SessionHomeDto> GetHomeAsync(int customerId, CancellationToken ct);
    Task<SessionDto> OpenAsync(OpenSessionCommand command, int employeeId, string key, CancellationToken ct);
    Task<SessionDto> CloseAsync(int sessionId, int employeeId, string key, CancellationToken ct);
    Task<IReadOnlyList<int>> GetActiveCustomersAsync(CancellationToken ct);
    Task BillAsync(int customerId, CancellationToken ct);
    Task SettleInTransactionAsync(int customerId, DateTime nowUtc, CancellationToken ct);
}
