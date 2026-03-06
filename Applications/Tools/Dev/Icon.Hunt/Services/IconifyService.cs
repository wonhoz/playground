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

    // ── 아이콘 목록 인덱싱 (Iconify /collection API 사용) ─────
    public async Task<List<IconEntry>> FetchCollectionIconsAsync(
        string prefix, IProgress<(int done, int total)>? progress = null,
        CancellationToken ct = default)
    {
        var icons = new List<IconEntry>();

        // Iconify API v3: /collection?prefix=xxx
        var url = $"{BaseApi}/collection?prefix={Uri.EscapeDataString(prefix)}";
        var json = await _http.GetStringAsync(url, ct);
        var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        // 카테고리별 태그 매핑 구성
        // categories: { "Category": ["icon1", "icon2", ...], ... }
        // uncategorized: ["icon3", "icon4", ...]
        var catMap = new Dictionary<string, string>(StringComparer.Ordinal);

        if (root.TryGetProperty("categories", out var cats))
        {
            foreach (var cat in cats.EnumerateObject())
            {
                if (cat.Value.ValueKind == JsonValueKind.Array)
                    foreach (var n in cat.Value.EnumerateArray())
                        catMap[n.GetString() ?? ""] = cat.Name;
            }
        }

        // 전체 아이콘 이름 수집: uncategorized + categories 값
        var allNames = new List<string>();

        if (root.TryGetProperty("uncategorized", out var unc) &&
            unc.ValueKind == JsonValueKind.Array)
        {
            foreach (var n in unc.EnumerateArray())
            {
                var s = n.GetString();
                if (s != null) allNames.Add(s);
            }
        }

        if (root.TryGetProperty("categories", out var cats2))
        {
            foreach (var cat in cats2.EnumerateObject())
            {
                if (cat.Value.ValueKind == JsonValueKind.Array)
                    foreach (var n in cat.Value.EnumerateArray())
                    {
                        var s = n.GetString();
                        if (s != null && !allNames.Contains(s)) allNames.Add(s);
                    }
            }
        }

        // hidden 아이콘은 제외
        var hiddenSet = new HashSet<string>(StringComparer.Ordinal);
        if (root.TryGetProperty("hidden", out var hidden) &&
            hidden.ValueKind == JsonValueKind.Array)
        {
            foreach (var n in hidden.EnumerateArray())
            {
                var s = n.GetString();
                if (s != null) hiddenSet.Add(s);
            }
        }

        int total = allNames.Count;
        int done = 0;

        foreach (var iconName in allNames)
        {
            ct.ThrowIfCancellationRequested();
            if (hiddenSet.Contains(iconName)) { done++; continue; }

            var tags = catMap.TryGetValue(iconName, out var cat) ? cat : "";

            icons.Add(new IconEntry
            {
                Id = $"{prefix}:{iconName}",
                Prefix = prefix,
                Name = iconName,
                Tags = tags
            });

            done++;
            if (done % 200 == 0)
                progress?.Report((done, total));
        }

        // aliases도 추가
        if (root.TryGetProperty("aliases", out var aliasesEl) &&
            aliasesEl.ValueKind == JsonValueKind.Object)
        {
            foreach (var alias in aliasesEl.EnumerateObject())
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

        progress?.Report((icons.Count, icons.Count));
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

    public void Dispose() { } // _http는 static이므로 해제하지 않음
}
