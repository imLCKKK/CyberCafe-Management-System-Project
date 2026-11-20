using System.Globalization;
using System.Net;
using System.Text.Json;
using CyberManagement.Application.DTOs.Payments;
using CyberManagement.Application.Interfaces.Services;
using Microsoft.Extensions.Options;

namespace CyberManagement.Infrastructure.Payments;

// Protocol adapter only. Receipt ownership and atomic wallet settlement belong to the service/repository.
public sealed class ZaloPaySandboxGateway(HttpClient client, IOptionsMonitor<ZaloPaySandboxOptions> settings) : IZaloPayGateway
{
    private const int MaximumResponseBytes = 131_072;
    private static readonly TimeSpan VietnamOffset = TimeSpan.FromHours(7);
    private static readonly HashSet<string> CheckoutHosts = new(StringComparer.OrdinalIgnoreCase)
    {
        "sbgateway.zalopay.vn", "sbcheckout.zalopay.vn", "qcgateway.zalopay.vn"
    };

    public bool Enabled => settings.CurrentValue.Enabled;
    public int AppId => settings.CurrentValue.AppId;

    public async Task<GatewayCreateResult> CreateAsync(GatewayCreateRequest intent, CancellationToken cancellationToken)
    {
        var options = settings.CurrentValue;
        if (!options.Enabled) return new(null, false, "CreateUnavailable");
        if (intent.AppId != options.AppId) return new(null, false, "AppChanged");
        var merchantId = intent.MerchantTransactionId;
        var appTime = intent.CreatedAtUtc.ToUnixTimeMilliseconds();
        var lifetime = intent.ExpiresAtUtc - intent.CreatedAtUtc;
        if (intent.IntentId <= 0 || intent.CustomerId <= 0 || intent.AmountVnd is < 1 or > 999_999_999
            || intent.AmountVnd != decimal.Truncate(intent.AmountVnd) || !IsMerchantId(merchantId, appTime)
            || intent.CreatedAtUtc > DateTimeOffset.UtcNow.AddMinutes(5)
            || intent.ExpiresAtUtc <= DateTimeOffset.UtcNow
            || !(intent.Purpose == "TopUp" && intent.OrderId is null && lifetime == TimeSpan.FromMinutes(15)
                || intent.Purpose == "Order" && intent.OrderId > 0
                    && lifetime >= TimeSpan.FromMinutes(5) && lifetime <= TimeSpan.FromMinutes(15)))
            return new(null, false, "CreateUnavailable");

        try
        {
            var redirect = new Uri(options.RedirectUrl, UriKind.Absolute);
            var redirectUrl = new Uri(redirect, intent.Purpose == "TopUp" ? "/client/wallet" : "/client/ordering").AbsoluteUri;
            var embedData = JsonSerializer.Serialize(new
            {
                redirecturl = redirectUrl,
                preferred_payment_method = new[] { "zalopay_wallet" },
                purpose = intent.Purpose,
                intentId = intent.IntentId,
                sourceId = intent.OrderId ?? intent.IntentId
            });
            const string item = "[]";
            var appUser = "customer:" + intent.CustomerId.ToString(CultureInfo.InvariantCulture);
            var amount = decimal.ToInt64(intent.AmountVnd);
            var mac = ZaloPaySignatures.Create(options.AppId, merchantId!, appUser, amount,
                appTime, embedData, item, options.Key1);
            using var document = await PostAsync(ZaloPaySandboxOptions.CreateEndpoint, new()
            {
                ["app_id"] = options.AppId.ToString(CultureInfo.InvariantCulture),
                ["app_user"] = appUser,
                ["app_trans_id"] = merchantId!,
                ["app_time"] = appTime.ToString(CultureInfo.InvariantCulture),
                ["amount"] = amount.ToString(CultureInfo.InvariantCulture),
                ["description"] = "CyberManagement " + intent.Purpose + " #" + (intent.OrderId ?? intent.IntentId).ToString(CultureInfo.InvariantCulture),
                ["callback_url"] = options.CallbackUrl,
                ["embed_data"] = embedData,
                ["item"] = item,
                ["bank_code"] = "",
                ["expire_duration_seconds"] = ((long)Math.Floor(lifetime.TotalSeconds)).ToString(CultureInfo.InvariantCulture),
                ["mac"] = mac
            }, cancellationToken);
            if (document is null) return new(null, false, "CreateUnavailable");
            var root = document.RootElement;
            if (!TryInt(root, "return_code", out var code)) return new(null, false, "CreateUnavailable");
            if (code == 1 && TryText(root, "order_url", 2048, out var url) && IsCheckoutUrl(url))
                return new(url, false, null);
            // Duplicate IDs, rate limits and system failures do not prove that no order exists.
            if (code == 2 && TryInt(root, "sub_return_code", out var subCode) && subCode is -401 or -402)
                return new(null, true, "ProviderRejected");
            return new(null, false, "CreateUnavailable");
        }
        catch (Exception error) when (error is ArgumentException or OverflowException)
        {
            return new(null, false, "CreateUnavailable");
        }
    }

