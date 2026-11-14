using System.Text.Json;
using CyberManagement.Infrastructure.Payments;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

// Test assembly only. This listener checks transport/signatures and cannot settle a payment.
internal static class ZaloPayCallbackProbeHost
{
    public const string CallbackPath = "/api/payments/zalopay/callback";

    public static async Task RunAsync()
    {
        var localUrl = Environment.GetEnvironmentVariable("ZALOPAY_PROBE_URL") ?? "http://127.0.0.1:5090";
        if (!Uri.TryCreate(localUrl, UriKind.Absolute, out var address)
            || address.Scheme != "http" || !address.IsLoopback || address.AbsolutePath != "/"
            || address.UserInfo.Length != 0 || address.Query.Length != 0 || address.Fragment.Length != 0)
            throw new InvalidOperationException("The S1 probe must listen on loopback HTTP only.");

        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = "Development",
            ApplicationName = typeof(CyberManagement.API.Program).Assembly.FullName
        });
        builder.Configuration.AddUserSecrets(typeof(CyberManagement.API.Program).Assembly, optional: true, reloadOnChange: true)
            .AddEnvironmentVariables();
        builder.Logging.ClearProviders();
        builder.WebHost.UseUrls(localUrl);
        builder.WebHost.ConfigureKestrel(server => server.Limits.MaxRequestBodySize = 131072);
        builder.Services.AddZaloPaySandboxConfiguration(builder.Configuration, builder.Environment.EnvironmentName);
        await using var app = builder.Build();
        app.Use(async (context, next) =>
        {
            context.Response.Headers.CacheControl = "no-store";
            await next(context);
        });
        app.MapGet(CallbackPath, (IOptionsMonitor<ZaloPaySandboxOptions> settings) => Results.Ok(new
        {
            service = "CyberManagement.ZaloPay.S1Probe",
            probeOnly = true,
            configured = settings.CurrentValue.Enabled,
            businessStateChanged = false,
            expectedCallbackMethod = "POST"
        }));
        app.MapPost(CallbackPath, async (HttpRequest request, IOptionsMonitor<ZaloPaySandboxOptions> settings) =>
        {
            var options = settings.CurrentValue;
            if (!options.Enabled)
                return Results.Json(new { probeOnly = true, error = "Sandbox configuration is disabled.", businessStateChanged = false }, statusCode: 503);
            try
            {
                var callback = await request.ReadFromJsonAsync<ProbeCallback>(request.HttpContext.RequestAborted);
                if (callback is null || callback.Type != 1
                    || !ZaloPaySignatures.VerifyCallback(callback.Data, callback.Mac, options.Key2))
                    return Results.Json(new { probeOnly = true, signatureValid = false, businessStateChanged = false }, statusCode: 400);
                using var payload = JsonDocument.Parse(callback.Data);
                if (payload.RootElement.ValueKind != JsonValueKind.Object
                    || !payload.RootElement.TryGetProperty("app_id", out var appId)
                    || !appId.TryGetInt32(out var actualAppId) || actualAppId != options.AppId)
                    return Results.Json(new { probeOnly = true, signatureValid = false, businessStateChanged = false }, statusCode: 400);
                // Deliberately no return_code=1: no persisted payment/ledger/settlement exists in this probe.
                return Results.Ok(new
                {
                    probeOnly = true,
                    signatureValid = true,
                    businessStateChanged = false,
                    providerAcknowledged = false
                });
            }
            catch (Exception error) when (error is JsonException or BadHttpRequestException or InvalidOperationException)
            {
                return Results.Json(new { probeOnly = true, error = "Invalid probe payload.", businessStateChanged = false }, statusCode: 400);
            }
        });
        await app.StartAsync();
        Console.WriteLine(JsonSerializer.Serialize(new { type = "ready", probeOnly = true, url = app.Urls.Single(), callbackPath = CallbackPath }));
        await app.WaitForShutdownAsync();
    }

    private sealed record ProbeCallback(string Data, string Mac, int Type);
}
