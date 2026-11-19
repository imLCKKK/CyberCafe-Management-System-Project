using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;
using CyberManagement.Application.DTOs.Computers;
using CyberManagement.Application.Interfaces.Repositories;
using CyberManagement.Application.Interfaces.Services;

namespace CyberManagement.Application.Services;

public sealed class SessionService(ISessionRepository repository) : ISessionService
{
    public Task<IReadOnlyList<SessionDto>> GetSessionsAsync(CancellationToken ct) => repository.GetSessionsAsync(ct);
    public Task<SessionHomeDto> GetHomeAsync(int customerId, CancellationToken ct) => repository.GetHomeAsync(customerId, ct);

    public Task<SessionDto> OpenAsync(OpenSessionCommand command, int employeeId, string key, CancellationToken ct)
    {
        ValidateKey(key);
        Validator.ValidateObject(command, new ValidationContext(command), true);
        return repository.OpenAsync(command, employeeId, key, ct);
    }

    public Task<SessionDto> CloseAsync(int sessionId, int employeeId, string key, CancellationToken ct)
    {
        ValidateKey(key);
        if (sessionId <= 0) throw new ValidationException("Mã phiên không hợp lệ.");
        return repository.CloseAsync(sessionId, employeeId, key, ct);
    }

    private static void ValidateKey(string key)
    {
        if (key is null || !Regex.IsMatch(key, "\\A[A-Za-z0-9:_-]{1,80}\\z")
            || key.StartsWith("SessionAutoClose:", StringComparison.OrdinalIgnoreCase)
            || key.StartsWith("SessionAutoInvoice:", StringComparison.OrdinalIgnoreCase))
            throw new ValidationException("Idempotency-Key không hợp lệ hoặc dành riêng cho hệ thống.");
    }
}