    public async Task<GatewayQueryResult> QueryAsync(int appId, string merchantTransactionId, CancellationToken cancellationToken)
    {
        var options = settings.CurrentValue;
        if (!options.Enabled) return new("Unavailable", null, "QueryUnavailable");
        if (appId != options.AppId) return new("Unavailable", null, "AppChanged");
        try
        {
            using var document = await PostAsync(ZaloPaySandboxOptions.QueryEndpoint, new()
            {
                ["app_id"] = appId.ToString(CultureInfo.InvariantCulture),
                ["app_trans_id"] = merchantTransactionId,
                ["mac"] = ZaloPaySignatures.Query(appId, merchantTransactionId, options.Key1)
            }, cancellationToken);
            if (document is null) return new("Unavailable", null, "QueryUnavailable");
            var root = document.RootElement;
            if (!TryInt(root, "return_code", out var code)) return new("Unavailable", null, "QueryUnavailable");
            var hasProcessing = root.TryGetProperty("is_processing", out var processing);
            if (hasProcessing && processing.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
                return new("Unavailable", null, "QueryUnavailable");
            // is_processing is reserved for ZaloPay's internal use. The live Sandbox returns
            // it as true even with return_code=2/sub_return_code=-54 (expired).
            // Resolve from the documented result codes; success still requires a valid receipt.
            if (code == 3)
                return new("Pending", null, null);
            if (code == 1)
            {
                if (!TryAmount(root, out var amount) || !TryTransactionId(root, out var transactionId))
                    return new("Unavailable", null, "QueryUnavailable");
                // Query server_time is query processing time, not proof of the original payment time.
                return new("Succeeded", new(appId, transactionId, amount, null), null);
            }
            if (code == 2 && TryInt(root, "sub_return_code", out var subCode))
            {
                if (subCode == -101) return new("NotFound", null, null);
                if (subCode == -54) return new("Expired", null, null);
                if (subCode is -63 or -217 or -332 or -333 or -1330 or -1331 or -1332 or -1333
                    or -1340 or -1341 or -1342 or -1343)
                    return new("Pending", null, "PaymentAttemptFailed", true);
            }
            return new("Unavailable", null, "QueryUnavailable");
        }
        catch (ArgumentException) { return new("Unavailable", null, "QueryUnavailable"); }
    }

    public GatewayCallback? ReadCallback(string data, string mac, int type)
    {
        var options = settings.CurrentValue;
        if (!options.Enabled || type != 1 || !ZaloPaySignatures.VerifyCallback(data, mac, options.Key2)) return null;
        try
        {
            using var document = JsonDocument.Parse(data, new JsonDocumentOptions { MaxDepth = 16 });
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object
                || root.EnumerateObject().Select(property => property.Name).Distinct(StringComparer.Ordinal).Count() != root.GetPropertyCount()
                || !TryInt(root, "app_id", out var appId) || appId <= 0 || appId != options.AppId
                || !TryLong(root, "app_time", out var appTime) || appTime <= 0
                || !TryLong(root, "server_time", out var serverTime) || serverTime <= 0
                || !TryText(root, "app_trans_id", 40, out var merchantId) || !IsMerchantId(merchantId, appTime)
                || !TryText(root, "app_user", 50, out var appUser) || appUser.Contains('|')
                || !TryAmount(root, out var amount) || !TryTransactionId(root, out var transactionId)) return null;
            var created = DateTimeOffset.FromUnixTimeMilliseconds(appTime);
            var occurred = DateTimeOffset.FromUnixTimeMilliseconds(serverTime);
            var latestAllowed = DateTimeOffset.UtcNow.AddMinutes(5);
            if (created > latestAllowed || occurred > latestAllowed || occurred < created.AddMinutes(-5)) return null;
            return new(appId, merchantId, appUser, appTime, amount, transactionId, occurred);
        }
        catch (Exception error) when (error is JsonException or ArgumentException or InvalidOperationException or OverflowException)
        {
            return null;
        }
    }

