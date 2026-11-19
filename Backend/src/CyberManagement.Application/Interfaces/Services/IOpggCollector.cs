namespace CyberManagement.Application.Interfaces.Services;

public interface IOpggCollector
{
    Task<string> CaptureAsync(string riotId, CancellationToken token);
}
