using CyberManagement.Application.Interfaces.Services;
using CyberManagement.Application.Interfaces.Validation;
using CyberManagement.Application.Services;
using CyberManagement.Application.DTOs.Authentication;
using CyberManagement.Application.Validators.Authentication;
using Microsoft.Extensions.DependencyInjection;
namespace CyberManagement.Application
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddApplicationServices(this IServiceCollection services)
        {
            services.AddScoped<IIdentityService, IdentityService>();
            services.AddScoped<IIdentitySeeder, IdentitySeeder>();
            services.AddScoped<ICustomerIdentityService, CustomerIdentityService>();
            services.AddScoped<IComputerService, ComputerService>();
            services.AddScoped<CatalogService>();
            services.AddScoped<ICustomerService, CustomerService>();
            services.AddScoped<ITournamentService, TournamentService>();
            services.AddScoped<ITeamBalancingService, TeamBalancingService>();
            services.AddScoped<CounterTopUpService>();
            services.AddScoped<IVoucherService, VoucherService>();
            services.AddScoped<IRequestValidator<LoginCommand>, LoginCommandValidator>();
            services.AddScoped<IRequestValidator<CustomerLoginCommand>, CustomerLoginCommandValidator>();
            services.AddScoped<IInventoryService, InventoryService>();
            services.AddScoped<InvoiceService>();
            services.AddScoped<IInvoiceService>(provider => provider.GetRequiredService<InvoiceService>());
            services.AddScoped<ISessionInvoiceIssuer>(provider => provider.GetRequiredService<InvoiceService>());
            return services;
        }
    }
}