    private async Task<JsonDocument?> PostAsync(string endpoint, Dictionary<string, string> values, CancellationToken cancellationToken)
    {
        try
        {
            // ResponseHeadersRead ends HttpClient's timeout at the headers; bound the body reads as well.
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            deadline.CancelAfter(TimeSpan.FromSeconds(10));
            using var request = new HttpRequestMessage(HttpMethod.Post, endpoint) { Content = new FormUrlEncodedContent(values) };
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, deadline.Token);
            if (response.StatusCode != HttpStatusCode.OK || response.Content.Headers.ContentLength > MaximumResponseBytes)
                return null;
            var mediaType = response.Content.Headers.ContentType?.MediaType;
            if (mediaType is null || (!mediaType.Equals("application/json", StringComparison.OrdinalIgnoreCase)
                && !mediaType.EndsWith("+json", StringComparison.OrdinalIgnoreCase))) return null;
            await using var stream = await response.Content.ReadAsStreamAsync(deadline.Token);
            using var buffer = new MemoryStream();
            var chunk = new byte[8192];
            int read;
            while ((read = await stream.ReadAsync(chunk, deadline.Token)) != 0)
            {
                if (buffer.Length + read > MaximumResponseBytes) return null;
                buffer.Write(chunk, 0, read);
            }
            var document = JsonDocument.Parse(buffer.ToArray(), new JsonDocumentOptions { MaxDepth = 16 });
            if (document.RootElement.ValueKind == JsonValueKind.Object
                && document.RootElement.EnumerateObject().Select(property => property.Name).Distinct(StringComparer.Ordinal).Count()
                    == document.RootElement.GetPropertyCount()) return document;
            document.Dispose();
            return null;
        }
        catch (Exception error) when (error is HttpRequestException or IOException or JsonException)
        {
            return null;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested) { return null; }
    }

    private static bool IsCheckoutUrl(string value) => Uri.TryCreate(value, UriKind.Absolute, out var uri)
        && uri.Scheme == Uri.UriSchemeHttps && uri.Port == 443 && CheckoutHosts.Contains(uri.IdnHost)
        && uri.UserInfo.Length == 0 && uri.Fragment.Length == 0;

    private static bool IsMerchantId(string? value, long appTime)
    {
        if (value is null || value.Length != 39 || value[6] != '_'
            || value.AsSpan(0, 6).ContainsAnyExceptInRange('0', '9')
            || value.AsSpan(7).ContainsAnyExcept("0123456789abcdefABCDEF")) return false;
        try
        {
            return value.AsSpan(0, 6).SequenceEqual(DateTimeOffset.FromUnixTimeMilliseconds(appTime)
                .ToOffset(VietnamOffset).ToString("yyMMdd", CultureInfo.InvariantCulture));
        }
        catch (ArgumentOutOfRangeException) { return false; }
    }

    private static bool TryText(JsonElement root, string name, int maximumLength, out string value)
    {
        value = "";
        if (!root.TryGetProperty(name, out var element) || element.ValueKind != JsonValueKind.String) return false;
        value = element.GetString() ?? "";
        return !string.IsNullOrWhiteSpace(value) && value.Length <= maximumLength;
    }

    private static bool TryInt(JsonElement root, string name, out int value)
    {
        value = 0;
        return root.TryGetProperty(name, out var element) && element.ValueKind == JsonValueKind.Number && element.TryGetInt32(out value);
    }

    private static bool TryLong(JsonElement root, string name, out long value)
    {
        value = 0;
        return root.TryGetProperty(name, out var element) && element.ValueKind == JsonValueKind.Number && element.TryGetInt64(out value);
    }

    private static bool TryAmount(JsonElement root, out decimal amount)
    {
        amount = 0;
        if (!TryLong(root, "amount", out var value) || value is < 1 or > 999_999_999) return false;
        amount = value;
        return true;
    }

    private static bool TryTransactionId(JsonElement root, out string transactionId)
    {
        transactionId = "";
        if (!root.TryGetProperty("zp_trans_id", out var element)) return false;
        var raw = element.ValueKind switch
        {
            JsonValueKind.Number => element.GetRawText(),
            JsonValueKind.String => element.GetString(),
            _ => null
        };
        if (raw is null || raw.Length is < 1 or > 19 || raw.AsSpan().ContainsAnyExceptInRange('0', '9')
            || !long.TryParse(raw, NumberStyles.None, CultureInfo.InvariantCulture, out var value) || value <= 0) return false;
        transactionId = value.ToString(CultureInfo.InvariantCulture);
        return true;
    }
}
