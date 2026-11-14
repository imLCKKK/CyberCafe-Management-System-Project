using System.Text;
using CyberManagement.API.Realtime;
using CyberManagement.Application.Interfaces.Services;
using CyberManagement.API.Security;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Tokens;

namespace CyberManagement.API;

public static class DependencyInjection
{
    public static IServiceCollection AddApiServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddProblemDetails();
        services.AddExceptionHandler<ExceptionHandling.ApiExceptionHandler>();
        services.AddSignalR();
        services.AddScoped<IRealtimePublisher, SignalRRealtimePublisher>();

        var signingKey = configuration["Authentication:Jwt:SigningKey"];
        if (string.IsNullOrWhiteSpace(signingKey) || Encoding.UTF8.GetByteCount(signingKey) < 32)
        {
            throw new InvalidOperationException(
                "Authentication:Jwt:SigningKey phải được cấu hình và có ít nhất 32 byte.");
        }

        var issuer = configuration["Authentication:Jwt:Issuer"] ?? "CyberManagement";
        var audience = configuration["Authentication:Jwt:Audience"] ?? "CyberManagement.Client";
        var tokenLifetimeMinutes = configuration.GetValue("Authentication:Jwt:TokenLifetimeMinutes", 20);
        if (tokenLifetimeMinutes is < 1 or > 30)
        {
            throw new InvalidOperationException(
                "Authentication:Jwt:TokenLifetimeMinutes must be between 1 and 30.");
        }

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey));

        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.MapInboundClaims = false;
                options.Events = new JwtBearerEvents
                {
                    OnMessageReceived = context =>
                    {
                        // Browser WebSockets/SSE cannot supply an Authorization header.
                        // Query-string tokens are accepted only on the exact hub transport endpoint.
                        if (context.Request.Path == RealtimeHub.Endpoint
                            && (context.HttpContext.WebSockets.IsWebSocketRequest
                                || context.Request.GetTypedHeaders().Accept?.Any(value => value.MediaType == "text/event-stream") == true)
                            && context.Request.Query.TryGetValue("access_token", out var token))
                            context.Token = token;
                        return Task.CompletedTask;
                    }
                };
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = issuer,
                    ValidateAudience = true,
                    ValidAudience = audience,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = key,
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromSeconds(30),
                    NameClaimType = "name",
                    RoleClaimType = "role"
                };
            });

        services.AddAuthorization(options =>
        {
            options.AddPolicy(ApiPolicies.RealtimeConnect, policy => policy
                .RequireAuthenticatedUser()
                .RequireAssertion(context => RealtimeGroups.FromPrincipal(context.User) is not null));
            options.FallbackPolicy = new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .Build();
        });
        services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
        services.AddScoped<IAuthorizationHandler, PermissionAuthorizationHandler>();

        return services;
    }
}
