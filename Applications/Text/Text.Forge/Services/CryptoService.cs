using System.Net;
using System.Security.Cryptography;
using System.Text;

namespace TextForge.Services;

public static class CryptoService
{
    // ── 해시 ─────────────────────────────────────────────────────
    public static string Md5(string s)    => Hex(MD5.HashData(U8(s)));
    public static string Sha1(string s)   => Hex(SHA1.HashData(U8(s)));
    public static string Sha256(string s) => Hex(SHA256.HashData(U8(s)));
    public static string Sha512(string s) => Hex(SHA512.HashData(U8(s)));

    public static string HmacSha256(string s, string key)
    {
        if (string.IsNullOrEmpty(key)) return "";
        using var h = new HMACSHA256(U8(key));
        return Hex(h.ComputeHash(U8(s)));
    }

    // ── 인코딩 ───────────────────────────────────────────────────
    public static string Base64Encode(string s) => Convert.ToBase64String(U8(s));
    public static string Base64Decode(string s)
    {
        try { return Encoding.UTF8.GetString(Convert.FromBase64String(s.Trim())); }
        catch { return "❌ 유효하지 않은 Base64"; }
    }

    public static string UrlEncode(string s) => Uri.EscapeDataString(s);
    public static string UrlDecode(string s)
    {
        try { return Uri.UnescapeDataString(s); }
        catch { return "❌ 유효하지 않은 URL 인코딩"; }
    }

    public static string HexEncode(string s) => Hex(U8(s));
    public static string HexDecode(string s)
    {
        try
        {
            var clean = s.Replace(" ", "").Replace("-", "").Replace("\n", "");
            return Encoding.UTF8.GetString(Convert.FromHexString(clean));
        }
        catch { return "❌ 유효하지 않은 Hex"; }
    }

    public static string HtmlEncode(string s) => WebUtility.HtmlEncode(s);
    public static string HtmlDecode(string s) => WebUtility.HtmlDecode(s);

    // ── 생성기 ───────────────────────────────────────────────────
    public static string GenerateUuid() => Guid.NewGuid().ToString();

    public static string GenerateUlid()
    {
        const string B32 = "0123456789ABCDEFGHJKMNPQRSTVWXYZ";
        var ms = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var sb = new StringBuilder(26);

        var t = ms;
        for (int i = 9; i >= 0; i--)
        { sb.Insert(0, B32[(int)(t % 32)]); t /= 32; }

        var rng = RandomNumberGenerator.GetBytes(10);
        ulong rand = 0;
        for (int i = 0; i < 10; i++) rand = (rand << 8) | rng[i];
        for (int i = 0; i < 16; i++)
        { sb.Append(B32[(int)(rand % 32)]); rand >>= 5; }

        return sb.ToString();
    }

    public static string GeneratePassword(int length, bool upper, bool digits, bool special)
    {
        var pool = "abcdefghijklmnopqrstuvwxyz";
        if (upper)   pool += "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
        if (digits)  pool += "0123456789";
        if (special) pool += "!@#$%^&*()_+-=[]{}|;:,.?";

        var bytes = RandomNumberGenerator.GetBytes(length);
        return new string(bytes.Select(b => pool[b % pool.Length]).ToArray());
    }

    public static (int score, string label, string color) PasswordStrength(string pw)
    {
        int s = 0;
        if (pw.Length >= 8)  s++;
        if (pw.Length >= 14) s++;
        if (pw.Any(char.IsUpper) && pw.Any(char.IsLower)) s++;
        if (pw.Any(char.IsDigit)) s++;
        if (pw.Any(c => "!@#$%^&*()_+-=[]{}|;:,.?".Contains(c))) s++;
        s = Math.Min(s, 4);
        return s switch
        {
            0 or 1 => (s, "매우 약함", "#D05060"),
            2      => (s, "약함",     "#D08030"),
            3      => (s, "보통",     "#A0B030"),
            _      => (s, "강함",     "#50C878")
        };
    }

    private static byte[] U8(string s) => Encoding.UTF8.GetBytes(s);
    private static string Hex(byte[] b) => Convert.ToHexString(b).ToLower();
}
