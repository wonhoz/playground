using System.IO;
using System.Net.Http;
using System.Text;
using HtmlAgilityPack;

namespace LinkVault.Services;

public class SnapshotService
{
    private static readonly string SnapshotDir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "LinkVault", "snapshots");

    private static readonly string FaviconDir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "LinkVault", "favicons");

    private readonly HttpClient _http;

    public SnapshotService()
    {
        Directory.CreateDirectory(SnapshotDir);
        Directory.CreateDirectory(FaviconDir);

        _http = new HttpClient(new HttpClientHandler
        {
            AllowAutoRedirect = true,
            MaxAutomaticRedirections = 5
        });
        _http.DefaultRequestHeaders.Add("User-Agent",
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 Chrome/120 Safari/537.36");
        _http.Timeout = TimeSpan.FromSeconds(20);
    }

    /// <summary>URL에서 제목/설명 등 메타데이터를 가져옵니다.</summary>
    public async Task<PageMeta> FetchMetaAsync(string url)
    {
        try
        {
            var html = await _http.GetStringAsync(url);
            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            var title = doc.DocumentNode.SelectSingleNode("//title")?.InnerText?.Trim() ?? "";
            var desc = doc.DocumentNode
                .SelectSingleNode("//meta[@name='description' or @property='og:description']")
                ?.GetAttributeValue("content", "") ?? "";

            var ogTitle = doc.DocumentNode
                .SelectSingleNode("//meta[@property='og:title']")
                ?.GetAttributeValue("content", "");
            if (!string.IsNullOrWhiteSpace(ogTitle)) title = ogTitle;

            return new PageMeta
            {
                Title = HtmlEntity.DeEntitize(title).Trim(),
                Description = HtmlEntity.DeEntitize(desc).Trim()
            };
        }
        catch
        {
            return new PageMeta { Title = url };
        }
    }

    /// <summary>HTML을 로컬에 저장하고 경로를 반환합니다.</summary>
    public async Task<string?> SaveSnapshotAsync(long bookmarkId, string url)
    {
        try
        {
            var html = await _http.GetStringAsync(url);
            var path = Path.Combine(SnapshotDir, $"{bookmarkId}.html");

            var baseTag = $"<base href=\"{url}\">";
            html = html.Replace("<head>", $"<head>{baseTag}", StringComparison.OrdinalIgnoreCase);
            if (!html.Contains("<head>", StringComparison.OrdinalIgnoreCase))
                html = baseTag + html;

            await File.WriteAllTextAsync(path, html, Encoding.UTF8);
            return path;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>스냅샷 HTML에서 본문 텍스트를 추출합니다 (FTS 색인용).</summary>
    public static string ExtractBodyText(string htmlPath)
    {
        try
        {
            var html = File.ReadAllText(htmlPath);
            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            var scripts = doc.DocumentNode.SelectNodes("//script|//style");
            if (scripts != null)
                foreach (var node in scripts.ToList())
                    node.Remove();

            var words = doc.DocumentNode.InnerText
                .Replace("\r\n", " ").Replace("\n", " ").Replace("\t", " ")
                .Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Take(2000);
            return string.Join(" ", words);
        }
        catch { return ""; }
    }

    /// <summary>파비콘 다운로드</summary>
    public async Task<string?> SaveFaviconAsync(long bookmarkId, string url)
    {
        try
        {
            var uri = new Uri(url);
            var faviconUrl = $"{uri.Scheme}://{uri.Host}/favicon.ico";
            var bytes = await _http.GetByteArrayAsync(faviconUrl);
            if (bytes.Length < 100) return null;
            var path = Path.Combine(FaviconDir, $"{bookmarkId}.ico");
            await File.WriteAllBytesAsync(path, bytes);
            return path;
        }
        catch { return null; }
    }

    public static string GetSnapshotPath(long id) => Path.Combine(SnapshotDir, $"{id}.html");
    public static bool SnapshotExists(long id) => File.Exists(GetSnapshotPath(id));
}

public record PageMeta
{
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
}
