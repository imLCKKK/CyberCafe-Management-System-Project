using CyberManagement.Application.Exceptions;
using CyberManagement.Application.Interfaces.Repositories;
using CyberManagement.Application.Interfaces.Services;
using CyberManagement.Application.Services;
using CyberManagement.Infrastructure.Persistance;
using CyberManagement.Infrastructure.Repositories;
using CyberManagement.Infrastructure.Security;
using CyberManagement.Infrastructure.TeamBalancing;
using CyberManagement.Application.Wallets;
using CyberManagement.Infrastructure.Wallets;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CyberManagement.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructureServices(
        this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "Configure ConnectionStrings:DefaultConnection with User Secrets or an environment variable for the 35-table database.");
        }

        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseSqlServer(connectionString, b =>
                b.MigrationsAssembly(typeof(ApplicationDbContext).Assembly.FullName)));
        services.AddScoped<IIdentityRepository, IdentityRepository>();
        services.AddScoped<IInventoryQueries, InventoryQueries>();
        services.AddScoped<IInventoryRepository, InventoryRepository>();
        services.AddScoped<IInventoryMovementRepository,InventoryMovementRepository>();
        services.AddScoped<IInventoryUnitOfWork, InventoryUnitOfWork>();
        services.AddScoped<ICatalogRepository, CatalogRepository>();
        services.AddScoped<ICustomerIdentityRepository, CustomerIdentityRepository>();
        services.AddScoped<IComputerRepository, ComputerRepository>();
        services.AddScoped<ICustomerRepository, CustomerRepository>();
        services.AddScoped<ITournamentRepository, TournamentRepository>();
        services.AddScoped<ITeamPreviewRosterReader, TeamPreviewRosterReader>();
        services.AddSingleton<ITeamBalancingService>(provider =>
        {
            var logger = provider.GetService<ILogger<TeamBalancingService>>();
            var directory = configuration["TeamBalancing:ModelDirectory"];
            var hash = configuration["TeamBalancing:ManifestSha256"];
            if (string.IsNullOrWhiteSpace(directory) || string.IsNullOrWhiteSpace(hash))
            {
                logger?.LogWarning("Team balancing ML is unavailable. Configure TeamBalancing:ModelDirectory and TeamBalancing:ManifestSha256.");
                return new TeamBalancingService(null);
            }

            try
            {
                var artifact = TeamBalanceModelArtifact.Load(directory, hash);
                logger?.LogInformation("Loaded team balancing model {ModelVersion} from dataset {DatasetVersion}.",
                    artifact.ModelVersion, artifact.DatasetVersion);
                return new TeamBalancingService(artifact);
            }
            catch (TeamBalanceException error) when (error.Code == "MODEL_UNAVAILABLE")
            {
                logger?.LogWarning("Team balancing ML is unavailable. The configured artifact could not be loaded: {Reason}", error.Message);
                return new TeamBalancingService(null);
            }
        });
        services.AddScoped<ICounterTopUpRepository, CounterTopUpRepository>();
        services.AddScoped<IWalletLedgerWriter, WalletLedgerWriter>();
        services.AddScoped<ISessionSettlement, SessionSettlement>();
        services.AddScoped<IEmployeePasswordHasher, EmployeePasswordHasher>();
        services.AddScoped<ICustomerPasswordHasher, CustomerPasswordHasher>();
        services.AddScoped<IVoucherRepository, VoucherRepository>();
        services.AddScoped<IPromotionUsageRepository, PromotionUsageRepository>();
        services.AddScoped<IVoucherUnitOfWork, VoucherUnitOfWork>();

        services.AddScoped<IInvoiceRepository, InvoiceRepository>();
        services.AddScoped<IInvoiceUnitOfWork, InvoiceUnitOfWork>();
        return services;
    }
}
