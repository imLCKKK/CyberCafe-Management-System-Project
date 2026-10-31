using CyberManagement.API.ExceptionHandling;
using CyberManagement.API.Security;
using CyberManagement.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace CyberManagement.API;

public static class ApiDependencyInjection
{
    // Đăng ký phần HTTP và đọc claims; chưa chọn cơ chế đăng nhập hoặc tự cấp danh tính giả.
    public static IServiceCollection AddApiServices(this IServiceCollection services)
    {
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUser, HttpCurrentUser>();


        //services.AddAuthentication();
        //Dùng cái dưới khi chưa có cơ chế đăng nhập, để đọc claims từ cookie hoặc header Authorization.
        //Mở comment trên khi có cơ chế đăng nhập và cấp danh tính giả.
        services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme).AddCookie(options =>
        {
            options.Cookie.Name = "CyberManagement.Session";
            options.Cookie.HttpOnly = true;
            options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
            options.Cookie.SameSite = SameSiteMode.Strict;

            options.Events.OnRedirectToLogin = context =>
            {
                context.Response.StatusCode = 401;
                return Task.CompletedTask;
            };

            options.Events.OnRedirectToAccessDenied = context =>
            {
                context.Response.StatusCode = 403;
                return Task.CompletedTask;
            };
        });




        services.AddAuthorization(options => options.AddPolicy(ApiPolicies.ActiveAccount, policy =>
            policy.RequireAuthenticatedUser().RequireRole("Customer", "Staff", "Admin").RequireAssertion(context => HttpCurrentUser.HasActiveAccount(context.User))));
        services.AddSingleton<IAuthorizationMiddlewareResultHandler, ApiAuthorizationResultHandler>();
        services.AddExceptionHandler<ApiExceptionHandler>();
        services.AddProblemDetails(options => options.CustomizeProblemDetails = context =>
            context.ProblemDetails.Extensions.TryAdd("traceId", context.HttpContext.TraceIdentifier));
        return services;
    }
}
