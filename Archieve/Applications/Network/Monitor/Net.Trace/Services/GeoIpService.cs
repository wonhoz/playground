using System.Net.Http;
using System.Text.Json;

namespace Net.Trace.Services;

public class GeoIpService : IDisposable
{
    readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(6) };
    readonly Dictionary<string, (string? country, string? city, double? lat, double? lon)> _cache = [];

    static readonly HashSet<string> PrivatePrefixes =
    [
        "10.", "127.", "169.254.", "::1", "fe80"
    ];

    public async Task<(string? country, string? city, double? lat, double? lon)> LookupAsync(string ip)
    {
        if (string.IsNullOrEmpty(ip) || IsPrivate(ip))
            return ("사설 IP", null, null, null);

        if (_cache.TryGetValue(ip, out var cached)) return cached;

        try
        {
            var json = await _http.GetStringAsync(
                $"http://ip-api.com/json/{ip}?fields=status,country,city,lat,lon&lang=en");
            using var doc  = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (root.GetProperty("status").GetString() == "success")
            {
                var result = (
                    root.GetProperty("country").GetString(),
                    root.GetProperty("city").GetString(),
                    (double?)root.GetProperty("lat").GetDouble(),
                    (double?)root.GetProperty("lon").GetDouble());
                _cache[ip] = result;
                return result;
            }
        }
        catch { /* 네트워크 오류 무시 */ }

        var fallback = ((string?)null, (string?)null, (double?)null, (double?)null);
        _cache[ip] = fallback;
        return fallback;
    }

    static bool IsPrivate(string ip)
    {
        if (PrivatePrefixes.Any(ip.StartsWith)) return true;
        if (!IPAddress.TryParse(ip, out var addr)) return true;
        var b = addr.GetAddressBytes();
        return b[0] == 10
            || (b[0] == 172 && b[1] is >= 16 and <= 31)
            || (b[0] == 192 && b[1] == 168);
    }

    public void Dispose() => _http.Dispose();
}
