using System.IO;
using System.Net.Http;

namespace MarkView.Services;

/// <summary>
/// CDN 리소스를 AppData에 캐시 — 이후 오프라인에서도 highlight.js 작동
/// </summary>
public static class CdnCache
{
    public static readonly string CacheDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "MarkView", "cdn-cache");

    // 캐시할 파일 목록 (파일명 → CDN URL)
    private static readonly Dictionary<string, string> _files = new()
    {
        { "highlight.min.js",
          "https://cdn.jsdelivr.net/npm/highlight.js@11/dist/highlight.min.js" },
        { "atom-one-dark.min.css",
          "https://cdn.jsdelivr.net/npm/highlight.js@11/dist/styles/atom-one-dark.min.css" },
        { "atom-one-light.min.css",
          "https://cdn.jsdelivr.net/npm/highlight.js@11/dist/styles/atom-one-light.min.css" },
    };

    // WebView2 가상 호스트명 (https://mv-cdn/<파일명>)
    public const string VirtualHost = "mv-cdn";

    public static bool IsReady { get; private set; }

    // 캐시 파일 최대 보존 기간 (이후 재다운로드)
    private static readonly TimeSpan CacheExpiry = TimeSpan.FromDays(30);

    /// <summary>
    /// 백그라운드에서 CDN 파일을 다운로드·캐시 (없거나 만료된 경우 갱신)
    /// </summary>
    public static async Task WarmupAsync()
    {
        Directory.CreateDirectory(CacheDir);
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
        foreach (var (file, url) in _files)
        {
            var localPath = Path.Combine(CacheDir, file);
            var needsUpdate = !File.Exists(localPath) ||
                              (DateTime.UtcNow - File.GetLastWriteTimeUtc(localPath)) > CacheExpiry;
            if (!needsUpdate) continue;
            try
            {
                var bytes = await client.GetByteArrayAsync(url);
                await File.WriteAllBytesAsync(localPath, bytes);
            }
            catch { /* 실패 시 기존 캐시 또는 CDN URL 사용 */ }
        }
        IsReady = _files.Keys.All(f => File.Exists(Path.Combine(CacheDir, f)));
    }

    public static bool HasFile(string fileName) =>
        File.Exists(Path.Combine(CacheDir, fileName));
}
