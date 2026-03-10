namespace Tag.Forge.Services;

using System.Net.Http;
using System.Text.Json;

public class MusicBrainzResult
{
    public string Title  { get; set; } = "";
    public string Artist { get; set; } = "";
    public string Album  { get; set; } = "";
    public uint   Year   { get; set; }
}

public class MusicBrainzService
{
    readonly HttpClient _http = new();

    public MusicBrainzService()
    {
        _http.DefaultRequestHeaders.Add("User-Agent", "Tag.Forge/1.0 (+playground)");
        _http.Timeout = TimeSpan.FromSeconds(10);
    }

    public async Task<MusicBrainzResult?> LookupAsync(string title, string artist, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(title)) return null;
        var q = $"recording:\"{Uri.EscapeDataString(title)}\"";
        if (!string.IsNullOrWhiteSpace(artist))
            q += $"+artist:\"{Uri.EscapeDataString(artist)}\"";
        var url = $"https://musicbrainz.org/ws/2/recording/?query={q}&fmt=json&limit=1";
        try
        {
            var json = await _http.GetStringAsync(url, ct);
            using var doc = JsonDocument.Parse(json);
            var recordings = doc.RootElement.GetProperty("recordings");
            if (recordings.GetArrayLength() == 0) return null;
            var r = recordings[0];
            var result = new MusicBrainzResult();
            if (r.TryGetProperty("title", out var t))   result.Title  = t.GetString() ?? "";
            if (r.TryGetProperty("artist-credit", out var ac) && ac.GetArrayLength() > 0
                && ac[0].TryGetProperty("name", out var an))
                result.Artist = an.GetString() ?? "";
            if (r.TryGetProperty("releases", out var rels) && rels.GetArrayLength() > 0)
            {
                var rel = rels[0];
                if (rel.TryGetProperty("title", out var rt))  result.Album = rt.GetString() ?? "";
                if (rel.TryGetProperty("date",  out var rd))
                {
                    var ds = rd.GetString() ?? "";
                    if (ds.Length >= 4 && uint.TryParse(ds[..4], out var y)) result.Year = y;
                }
            }
            return result;
        }
        catch { return null; }
    }
}
