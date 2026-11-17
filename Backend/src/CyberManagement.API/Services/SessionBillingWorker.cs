using CyberManagement.Application.Interfaces.Repositories;

namespace CyberManagement.API.Services;

public sealed class SessionBillingWorker(IServiceScopeFactory scopes, ILogger<SessionBillingWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(10));
        do
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                var customers = await scope.ServiceProvider.GetRequiredService<ISessionRepository>().GetActiveCustomersAsync(stoppingToken);
                foreach (var id in customers)
                {
                    try
                    {
                        await using var billingScope = scopes.CreateAsyncScope();
                        await billingScope.ServiceProvider.GetRequiredService<ISessionRepository>().BillAsync(id, stoppingToken);
                    }
                    catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
                    catch (Exception error) { logger.LogError(error, "Session billing failed for customer {CustomerId}", id); }
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
            catch (Exception error) { logger.LogError(error, "Could not read sessions for billing"); }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
