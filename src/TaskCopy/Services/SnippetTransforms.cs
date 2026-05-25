using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace TaskCopy.Services;

/// <summary>
/// Pipe-chained transforms for snippet placeholders (F28). Apply via
/// {{clipboard|upper}}, {{date|lower}}, {{clipboard|trim|jsonpretty}} etc.
/// Unknown transform names leave the value unchanged so they degrade
/// gracefully — same posture as unknown tokens.
/// </summary>
public static class SnippetTransforms
{
    public static string Apply(string transform, string value)
    {
        if (string.IsNullOrEmpty(transform)) return value;

        var name = transform.Trim().ToLowerInvariant();
        try
        {
            return name switch
            {
                "upper" or "uppercase" => value.ToUpperInvariant(),
                "lower" or "lowercase" => value.ToLowerInvariant(),
                "trim" => value.Trim(),
                "trimstart" => value.TrimStart(),
                "trimend" => value.TrimEnd(),
                "reverse" => Reverse(value),
                "length" => value.Length.ToString(),
                "jsonpretty" => TryJsonPretty(value),
                "urlencode" => Uri.EscapeDataString(value),
                "urldecode" => SafeUnescape(value),
                "base64" or "base64encode" => Convert.ToBase64String(Encoding.UTF8.GetBytes(value)),
                "base64decode" => TryBase64Decode(value),
                "sha256" => Sha256(value),
                "md5" => Md5(value),
                _ => value, // unknown — leave unchanged
            };
        }
        catch
        {
            // Per-transform failure is non-fatal — return the input unmodified.
            return value;
        }
    }

    private static string Reverse(string s)
    {
        var arr = s.ToCharArray();
        Array.Reverse(arr);
        return new string(arr);
    }

    private static string TryJsonPretty(string s)
    {
        try
        {
            using var doc = JsonDocument.Parse(s);
            return JsonSerializer.Serialize(doc.RootElement, new JsonSerializerOptions { WriteIndented = true });
        }
        catch
        {
            return s;
        }
    }

    private static string SafeUnescape(string s)
    {
        try { return Uri.UnescapeDataString(s); } catch { return s; }
    }

    private static string TryBase64Decode(string s)
    {
        try { return Encoding.UTF8.GetString(Convert.FromBase64String(s)); } catch { return s; }
    }

    private static string Sha256(string s)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(s));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static string Md5(string s)
    {
        var bytes = MD5.HashData(Encoding.UTF8.GetBytes(s));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
