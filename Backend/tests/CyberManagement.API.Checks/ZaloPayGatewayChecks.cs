using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CyberManagement.Application.DTOs.Payments;
using CyberManagement.Infrastructure.Payments;
using Microsoft.Extensions.Options;

internal static class ZaloPayGatewayChecks
{
    private const string Key1 = "gateway-fixture-key1-not-merchant";
    private const string Key2 = "gateway-fixture-key2-not-merchant";

    public static async Task<int> RunAsync()
    {
        var checks = 0;
        var options = Options();
        var source = Source(DateTimeOffset.UtcNow.AddSeconds(-2));
        Dictionary<string, string>? sent = null;
        Uri? sentUri = null;
        using (var client = new HttpClient(new Handler(async (request, token) =>
        {
            sentUri = request.RequestUri;
            Check("Outbound request uses POST with a bounded cancellation token", request.Method == HttpMethod.Post && token.CanBeCanceled);
            Check("Provider request is form-urlencoded", request.Content?.Headers.ContentType?.MediaType == "application/x-www-form-urlencoded");
            sent = Form(await request.Content!.ReadAsStringAsync(token));
            return Json("{\"return_code\":1,\"order_url\":\"https://sbgateway.zalopay.vn/pay?order=fixture\"}");
        })))
        {
            var result = await new ZaloPaySandboxGateway(client, new Monitor(options)).CreateAsync(source, default);
            Check("Create accepts an exact sandbox checkout host", result.PaymentUrl == "https://sbgateway.zalopay.vn/pay?order=fixture" && !result.Rejected);
        }
        Check("Create uses only the fixed sandbox endpoint", sentUri?.AbsoluteUri == ZaloPaySandboxOptions.CreateEndpoint);
        Check("Create uses saved source identity, amount and time", sent!["app_trans_id"] == source.MerchantTransactionId
            && sent["amount"] == "35000" && sent["app_user"] == "customer:17"
            && sent["app_time"] == source.CreatedAtUtc.ToUnixTimeMilliseconds().ToString(CultureInfo.InvariantCulture));
        using (var embed = JsonDocument.Parse(sent!["embed_data"]))
            Check("Create sends wallet checkout, saved source metadata and configured return URL",
                embed.RootElement.GetProperty("purpose").GetString() == "TopUp"
                && embed.RootElement.GetProperty("intentId").GetInt32() == 41
                && embed.RootElement.GetProperty("preferred_payment_method")[0].GetString() == "zalopay_wallet"
                && embed.RootElement.GetProperty("redirecturl").GetString() == "http://127.0.0.1:5175/client/wallet");
        var independentlySignedInput = string.Join('|', sent!["app_id"], sent["app_trans_id"], sent["app_user"],
            sent["amount"], sent["app_time"], sent["embed_data"], sent["item"]);
        Check("Create MAC covers the exact encoded JSON values that were sent", sent["mac"] == Mac(Key1, independentlySignedInput)
            && sent["item"] == "[]" && sent["expire_duration_seconds"] == "900" && sent["callback_url"] == options.CallbackUrl);

        var orderSource = source with { Purpose = "Order", OrderId = 73 };
        using (var client = new HttpClient(new Handler(async (request, token) =>
        {
            sent = Form(await request.Content!.ReadAsStringAsync(token));
            return Json("{\"return_code\":1,\"order_url\":\"https://qcgateway.zalopay.vn/openinapp?order=opaque%2Btoken%3D\"}");
        })))
        {
            var result = await new ZaloPaySandboxGateway(client, new Monitor(options)).CreateAsync(orderSource, default);
            Check("Order uses the same adapter and preserves the opaque checkout URL", result.PaymentUrl == "https://qcgateway.zalopay.vn/openinapp?order=opaque%2Btoken%3D" && !result.Rejected);
        }
        using (var embed = JsonDocument.Parse(sent!["embed_data"]))
            Check("Order metadata binds the saved intent and order with a server-selected return path",
                embed.RootElement.GetProperty("purpose").GetString() == "Order"
                && embed.RootElement.GetProperty("intentId").GetInt32() == 41
                && embed.RootElement.GetProperty("sourceId").GetInt32() == 73
                && embed.RootElement.GetProperty("redirecturl").GetString() == "http://127.0.0.1:5175/client/ordering");
        Check("Order signs its exact source metadata using the same protocol",
            sent!["mac"] == Mac(Key1, string.Join('|', sent["app_id"], sent["app_trans_id"], sent["app_user"], sent["amount"], sent["app_time"], sent["embed_data"], sent["item"]))
            && sent["amount"] == "35000" && sent["app_user"] == "customer:17" && sent["description"] == "CyberManagement Order #73");

        using (var client = new HttpClient(new Handler(async (request, token) =>
        {
            sent = Form(await request.Content!.ReadAsStringAsync(token));
            return Json("{\"return_code\":1,\"order_url\":\"https://sbgateway.zalopay.vn/pay?order=fixture\"}");
        })))
        {
            var result = await new ZaloPaySandboxGateway(client, new Monitor(options)).CreateAsync(
                orderSource with { ExpiresAtUtc = orderSource.CreatedAtUtc.AddMinutes(10) }, default);
            Check("Order uses its remaining saved deadline without starting a new fifteen-minute window",
                result.PaymentUrl is not null && sent!["expire_duration_seconds"] == "600");
        }

        using (var client = new HttpClient(new Handler((_, _) => throw new InvalidOperationException("Invalid server input must not call HTTP"))))
        {
            var gateway = new ZaloPaySandboxGateway(client, new Monitor(options));
            foreach (var invalid in new[]
            {
                source with { IntentId = 0 }, source with { CustomerId = 0 }, source with { AmountVnd = 0 },
                source with { AmountVnd = 999_999_999.5m }, source with { AmountVnd = 35000.5m },
                source with { Purpose = "Other" }, source with { OrderId = 73 }, source with { Purpose = "Order" },
                source with { Purpose = "Order", OrderId = 0 }, source with { MerchantTransactionId = "wrong-reference" },
                source with { ExpiresAtUtc = source.CreatedAtUtc.AddMinutes(14) },
                source with { ExpiresAtUtc = source.CreatedAtUtc.AddMinutes(16) },
                orderSource with { ExpiresAtUtc = source.CreatedAtUtc.AddSeconds(299) },
                orderSource with { ExpiresAtUtc = source.CreatedAtUtc.AddSeconds(901) },
                Source(DateTimeOffset.UtcNow.AddMinutes(-16)), Source(DateTimeOffset.UtcNow.AddDays(1))
            })
                Check("Invalid amount, source, reference or expiry cannot create a provider order", (await gateway.CreateAsync(invalid, default)).PaymentUrl is null);
            Check("Changed merchant cannot create using an old intent", (await gateway.CreateAsync(source with { AppId = 99 }, default)).FailureReason == "AppChanged");
        }

        foreach (var purpose in new[] { "TopUp", "Order" })
        {
            var configured = Options();
            configured.RedirectUrl = "https://merchant.example/ignored/configured/path";
            using var client = new HttpClient(new Handler(async (request, token) =>
            {
                sent = Form(await request.Content!.ReadAsStringAsync(token));
                return Json("{\"return_code\":1,\"order_url\":\"https://sbcheckout.zalopay.vn/pay?order=fixture\"}");
            }));
            await new ZaloPaySandboxGateway(client, new Monitor(configured)).CreateAsync(
                source with { Purpose = purpose, OrderId = purpose == "Order" ? 73 : null }, default);
            using var embed = JsonDocument.Parse(sent!["embed_data"]);
            Check("Redirect uses only configured origin and a purpose-selected route",
                embed.RootElement.GetProperty("redirecturl").GetString() == "https://merchant.example/client/" + (purpose == "TopUp" ? "wallet" : "ordering"));
        }

        foreach (var url in new[]
        {
            "https://sbgateway.zalopay.vn.evil.example/pay", "https://zalopay.vn/pay", "http://sbgateway.zalopay.vn/pay",
            "https://user@sbgateway.zalopay.vn/pay", "https://sbgateway.zalopay.vn:444/pay", "https://sbgateway.zalopay.vn/pay#fragment"
        })
        {
            var result = await CreateResult(JsonSerializer.Serialize(new { return_code = 1, order_url = url }));
            Check("Unsafe or production checkout URL stays unconfirmed", result.PaymentUrl is null && !result.Rejected);
        }
        foreach (var subCode in new[] { -68, -429, -500, -999, -123456 })
        {
            var result = await CreateResult(JsonSerializer.Serialize(new { return_code = 2, sub_return_code = subCode }));
            Check("Duplicate, transient or unknown Create errors do not invent failure", !result.Rejected && result.PaymentUrl is null);
        }
        foreach (var subCode in new[] { -401, -402 })
        {
            var result = await CreateResult(JsonSerializer.Serialize(new { return_code = 2, sub_return_code = subCode, return_message = Key1 }));
            Check("Definitive Create rejection is safe and does not reflect provider messages", result.Rejected && result.FailureReason == "ProviderRejected");
        }

        using (var client = new HttpClient(new Handler((_, _) => throw new TaskCanceledException("fixture timeout"))))
        {
            var gateway = new ZaloPaySandboxGateway(client, new Monitor(options));
            Check("Create timeout remains unknown", !(await gateway.CreateAsync(source, default)).Rejected);
            Check("Query timeout remains unavailable", (await gateway.QueryAsync(options.AppId, source.MerchantTransactionId!, default)).State == "Unavailable");
        }
        foreach (var payload in new[]
        {
            "not-json", "[]", "{\"return_code\":1,\"return_code\":2}", new string(' ', 131_073) + "{}"
        })
        {
            using var client = new HttpClient(new Handler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                { Content = new UnknownLengthContent(payload) })));
            var result = await new ZaloPaySandboxGateway(client, new Monitor(options)).QueryAsync(options.AppId, source.MerchantTransactionId!, default);
            Check("Malformed, ambiguous or over-limit response never becomes a receipt", result.State == "Unavailable" && result.Receipt is null);
        }
        using (var client = new HttpClient(new Handler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.Redirect)
            { Headers = { Location = new Uri("https://outside.example/") }, Content = new StringContent("redirect") }))))
            Check("HTTP redirect is not treated as provider JSON", (await new ZaloPaySandboxGateway(client, new Monitor(options))
                .QueryAsync(options.AppId, source.MerchantTransactionId!, default)).State == "Unavailable");

        var queryPayload = "{\"return_code\":1,\"amount\":35000,\"zp_trans_id\":9007199254740993,\"server_time\":1794200000000}";
        sent = null;
        using (var client = new HttpClient(new Handler(async (request, token) =>
        {
            sentUri = request.RequestUri;
            sent = Form(await request.Content!.ReadAsStringAsync(token));
            return Json(queryPayload);
        })))
        {
            var result = await new ZaloPaySandboxGateway(client, new Monitor(options)).QueryAsync(options.AppId, source.MerchantTransactionId!, default);
            Check("Query preserves IDs above JavaScript's safe integer limit", result.State == "Succeeded" && result.Receipt?.TransactionId == "9007199254740993");
            Check("Query does not label processing time as the payment timestamp", result.Receipt?.ProviderOccurredAtUtc is null);
        }
        Check("Query MAC binds the exact server-issued merchant reference", sentUri?.AbsoluteUri == ZaloPaySandboxOptions.QueryEndpoint
            && sent!["mac"] == Mac(Key1, options.AppId.ToString(CultureInfo.InvariantCulture) + "|" + source.MerchantTransactionId + "|" + Key1));
        Check("Quoted Int64 transaction IDs retain exact digits", (await QueryResult("{\"return_code\":1,\"amount\":35000,\"zp_trans_id\":\"9223372036854775807\"}")).Receipt?.TransactionId == "9223372036854775807");
        foreach (var id in new[] { "0", "-1", "1.5", "1e3", "9223372036854775808", "\"+123\"" })
        {
            var result = await QueryResult("{\"return_code\":1,\"amount\":35000,\"zp_trans_id\":" + id + "}");
            Check("Unsafe or malformed provider transaction ID is rejected", result.State == "Unavailable" && result.Receipt is null);
        }
        Check("Processing result code cannot produce a success receipt", (await QueryResult(queryPayload.Replace("\"return_code\":1", "\"return_code\":3,\"is_processing\":true"))).State == "Pending");
        Check("Success with a valid receipt uses result code despite reserved processing flag", (await QueryResult(queryPayload.Replace("\"return_code\":1", "\"return_code\":1,\"is_processing\":true"))).State == "Succeeded");
        Check("Malformed processing flag cannot produce a receipt", (await QueryResult(queryPayload.Replace("\"return_code\":1", "\"return_code\":1,\"is_processing\":\"false\""))).State == "Unavailable");
        Check("Authenticated missing reference is NotFound", (await QueryResult("{\"return_code\":2,\"sub_return_code\":-101}")).State == "NotFound");
        Check("Provider expiry is distinguished from NotFound", (await QueryResult("{\"return_code\":2,\"sub_return_code\":-54}")).State == "Expired");
        Check("Live Sandbox expiry overrides its reserved true processing flag", (await QueryResult("{\"return_code\":2,\"sub_return_code\":-54,\"is_processing\":true,\"amount\":0,\"zp_trans_id\":0}")).State == "Expired");
        Check("Missing reference is not masked by reserved processing flag", (await QueryResult("{\"return_code\":2,\"sub_return_code\":-101,\"is_processing\":true}")).State == "NotFound");
        Check("Signature rejection is not masked by reserved processing flag", (await QueryResult("{\"return_code\":2,\"sub_return_code\":-402,\"is_processing\":true}")).State == "Unavailable");
        Check("Unknown result code cannot become processing based on reserved flag", (await QueryResult("{\"return_code\":999,\"is_processing\":true}")).State == "Unavailable");
        Check("Query signature rejection stays unavailable", (await QueryResult("{\"return_code\":2,\"sub_return_code\":-402}")).State == "Unavailable");
        foreach (var failureCode in new[] { -63, -217, -332, -333, -1330, -1331, -1332, -1333, -1340, -1341, -1342, -1343 })
        {
            var failedAttempt = await QueryResult(JsonSerializer.Serialize(new { return_code = 2, sub_return_code = failureCode, return_message = Key1 }));
            Check("Known failed payment attempts remain retryable and never produce a receipt", failedAttempt.State == "Pending"
                && failedAttempt.Receipt is null && failedAttempt.RetryableFailure && failedAttempt.FailureReason == "PaymentAttemptFailed");
        }
        foreach (var failureCode in new[] { -401, -402, -429, -500, -999, -123456 })
        {
            var unavailable = await QueryResult(JsonSerializer.Serialize(new { return_code = 2, sub_return_code = failureCode }));
            Check("Request, transient and unknown Query failures never invent a definitive payment failure", unavailable.State == "Unavailable"
                && unavailable.Receipt is null && !unavailable.RetryableFailure);
        }
        using (var client = new HttpClient(new Handler((_, _) => throw new InvalidOperationException("Network should not be called"))))
        {
            var gateway = new ZaloPaySandboxGateway(client, new Monitor(options));
            Check("Changed AppId cannot query the original merchant with a different account", (await gateway.QueryAsync(99, source.MerchantTransactionId!, default)).FailureReason == "AppChanged");
        }

        using (var client = new HttpClient(new Handler((_, _) => throw new InvalidOperationException("Callback must not call HTTP"))))
        {
            var gateway = new ZaloPaySandboxGateway(client, new Monitor(options));
            var payload = Callback(source);
            Check("Signed callback preserves the large transaction ID", gateway.ReadCallback(payload, Mac(Key2, payload), 1)?.TransactionId == "9007199254740993");
            Check("Callback accepts uppercase hexadecimal MAC", gateway.ReadCallback(payload, Mac(Key2, payload).ToUpperInvariant(), 1) is not null);
            Check("Wrong MAC is rejected before payload parsing", gateway.ReadCallback("not-json", new string('0', 64), 1) is null);
            Check("Wrong callback type is rejected", gateway.ReadCallback(payload, Mac(Key2, payload), 2) is null);
            var mismatchedApp = payload.Replace("\"app_id\":5555", "\"app_id\":9999");
            Check("Signed callback from a different app is rejected", gateway.ReadCallback(mismatchedApp, Mac(Key2, mismatchedApp), 1) is null);
            var duplicate = payload.Replace("\"app_id\":5555", "\"app_id\":5555,\"app_id\":5555");
            Check("Ambiguous duplicate callback fields are rejected", gateway.ReadCallback(duplicate, Mac(Key2, duplicate), 1) is null);
            var fraction = payload.Replace("\"amount\":35000", "\"amount\":35000.5");
            Check("Fractional callback amount is rejected", gateway.ReadCallback(fraction, Mac(Key2, fraction), 1) is null);
            var future = Callback(Source(DateTimeOffset.UtcNow.AddDays(1)));
            Check("Future callback timestamps are rejected", gateway.ReadCallback(future, Mac(Key2, future), 1) is null);
            var old = Callback(Source(DateTimeOffset.UtcNow.AddDays(-2)));
            Check("Authentic delayed callback is not rejected just for age", gateway.ReadCallback(old, Mac(Key2, old), 1) is not null);
            Check("Malformed JSON with a valid fixture MAC cannot become a receipt", gateway.ReadCallback("not-json", Mac(Key2, "not-json"), 1) is null);
            var changedBytes = payload.Replace("\"app_id\":", "\"app_id\": ");
            Check("Callback MAC covers original data text rather than normalized JSON", gateway.ReadCallback(changedBytes, Mac(Key2, payload), 1) is null);
            Check("Equivalent JSON is accepted only with its own original-byte signature", gateway.ReadCallback(changedBytes, Mac(Key2, changedBytes), 1) is not null);
            var wrongDate = payload.Replace(source.MerchantTransactionId[..6], "010101");
            Check("Signed merchant date must match the saved creation date in Vietnam", gateway.ReadCallback(wrongDate, Mac(Key2, wrongDate), 1) is null);
            var zeroId = payload.Replace("9007199254740993", "0");
            Check("Signed callbacks still require a positive provider transaction ID", gateway.ReadCallback(zeroId, Mac(Key2, zeroId), 1) is null);
        }

        var disabled = Options();
        disabled.Enabled = false;
        using (var client = new HttpClient(new Handler((_, _) => throw new InvalidOperationException("Disabled adapter must not call HTTP"))))
        {
            var gateway = new ZaloPaySandboxGateway(client, new Monitor(disabled));
            Check("Disabled adapter creates no URL or provider order", (await gateway.CreateAsync(source, default)).PaymentUrl is null);
            Check("Disabled adapter cannot query a receipt", (await gateway.QueryAsync(source.AppId, source.MerchantTransactionId, default)).Receipt is null);
            Check("Disabled callback cannot be acknowledged as a valid receipt", gateway.ReadCallback(Callback(source), Mac(Key2, Callback(source)), 1) is null);
        }
        foreach (var status in new[] { HttpStatusCode.BadRequest, HttpStatusCode.Unauthorized, HttpStatusCode.TooManyRequests, HttpStatusCode.InternalServerError })
        {
            using var client = new HttpClient(new Handler((_, _) => Task.FromResult(new HttpResponseMessage(status)
                { Content = new StringContent(Key1, Encoding.UTF8, "application/json") })));
            var gateway = new ZaloPaySandboxGateway(client, new Monitor(options));
            Check("HTTP failure does not create a URL or falsely reject an ambiguous intent", (await gateway.CreateAsync(source, default)) is { PaymentUrl: null, Rejected: false });
            Check("HTTP failure cannot fall back to a success receipt", (await gateway.QueryAsync(source.AppId, source.MerchantTransactionId, default)) is { State: "Unavailable", Receipt: null });
        }
        using (var canceled = new CancellationTokenSource())
        using (var client = new HttpClient(new Handler((_, token) => throw new OperationCanceledException(token))))
        {
            canceled.Cancel();
            var gateway = new ZaloPaySandboxGateway(client, new Monitor(options));
            try { await gateway.CreateAsync(source, canceled.Token); throw new InvalidOperationException("Caller cancellation was swallowed."); }
            catch (OperationCanceledException) { Check("Caller cancellation is propagated so recovery can query the saved intent", true); }
        }
        return checks;

        void Check(string description, bool success)
        {
            if (!success) throw new InvalidOperationException("ZaloPay gateway check failed: " + description);
            checks++;
            Console.WriteLine("PASS ZaloPay gateway: " + description);
        }

        async Task<CyberManagement.Application.DTOs.Payments.GatewayCreateResult> CreateResult(string payload)
        {
            using var client = new HttpClient(new Handler((_, _) => Task.FromResult(Json(payload))));
            return await new ZaloPaySandboxGateway(client, new Monitor(options)).CreateAsync(source, default);
        }

        async Task<CyberManagement.Application.DTOs.Payments.GatewayQueryResult> QueryResult(string payload)
        {
            using var client = new HttpClient(new Handler((_, _) => Task.FromResult(Json(payload))));
            return await new ZaloPaySandboxGateway(client, new Monitor(options)).QueryAsync(options.AppId, source.MerchantTransactionId!, default);
        }
    }

    private static ZaloPaySandboxOptions Options() => new()
    {
        Enabled = true, AppId = 5555, Key1 = Key1, Key2 = Key2,
        CallbackUrl = "https://callback.example.org/api/payments/zalopay/callback",
        RedirectUrl = "http://127.0.0.1:5175/client/wallet"
    };

    private static GatewayCreateRequest Source(DateTimeOffset created) => new(41, "TopUp", 17, null, 5555,
        created.ToOffset(TimeSpan.FromHours(7)).ToString("yyMMdd", CultureInfo.InvariantCulture) + "_00112233445566778899aabbccddeeff",
        35000, created, created.AddMinutes(15));

    private static string Callback(GatewayCreateRequest source) => JsonSerializer.Serialize(new
    {
        app_id = 5555, app_trans_id = source.MerchantTransactionId, app_user = "customer:17",
        app_time = source.CreatedAtUtc.ToUnixTimeMilliseconds(), amount = 35000,
        zp_trans_id = 9_007_199_254_740_993L, server_time = source.CreatedAtUtc.AddSeconds(1).ToUnixTimeMilliseconds()
    });

    private static HttpResponseMessage Json(string value) => new(HttpStatusCode.OK)
        { Content = new StringContent(value, Encoding.UTF8, "application/json") };

    private static Dictionary<string, string> Form(string value) => value.Split('&').Select(pair => pair.Split('=', 2))
        .ToDictionary(pair => Uri.UnescapeDataString(pair[0].Replace('+', ' ')), pair => Uri.UnescapeDataString(pair[1].Replace('+', ' ')));

    private static string Mac(string key, string value) => Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(key), Encoding.UTF8.GetBytes(value))).ToLowerInvariant();

    private sealed class Handler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => send(request, cancellationToken);
    }

    private sealed class Monitor(ZaloPaySandboxOptions value) : IOptionsMonitor<ZaloPaySandboxOptions>
    {
        public ZaloPaySandboxOptions CurrentValue => value;
        public ZaloPaySandboxOptions Get(string? name) => value;
        public IDisposable? OnChange(Action<ZaloPaySandboxOptions, string?> listener) => null;
    }

    private sealed class UnknownLengthContent : HttpContent
    {
        private readonly byte[] bytes;
        public UnknownLengthContent(string payload)
        {
            Headers.ContentType = new MediaTypeHeaderValue("application/json");
            bytes = Encoding.UTF8.GetBytes(payload);
        }
        protected override bool TryComputeLength(out long length) { length = 0; return false; }
        protected override async Task SerializeToStreamAsync(Stream stream, TransportContext? context)
        {
            await stream.WriteAsync(bytes);
        }
    }
}
