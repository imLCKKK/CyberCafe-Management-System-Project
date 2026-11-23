using CyberManagement.Application.Interfaces.Repositories;
using CyberManagement.Application.Interfaces.Services;
using CyberManagement.Application.Services;
using CyberManagement.Infrastructure.Repositories;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CyberManagement.Infrastructure.Payments;

public static class ZaloPayGatewayRegistration
{
    public static IServiceCollection AddZaloPayGateway(this IServiceCollection services, IConfiguration configuration,
        string environmentName, bool enableWorker = true)
    {
        services.AddZaloPaySandboxConfiguration(configuration, environmentName);
        services.TryAddSingleton(TimeProvider.System);
        services.AddScoped<IZaloPayIntentRepository, ZaloPayIntentRepository>();
        services.AddScoped<IZaloPayPaymentService, ZaloPayPaymentService>();
        services.AddHttpClient<IZaloPayGateway, ZaloPaySandboxGateway>(client => client.Timeout = TimeSpan.FromSeconds(10))
            .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
            {
                AllowAutoRedirect = false, ConnectTimeout = TimeSpan.FromSeconds(5),
                PooledConnectionLifetime = TimeSpan.FromMinutes(5)
            });
        if (enableWorker) services.AddHostedService<ZaloPayIntentQueryWorker>();
        return services;
    }
}

internal sealed class ZaloPayIntentQueryWorker(IServiceScopeFactory scopes, ILogger<ZaloPayIntentQueryWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(15));
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                try
                {
                    await using var scope = scopes.CreateAsyncScope();
                    await scope.ServiceProvider.GetRequiredService<IZaloPayPaymentService>().ReconcileDueAsync(stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
                catch (Exception) { logger.LogWarning("ZaloPay intent query deferred; a later interval will retry."); }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
    }
}
