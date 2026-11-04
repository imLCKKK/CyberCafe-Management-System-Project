using CyberManagement.Application.Interfaces.Services;
using CyberManagement.Application.Interfaces.Validation;
using CyberManagement.Application.Services;
using CyberManagement.Application.DTOs.Authentication;
using CyberManagement.Application.Validators.Authentication;
using CyberManagement.Application.DTOs.Customers;
using CyberManagement.Application.Validators.Customers;
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
            services.AddScoped<ICustomerService, CustomerService>();
            services.AddScoped<IClientAccountService, ClientAccountService>();
            services.AddScoped<IRequestValidator<CreateCustomerCommand>, CreateCustomerCommandValidator>();
            services.AddScoped<IRequestValidator<UpdateCustomerCommand>, UpdateCustomerCommandValidator>();
            services.AddScoped<IRequestValidator<ChangeCustomerStatusCommand>, ChangeCustomerStatusCommandValidator>();
            services.AddScoped<IRequestValidator<StaffTopUpCommand>, StaffTopUpCommandValidator>();
            return services;
        }
    }
}
