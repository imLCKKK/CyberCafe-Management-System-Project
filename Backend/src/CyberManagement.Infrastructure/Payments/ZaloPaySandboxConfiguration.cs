using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CyberManagement.Infrastructure.Payments;

public static class ZaloPaySandboxConfiguration
{
    public static IServiceCollection AddZaloPaySandboxConfiguration(
        this IServiceCollection services, IConfiguration configuration, string environmentName)
    {
        services.AddOptions<ZaloPaySandboxOptions>()
            .Bind(configuration.GetSection(ZaloPaySandboxOptions.SectionName))
            .Validate(options => !options.Enabled || IsSandboxEnvironment(environmentName),
                "ZaloPay sandbox may only be enabled in Development, Testing or Test.")
            .Validate(options => !options.Enabled || (options.AppId > 0
                && !string.IsNullOrWhiteSpace(options.Key1) && !string.IsNullOrWhiteSpace(options.Key2)),
                "Enabled ZaloPay sandbox requires a positive AppId and both signing keys.")
            .Validate(options => !options.Enabled || IsCallbackUrl(options.CallbackUrl),
                "ZaloPay callback requires a public DNS HTTPS URL on port 443 at /api/payments/zalopay/callback, without user information, query or fragment.")
            .Validate(options => !options.Enabled || IsRedirectUrl(options.RedirectUrl),
                "ZaloPay redirect requires HTTPS or loopback HTTP, without user information, query or fragment.")
            .ValidateOnStart();
        return services;
    }

    private static bool IsSandboxEnvironment(string name) =>
        name.Equals("Development", StringComparison.OrdinalIgnoreCase)
        || name.Equals("Testing", StringComparison.OrdinalIgnoreCase)
        || name.Equals("Test", StringComparison.OrdinalIgnoreCase);

    private static bool IsCallbackUrl(string value)
    {
        if (!TryGetUrl(value, out var uri) || uri!.Scheme != Uri.UriSchemeHttps || uri.Port != 443
            || uri.AbsolutePath != ZaloPaySandboxOptions.CallbackPath || uri.IsLoopback
            || uri.HostNameType != UriHostNameType.Dns)
            return false;
        var host = uri.IdnHost.TrimEnd('.');
        // This checks the address form; external reachability must be verified separately.
        return host.Contains('.') && host != "localhost"
            && !host.EndsWith(".localhost", StringComparison.OrdinalIgnoreCase)
            && !host.EndsWith(".local", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsRedirectUrl(string value) => TryGetUrl(value, out var uri)
        && (uri!.Scheme == Uri.UriSchemeHttps || (uri.Scheme == Uri.UriSchemeHttp && uri.IsLoopback));

    private static bool TryGetUrl(string value, out Uri? uri) =>
        Uri.TryCreate(value, UriKind.Absolute, out uri)
        && string.IsNullOrEmpty(uri.UserInfo) && string.IsNullOrEmpty(uri.Query) && string.IsNullOrEmpty(uri.Fragment);
}
