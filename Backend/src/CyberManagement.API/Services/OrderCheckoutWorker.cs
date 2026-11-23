using CyberManagement.Application.Interfaces.Repositories;
using CyberManagement.Application.Interfaces.Services;
using CyberManagement.Application.Services;

namespace CyberManagement.API.Services;

public sealed class OrderCheckoutWorker(IServiceScopeFactory scopes, ILogger<OrderCheckoutWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(15));
        try
        {
            do
            {
                try
                {
                    await using var scope = scopes.CreateAsyncScope();
                    var ids = await scope.ServiceProvider.GetRequiredService<IOrderRepository>()
                        .CheckoutWorkAsync(stoppingToken);
                    foreach (var id in ids)
                    {
                        try
                        {
                            await using var work = scopes.CreateAsyncScope();
                            await work.ServiceProvider.GetRequiredService<IOrderZaloPaySettlement>().ApplyAsync(id, stoppingToken);
                        }
                        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
                        catch (Exception) { logger.LogWarning("Order checkout {OrderId} deferred; saved state will be retried.", id); }
                    }
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
                catch (Exception) { logger.LogWarning("Order checkout scan deferred; a later interval will retry."); }
            } while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
    }
}

// HTTP recovery has its own loop so a slow provider cannot delay expiry or receipt settlement.
public sealed class OrderGatewayRecoveryWorker(IServiceScopeFactory scopes, ILogger<OrderGatewayRecoveryWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(15));
        try
        {
            do
            {
                try
                {
                    await using var scope = scopes.CreateAsyncScope();
                    var ids = await scope.ServiceProvider.GetRequiredService<IOrderRepository>()
                        .CheckoutWorkAsync(stoppingToken);
                    foreach (var id in ids)
                    {
                        try
                        {
                            await using var recovery = scopes.CreateAsyncScope();
                            await recovery.ServiceProvider.GetRequiredService<OrderService>().StartGatewayAsync(id, stoppingToken);
                        }
                        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
                        catch (Exception) { logger.LogWarning("Order gateway recovery {OrderId} deferred.", id); }
                    }
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
                catch (Exception) { logger.LogWarning("Order gateway recovery scan deferred."); }
            } while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
    }
}
