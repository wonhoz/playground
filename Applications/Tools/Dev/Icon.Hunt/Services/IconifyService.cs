using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Nodes;
using IconHunt.Models;

namespace IconHunt.Services;

public class IconifyService : IDisposable
{
    private static readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(15) };
    private static readonly string _cacheRoot =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "IconHunt", "svg");

    private const string BaseApi = "https://api.iconify.design";

    // ── SVG 가져오기 (캐시 우선) ─────────────────────────────
    public async Task<string?> GetSvgAsync(string prefix, string name, string color = "#E0E0E0",
                                           int size = 24, CancellationToken ct = default)
    {
        var cacheDir = Path.Combine(_cacheRoot, prefix);
        var cachePath = Path.Combine(cacheDir, $"{name}.svg");

        if (File.Exists(cachePath))
            return await File.ReadAllTextAsync(cachePath, ct);

        try
        {
            // 색상 없이 원본 SVG 저장 (색상은 렌더링 시 적용)
            var url = $"{BaseApi}/{prefix}/{name}.svg";
            var svg = await _http.GetStringAsync(url, ct);
            if (!string.IsNullOrEmpty(svg))
            {
                Directory.CreateDirectory(cacheDir);
                await File.WriteAllTextAsync(cachePath, svg, ct);
                return svg;
            }
        }
        catch { /* 오프라인 or 오류 */ }
        return null;
    }

    // SVG를 색상 적용 버전으로 반환 (파일 저장 안 함)
    public static string ApplyColor(string svg, string color)
    {
        // currentColor를 지정 색상으로 치환
        return svg.Replace("currentColor", color)
                  // fill 없는 경우 추가
                  .Replace("<svg ", $"<svg fill=\"{color}\" ");
    }

    // SVG를 특정 크기로 래핑
    public static string WrapSvg(string svgBody, int width, int height, string color)
    {
        // Iconify API SVG는 이미 완전한 SVG임
        return svgBody.Replace("currentColor", color);
    }

    // SVG를 파일 경로로 반환 (SharpVectors용)
    public async Task<string?> GetSvgPathAsync(string prefix, string name,
                                                CancellationToken ct = default)
    {
        var cacheDir = Path.Combine(_cacheRoot, prefix);
        var cachePath = Path.Combine(cacheDir, $"{name}.svg");
        if (File.Exists(cachePath)) return cachePath;

        var svg = await GetSvgAsync(prefix, name, ct: ct);
        return svg != null ? cachePath : null;
    }

    // ── 아이콘 목록 인덱싱 (Iconify JSON 형식 다운로드) ───────
    public async Task<List<IconEntry>> FetchCollectionIconsAsync(
        string prefix, IProgress<(int done, int total)>? progress = null,
        CancellationToken ct = default)
    {
        var icons = new List<IconEntry>();

        // Iconify collection metadata API
        var url = $"{BaseApi}/{prefix}.json?info=true";
        try
        {
            var json = await _http.GetStringAsync(url, ct);
            var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            // icon 목록
            if (!root.TryGetProperty("icons", out var iconsEl)) return icons;

            // aliases 포함
            var aliasesEl = root.TryGetProperty("aliases", out var ae) ? ae : (JsonElement?)null;

            var entries = iconsEl.EnumerateObject().ToList();
            int total = entries.Count;
            int done = 0;

            // 카테고리 정보 (있으면)
            Dictionary<string, string> catMap = new();
            if (root.TryGetProperty("categories", out var cats))
            {
                foreach (var cat in cats.EnumerateObject())
                {
                    if (cat.Value.ValueKind == JsonValueKind.Array)
                        foreach (var n in cat.Value.EnumerateArray())
                            catMap[n.GetString() ?? ""] = cat.Name;
                }
            }

            foreach (var entry in entries)
            {
                ct.ThrowIfCancellationRequested();
                var iconName = entry.Name;
                var tags = catMap.TryGetValue(iconName, out var cat) ? cat : "";

                // tags 속성에서 추가 태그
                if (entry.Value.TryGetProperty("tags", out var tagsEl))
                    tags += "," + string.Join(",", tagsEl.EnumerateArray().Select(t => t.GetString() ?? ""));

                icons.Add(new IconEntry
                {
                    Id = $"{prefix}:{iconName}",
                    Prefix = prefix,
                    Name = iconName,
                    Tags = tags.Trim(',')
                });

                done++;
                if (done % 100 == 0)
                    progress?.Report((done, total));
            }

            // aliases도 추가
            if (aliasesEl.HasValue)
            {
                foreach (var alias in aliasesEl.Value.EnumerateObject())
                {
                    icons.Add(new IconEntry
                    {
                        Id = $"{prefix}:{alias.Name}",
                        Prefix = prefix,
                        Name = alias.Name,
                        Tags = ""
                    });
                }
            }

            progress?.Report((total, total));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // 네트워크 오류 시 빈 목록 반환
        }

        return icons;
    }

    // ── Iconify 검색 API ────────────────────────────────────
    public async Task<List<string>> SearchOnlineAsync(string query, int limit = 100,
                                                       CancellationToken ct = default)
    {
        var result = new List<string>();
        try
        {
            var url = $"{BaseApi}/search?query={Uri.EscapeDataString(query)}&limit={limit}";
            var json = await _http.GetStringAsync(url, ct);
            var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("icons", out var arr))
                foreach (var el in arr.EnumerateArray())
                    result.Add(el.GetString() ?? "");
        }
        catch { }
        return result;
    }

    // ── 캐시 통계 ────────────────────────────────────────────
    public static long GetCacheSizeBytes()
    {
        if (!Directory.Exists(_cacheRoot)) return 0;
        return Directory.GetFiles(_cacheRoot, "*.svg", SearchOption.AllDirectories)
                        .Sum(f => new FileInfo(f).Length);
    }

    public static void ClearCache()
    {
        if (Directory.Exists(_cacheRoot))
            Directory.Delete(_cacheRoot, true);
    }

    public void Dispose() => _http.Dispose();
}
