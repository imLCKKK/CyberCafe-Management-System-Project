using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace CyberManagement.Application.Services;

// RFC 8785 for the I-JSON values used by the balance contract (finite IEEE-754 numbers).
public static class TeamBalanceJson
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    public static string Hash<T>(T value) => "sha256:" + Convert.ToHexStringLower(
        SHA256.HashData(Encoding.UTF8.GetBytes(Canonical(value))));

    public static string Canonical<T>(T value)
    {
        var json = JsonSerializer.SerializeToElement(value, Options);
        var result = new StringBuilder();
        Append(json, result);
        return result.ToString();
    }

    private static void Append(JsonElement element, StringBuilder output)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                output.Append('{');
                var first = true;
                var keys = new HashSet<string>(StringComparer.Ordinal);
                foreach (var property in element.EnumerateObject().OrderBy(p => p.Name, StringComparer.Ordinal))
                {
                    if (!keys.Add(property.Name)) throw new JsonException("JSON có key bị trùng.");
                    if (!first) output.Append(',');
                    first = false;
                    AppendString(property.Name, output);
                    output.Append(':');
                    Append(property.Value, output);
                }
                output.Append('}');
                break;
            case JsonValueKind.Array:
                output.Append('[');
                var separator = false;
                foreach (var item in element.EnumerateArray())
                {
                    if (separator) output.Append(',');
                    separator = true;
                    Append(item, output);
                }
                output.Append(']');
                break;
            case JsonValueKind.String: AppendString(element.GetString()!, output); break;
            case JsonValueKind.Number: output.Append(Number(element.GetDouble())); break;
            case JsonValueKind.True: output.Append("true"); break;
            case JsonValueKind.False: output.Append("false"); break;
            case JsonValueKind.Null: output.Append("null"); break;
            default: throw new JsonException("Giá trị JSON không hợp lệ.");
        }
    }

    private static void AppendString(string value, StringBuilder output)
    {
        output.Append('"');
        for (var i = 0; i < value.Length; i++)
        {
            var c = value[i];
            switch (c)
            {
                case '"': output.Append("\\\""); break;
                case '\\': output.Append("\\\\"); break;
                case '\b': output.Append("\\b"); break;
                case '\t': output.Append("\\t"); break;
                case '\n': output.Append("\\n"); break;
                case '\f': output.Append("\\f"); break;
                case '\r': output.Append("\\r"); break;
                default:
                    if (c < 32) output.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                    else if (char.IsHighSurrogate(c) && i + 1 < value.Length && char.IsLowSurrogate(value[i + 1]))
                        output.Append(c).Append(value[++i]);
                    else if (char.IsSurrogate(c)) throw new JsonException("Unicode không hợp lệ.");
                    else output.Append(c);
                    break;
            }
        }
        output.Append('"');
    }

    private static string Number(double value)
    {
        if (!double.IsFinite(value)) throw new JsonException("Số không hữu hạn.");
        if (value == 0) return "0";
        var negative = value < 0 ? "-" : "";
        var text = Math.Abs(value).ToString("R", CultureInfo.InvariantCulture).ToLowerInvariant();
        var parts = text.Split('e');
        var exponent = parts.Length == 2 ? int.Parse(parts[1], CultureInfo.InvariantCulture) : 0;
        var dot = parts[0].IndexOf('.');
        var point = (dot < 0 ? parts[0].Length : dot) + exponent;
        var digits = parts[0].Replace(".", "");
        while (digits.Length > 1 && digits[0] == '0') { digits = digits[1..]; point--; }
        digits = digits.TrimEnd('0');
        if (point > 0 && point <= 21)
            return negative + (point >= digits.Length ? digits.PadRight(point, '0') : digits.Insert(point, "."));
        if (point <= 0 && point > -6) return negative + "0." + new string('0', -point) + digits;
        var scientificExponent = point - 1;
        return negative + digits[0] + (digits.Length > 1 ? "." + digits[1..] : "") + "e"
            + (scientificExponent >= 0 ? "+" : "") + scientificExponent.ToString(CultureInfo.InvariantCulture);
    }
}
