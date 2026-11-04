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
            services.AddScoped<IRequestValidator<LoginCommand>, LoginCommandValidator>();
            services.AddScoped<IRequestValidator<CustomerLoginCommand>, CustomerLoginCommandValidator>();
            return services;
        }
    }
}
