using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace CyberManagement.Infrastructure.Payments;

// Protocol signatures only. A valid MAC does not prove a payment was persisted or belongs to a source.
public static class ZaloPaySignatures
{
    public const int MaximumCallbackDataBytes = 65_536;
    private static readonly UTF8Encoding Utf8 = new(false, true);

    public static string Create(int appId, string appTransId, string appUser, long amount,
        long appTime, string embedData, string item, string key1)
    {
        ValidateIdentity(appId, appTransId);
        ValidateText(appUser, 50, nameof(appUser));
        if (appUser.Contains('|')) throw new ArgumentException("User reference cannot contain a separator.", nameof(appUser));
        if (amount is <= 0 or > 999_999_999)
            throw new ArgumentOutOfRangeException(nameof(amount), "Amount must be positive and within the application's VND limit.");
        if (appTime <= 0) throw new ArgumentOutOfRangeException(nameof(appTime), "Creation time must be positive Unix milliseconds.");
        // Conservative application bound for embed_data; the current Create HTML does not specify a maximum.
        ValidateText(embedData, 1024, nameof(embedData));
        ValidateText(item, 2048, nameof(item));
        ValidateKey(key1);
        var input = string.Join('|', appId.ToString(CultureInfo.InvariantCulture), appTransId, appUser,
            amount.ToString(CultureInfo.InvariantCulture), appTime.ToString(CultureInfo.InvariantCulture), embedData, item);
        return Sign(input, key1);
    }

    public static string Query(int appId, string appTransId, string key1)
    {
        ValidateIdentity(appId, appTransId);
        ValidateKey(key1);
        return Sign(string.Join('|', appId.ToString(CultureInfo.InvariantCulture), appTransId, key1), key1);
    }

    public static bool VerifyCallback(string data, string mac, string key2)
    {
        if (string.IsNullOrEmpty(data) || data.Length > MaximumCallbackDataBytes
            || string.IsNullOrWhiteSpace(key2) || mac is null || mac.Length != 64)
            return false;
        try
        {
            if (Utf8.GetByteCount(data) > MaximumCallbackDataBytes) return false;
            var received = Convert.FromHexString(mac);
            var expected = HMACSHA256.HashData(Utf8.GetBytes(key2), Utf8.GetBytes(data));
            return CryptographicOperations.FixedTimeEquals(expected, received);
        }
        catch (FormatException) { return false; }
        catch (EncoderFallbackException) { return false; }
    }

    private static string Sign(string data, string key) =>
        Convert.ToHexString(HMACSHA256.HashData(Utf8.GetBytes(key), Utf8.GetBytes(data))).ToLowerInvariant();

    private static void ValidateIdentity(int appId, string appTransId)
    {
        if (appId <= 0) throw new ArgumentOutOfRangeException(nameof(appId), "Application ID must be positive.");
        ValidateText(appTransId, 40, nameof(appTransId));
        if (appTransId.Contains('|')) throw new ArgumentException("Transaction reference cannot contain a separator.", nameof(appTransId));
    }

    private static void ValidateText(string value, int maximum, string parameter)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > maximum)
            throw new ArgumentException("Required protocol text is missing or exceeds its length limit.", parameter);
    }

    private static void ValidateKey(string key)
    {
        if (string.IsNullOrWhiteSpace(key)) throw new ArgumentException("Signing key is required.", nameof(key));
    }
}
