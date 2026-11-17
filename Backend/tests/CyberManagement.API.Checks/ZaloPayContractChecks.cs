using CyberManagement.Infrastructure.Payments;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

internal static class ZaloPayContractChecks
{
    // Dedicated internal fixture keys. These are not provider credentials and cannot demonstrate a live payment.
    private const string FixtureKey1 = "fixture-key-1-2026";
    private const string FixtureKey2 = "fixture-key-2-2026";
    private const string FixtureData = "{\"app_id\":2553,\"app_trans_id\":\"261108_T42\",\"amount\":50000,\"note\":\"nạp ví\"}";
    private const string FixtureMac = "f7a7b376764ff2c2c1d7d09d0acd3a5f7939659a4564a7874b21a1923675cbb3";

    public static int Run()
    {
        var checks = 0;
        // Expected answers were generated independently with Node.js node:crypto, not by the implementation under test.
        Check("Create MAC matches independent UTF-8 fixture", ZaloPaySignatures.Create(2553, "261108_T42", "khách-42",
            50000, 1794096000123, "{\"purpose\":\"TopUp\",\"sourceId\":42}", "[]", FixtureKey1)
            == "fa3f059648e8c2a436fe9f20ae62c6ef343192f943ffe3cbc6680e99baa87977");
        Check("Query MAC includes the signing key in the input", ZaloPaySignatures.Query(2553, "261108_T42", FixtureKey1)
            == "81e63237f415b48410fff277d0d9b7de0724ed9ead368fd2c42834f797629653");
        Check("Callback MAC matches independent raw JSON fixture", ZaloPaySignatures.VerifyCallback(FixtureData, FixtureMac, FixtureKey2));
        Check("Callback accepts uppercase hexadecimal", ZaloPaySignatures.VerifyCallback(FixtureData, FixtureMac.ToUpperInvariant(), FixtureKey2));
        Check("Changed amount invalidates callback MAC", !ZaloPaySignatures.VerifyCallback(FixtureData.Replace("50000", "60000"), FixtureMac, FixtureKey2));
        Check("Whitespace changes the signed raw data", !ZaloPaySignatures.VerifyCallback(" " + FixtureData, FixtureMac, FixtureKey2));
        Check("Wrong callback key is rejected", !ZaloPaySignatures.VerifyCallback(FixtureData, FixtureMac, FixtureKey1));
        foreach (var mac in new[] { "", "00", new string('0', 63), new string('0', 65), new string('z', 64), new string('0', 64) })
            Check("Malformed or wrong callback MAC is rejected", !ZaloPaySignatures.VerifyCallback(FixtureData, mac, FixtureKey2));
        Check("Empty callback data is rejected", !ZaloPaySignatures.VerifyCallback("", FixtureMac, FixtureKey2));
        Check("Oversized callback data is rejected", !ZaloPaySignatures.VerifyCallback(new string('x', 65_537), FixtureMac, FixtureKey2));
        Check("Callback limit counts UTF-8 bytes", !ZaloPaySignatures.VerifyCallback(new string('ạ', 30_000), FixtureMac, FixtureKey2));
        Check("Invalid UTF-16 cannot be normalized into signed data", !ZaloPaySignatures.VerifyCallback("\ud800", FixtureMac, FixtureKey2));
        Check("Empty callback key is rejected", !ZaloPaySignatures.VerifyCallback(FixtureData, FixtureMac, ""));
        RejectArgument("Invalid app ID is rejected", () => ZaloPaySignatures.Query(0, "261108_T42", FixtureKey1));
        RejectArgument("Overlong merchant ID is rejected", () => ZaloPaySignatures.Query(2553, new string('x', 41), FixtureKey1));
        RejectArgument("Delimiter in merchant ID is rejected", () => ZaloPaySignatures.Query(2553, "261108_T|42", FixtureKey1));
        RejectArgument("Missing request key is rejected", () => ZaloPaySignatures.Query(2553, "261108_T42", ""));
        RejectArgument("Fraction-free amount cannot be zero", () => ZaloPaySignatures.Create(2553, "261108_T42", "42", 0, 1, "{}", "[]", FixtureKey1));
        RejectArgument("Application amount cap is enforced", () => ZaloPaySignatures.Create(2553, "261108_T42", "42", 1_000_000_000, 1, "{}", "[]", FixtureKey1));
        RejectArgument("Overlong app_user is rejected", () => ZaloPaySignatures.Create(2553, "261108_T42", new string('x', 51), 1, 1, "{}", "[]", FixtureKey1));
        RejectArgument("Overlong item is rejected", () => ZaloPaySignatures.Create(2553, "261108_T42", "42", 1, 1, "{}", new string('x', 2049), FixtureKey1));

        ValidateConfiguration("Disabled sandbox does not require credentials", new(), "Production", true);
        foreach (var environment in new[] { "Development", "Testing", "Test" })
            ValidateConfiguration("Valid sandbox configuration in " + environment, EnabledConfiguration(), environment, true);
        ValidateConfiguration("Enabled sandbox is rejected in Production", EnabledConfiguration(), "Production", false);
        foreach (var field in new[] { "AppId", "Key1", "Key2", "CallbackUrl", "RedirectUrl" })
        {
            var configuration = EnabledConfiguration();
            configuration.Remove(ZaloPaySandboxOptions.SectionName + ":" + field);
            ValidateConfiguration("Enabled sandbox requires " + field, configuration, "Development", false);
        }
        foreach (var callback in new[]
        {
            "http://callback.example.org/api/payments/zalopay/callback", "https://localhost/api/payments/zalopay/callback",
            "https://127.0.0.1/api/payments/zalopay/callback", "https://[::1]/api/payments/zalopay/callback",
            "https://api.local/api/payments/zalopay/callback", "https://callback.example.org:8443/api/payments/zalopay/callback",
            "https://callback.example.org/other", "https://user@callback.example.org/api/payments/zalopay/callback",
            "https://callback.example.org/api/payments/zalopay/callback?x=1", "https://callback.example.org/api/payments/zalopay/callback#part"
        })
        {
            var configuration = EnabledConfiguration();
            configuration[ZaloPaySandboxOptions.SectionName + ":CallbackUrl"] = callback;
            ValidateConfiguration("Unsafe or incorrect callback URL is rejected", configuration, "Development", false);
        }
        foreach (var redirect in new[] { "http://localhost:5173/client/wallet", "http://127.0.0.1:5173/client/wallet", "http://[::1]:5173/client/wallet", "https://app.example.org/client/wallet" })
        {
            var configuration = EnabledConfiguration();
            configuration[ZaloPaySandboxOptions.SectionName + ":RedirectUrl"] = redirect;
            ValidateConfiguration("Valid development redirect is accepted", configuration, "Development", true);
        }
        foreach (var redirect in new[] { "http://app.example.org/client/wallet", "https://user@app.example.org/client/wallet", "https://app.example.org/client/wallet?paid=true", "https://app.example.org/client/wallet#paid", "javascript:alert(1)" })
        {
            var configuration = EnabledConfiguration();
            configuration[ZaloPaySandboxOptions.SectionName + ":RedirectUrl"] = redirect;
            ValidateConfiguration("Unsafe redirect URL is rejected", configuration, "Development", false);
        }
        Check("Provider URLs are fixed sandbox HTTPS endpoints", ZaloPaySandboxOptions.CreateEndpoint == "https://sb-openapi.zalopay.vn/v2/create"
            && ZaloPaySandboxOptions.QueryEndpoint == "https://sb-openapi.zalopay.vn/v2/query");
        return checks;

        void Check(string name, bool condition)
        {
            if (!condition) throw new InvalidOperationException(name);
            checks++;
            Console.WriteLine("PASS ZaloPay fixture: " + name);
        }
        void RejectArgument(string name, Action action)
        {
            try { action(); }
            catch (ArgumentException) { Check(name, true); return; }
            throw new InvalidOperationException(name);
        }
        void ValidateConfiguration(string name, Dictionary<string, string?> values, string environment, bool valid)
        {
            var configuration = new ConfigurationBuilder().AddInMemoryCollection(values).Build();
            var services = new ServiceCollection().AddZaloPaySandboxConfiguration(configuration, environment);
            using var provider = services.BuildServiceProvider();
            try
            {
                // Exercise ValidateOnStart without opening HTTP ports or a database.
                provider.GetRequiredService<IStartupValidator>().Validate();
                _ = provider.GetRequiredService<IOptions<ZaloPaySandboxOptions>>().Value;
                Check(name, valid);
            }
            catch (OptionsValidationException exception)
            {
                Check(name, !valid);
                Check("Configuration errors do not include key values", !exception.ToString().Contains(FixtureKey1)
                    && !exception.ToString().Contains(FixtureKey2));
            }
        }
    }

    private static Dictionary<string, string?> EnabledConfiguration() => new()
    {
        [ZaloPaySandboxOptions.SectionName + ":Enabled"] = "true",
        [ZaloPaySandboxOptions.SectionName + ":AppId"] = "2553",
        [ZaloPaySandboxOptions.SectionName + ":Key1"] = FixtureKey1,
        [ZaloPaySandboxOptions.SectionName + ":Key2"] = FixtureKey2,
        [ZaloPaySandboxOptions.SectionName + ":CallbackUrl"] = "https://callback.example.org/api/payments/zalopay/callback",
        [ZaloPaySandboxOptions.SectionName + ":RedirectUrl"] = "http://127.0.0.1:5173/client/wallet"
    };
}
