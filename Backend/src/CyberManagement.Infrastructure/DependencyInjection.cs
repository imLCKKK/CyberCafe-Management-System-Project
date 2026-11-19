using CyberManagement.Application.Interfaces.Repositories;
using CyberManagement.Application.Interfaces.Services;
using CyberManagement.Infrastructure.Persistance;
using CyberManagement.Infrastructure.Repositories;
using CyberManagement.Infrastructure.Security;
using CyberManagement.Application.Wallets;
using CyberManagement.Infrastructure.Wallets;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

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
        services.AddScoped<IOrderRepository, OrderRepository>();
        services.AddScoped<ICustomerIdentityRepository, CustomerIdentityRepository>();
        services.AddScoped<IComputerRepository, ComputerRepository>();
        services.AddScoped<ICustomerRepository, CustomerRepository>();
        services.AddScoped<ITournamentRepository, TournamentRepository>();
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
