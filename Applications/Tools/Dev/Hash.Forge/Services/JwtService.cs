using System.Text;
using System.Text.Json;

namespace HashForge.Services;

public record JwtResult(
    string Header,
    string Payload,
    string Signature,
    string Info,
    bool   IsValid);

public static class JwtService
{
    public static JwtResult Decode(string token)
    {
        var parts = token.Trim().Split('.');
        if (parts.Length != 3)
            return new JwtResult("", "❌ 유효하지 않은 JWT (점(.) 구분자 3개 필요)", "", "", false);

        try
        {
            var header    = PrettyJson(B64UrlDecode(parts[0]));
            var payload   = PrettyJson(B64UrlDecode(parts[1]));
            var signature = parts[2];
            var info      = BuildInfo(parts[1]);
            return new JwtResult(header, payload, signature, info, true);
        }
        catch (Exception ex)
        {
            return new JwtResult("", $"❌ 파싱 오류: {ex.Message}", "", "", false);
        }
    }

    private static string BuildInfo(string payloadPart)
    {
        var sb = new StringBuilder();
        try
        {
            using var doc = JsonDocument.Parse(B64UrlDecode(payloadPart));
            var root = doc.RootElement;

            if (root.TryGetProperty("exp", out var exp))
            {
                var t = DateTimeOffset.FromUnixTimeSeconds(exp.GetInt64()).ToLocalTime();
                var expired = t < DateTimeOffset.Now;
                sb.AppendLine($"만료: {t:yyyy-MM-dd HH:mm:ss}  {(expired ? "❌ 만료됨" : "✅ 유효")}");
            }
            if (root.TryGetProperty("iat", out var iat))
            {
                var t = DateTimeOffset.FromUnixTimeSeconds(iat.GetInt64()).ToLocalTime();
                sb.AppendLine($"발급: {t:yyyy-MM-dd HH:mm:ss}");
            }
            if (root.TryGetProperty("sub", out var sub))
                sb.AppendLine($"Subject: {sub}");
            if (root.TryGetProperty("iss", out var iss))
                sb.AppendLine($"Issuer: {iss}");
        }
        catch { }
        return sb.ToString().TrimEnd();
    }

    private static string B64UrlDecode(string s)
    {
        s = s.Replace('-', '+').Replace('_', '/');
        s = s.PadRight(s.Length + (4 - s.Length % 4) % 4, '=');
        return Encoding.UTF8.GetString(Convert.FromBase64String(s));
    }

    private static string PrettyJson(string json)
    {
        try
        {
            var obj = JsonSerializer.Deserialize<object>(json);
            return JsonSerializer.Serialize(obj, new JsonSerializerOptions { WriteIndented = true });
        }
        catch { return json; }
    }
}
