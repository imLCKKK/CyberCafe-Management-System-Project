namespace CyberManagement.Infrastructure.Payments;

public sealed class ZaloPaySandboxOptions
{
    public const string SectionName = "Payments:ZaloPaySandbox";
    public const string CreateEndpoint = "https://sb-openapi.zalopay.vn/v2/create";
    public const string QueryEndpoint = "https://sb-openapi.zalopay.vn/v2/query";
    public const string CallbackPath = "/api/payments/zalopay/callback";

    public bool Enabled { get; set; }
    public int AppId { get; set; }
    public string Key1 { get; set; } = string.Empty;
    public string Key2 { get; set; } = string.Empty;
    public string CallbackUrl { get; set; } = string.Empty;
    public string RedirectUrl { get; set; } = string.Empty;
}
