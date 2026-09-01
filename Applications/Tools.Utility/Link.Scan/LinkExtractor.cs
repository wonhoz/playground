using System.Diagnostics;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace LinkScan;

/// <summary>추출된 링크 한 건.</summary>
internal sealed record LinkItem(Uri Uri, string Text);

/// <summary>페이지 1건에 대한 추출 결과.</summary>
internal sealed record ScanResult(
    Uri Target,
    Uri BaseUri,
    HttpStatusCode StatusCode,
    string Charset,
    long ByteLength,
    TimeSpan Elapsed,
    int TotalHrefCount,
    int UniqueCount,
    IReadOnlyList<LinkItem> Links);

internal sealed partial class LinkExtractor(HttpClient httpClient)
{
    private readonly HttpClient _httpClient = httpClient;

    public async Task<ScanResult> ScanAsync(Uri target, LinkScope scope, int maxCount, CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();

        using var request = new HttpRequestMessage(HttpMethod.Get, target);
        request.Headers.Accept.ParseAdd("text/html,application/xhtml+xml;q=0.9,*/*;q=0.8");

        using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseContentRead, cancellationToken);
        var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
        stopwatch.Stop();

        response.EnsureSuccessStatusCode();

        var encoding = ResolveEncoding(response, bytes);
        var html = encoding.GetString(bytes);

        // 리다이렉트를 따라간 경우 최종 주소가 상대 경로 해석의 기준이 된다.
        var finalUri = response.RequestMessage?.RequestUri ?? target;
        var baseUri = ResolveBaseUri(html, finalUri);

        var hrefs = ExtractHrefs(html);
        var links = Normalize(hrefs, baseUri, scope, maxCount, out var uniqueCount);

        return new ScanResult(
            target,
            baseUri,
            response.StatusCode,
            encoding.WebName,
            bytes.LongLength,
            stopwatch.Elapsed,
            hrefs.Count,
            uniqueCount,
            links);
    }

    /// <summary>&lt;a&gt; / &lt;area&gt; 여는 태그를 훑어 href 와 링크 텍스트를 뽑는다.</summary>
    private static List<(string Href, string Text)> ExtractHrefs(string html)
    {
        var results = new List<(string, string)>();

        foreach (var tag in AnchorTagRegex().EnumerateMatches(html))
        {
            var tagText = html.Substring(tag.Index, tag.Length);
            var href = HrefRegex().Match(tagText);
            if (!href.Success)
                continue;

            var url = WebUtility.HtmlDecode(href.Groups["u"].Value).Trim();
            if (url.Length == 0)
                continue;

            results.Add((url, ExtractLinkText(html, tag.Index + tag.Length)));
        }

        return results;
    }

    /// <summary>여는 태그 뒤부터 첫 &lt;/a&gt; 까지를 링크 텍스트로 본다.</summary>
    private static string ExtractLinkText(string html, int startIndex)
    {
        if (startIndex >= html.Length)
            return string.Empty;

        var close = html.IndexOf("</a", startIndex, StringComparison.OrdinalIgnoreCase);
        if (close < 0)
            return string.Empty;

        // 다음 링크의 여는 태그를 넘어섰다면 (닫는 태그 누락) 텍스트가 없는 것으로 처리한다.
        var nextOpen = html.IndexOf("<a", startIndex, StringComparison.OrdinalIgnoreCase);
        if (nextOpen >= 0 && nextOpen < close)
            return string.Empty;

        var inner = html[startIndex..close];
        var text = WebUtility.HtmlDecode(TagRegex().Replace(inner, " "));
        return WhitespaceRegex().Replace(text, " ").Trim();
    }

    /// <summary>절대 주소 변환 · 범위 필터 · 중복 제거를 한 번에 처리한다.</summary>
    private static List<LinkItem> Normalize(
        List<(string Href, string Text)> hrefs,
        Uri baseUri,
        LinkScope scope,
        int maxCount,
        out int uniqueCount)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var inScope = new List<LinkItem>();

        foreach (var (href, text) in hrefs)
        {
            if (IsNonNavigable(href))
                continue;

            if (!Uri.TryCreate(baseUri, href, out var absolute))
                continue;

            if (absolute.Scheme is not ("http" or "https"))
                continue;

            // 프래그먼트는 같은 문서 내 위치일 뿐이므로 제거 후 중복 판정한다.
            absolute = StripFragment(absolute);

            if (!seen.Add(absolute.AbsoluteUri))
                continue;

            if (!IsInScope(absolute, baseUri, scope))
                continue;

            inScope.Add(new LinkItem(absolute, text));
        }

        uniqueCount = seen.Count;
        return inScope.Count > maxCount ? inScope.GetRange(0, maxCount) : inScope;
    }

    private static bool IsNonNavigable(string href)
    {
        if (href.StartsWith('#'))
            return true;

        var colon = href.IndexOf(':');
        if (colon <= 0)
            return false;

        // 스킴처럼 보이는 접두사만 검사한다 (경로 안의 콜론은 무시).
        var scheme = href[..colon];
        if (scheme.Contains('/') || scheme.Contains('?'))
            return false;

        return scheme.ToLowerInvariant() is "javascript" or "mailto" or "tel" or "data" or "about" or "file" or "sms";
    }

    private static Uri StripFragment(Uri uri)
    {
        if (string.IsNullOrEmpty(uri.Fragment))
            return uri;

        var builder = new UriBuilder(uri) { Fragment = string.Empty };
        return builder.Uri;
    }

    private static bool IsInScope(Uri candidate, Uri baseUri, LinkScope scope)
    {
        if (scope == LinkScope.All)
            return true;

        if (!string.Equals(candidate.Host, baseUri.Host, StringComparison.OrdinalIgnoreCase))
            return false;

        if (scope == LinkScope.Host)
            return true;

        return candidate.AbsolutePath.StartsWith(DirectoryOf(baseUri), StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>기준 주소의 디렉토리 경로 (/docs/index.html → /docs/).</summary>
    private static string DirectoryOf(Uri uri)
    {
        var path = uri.AbsolutePath;
        var slash = path.LastIndexOf('/');
        return slash >= 0 ? path[..(slash + 1)] : "/";
    }

    /// <summary>&lt;base href&gt; 가 있으면 상대 경로 해석 기준을 그쪽으로 옮긴다.</summary>
    private static Uri ResolveBaseUri(string html, Uri requestUri)
    {
        var match = BaseTagRegex().Match(html);
        if (!match.Success)
            return requestUri;

        var value = WebUtility.HtmlDecode(match.Groups["u"].Value).Trim();
        return Uri.TryCreate(requestUri, value, out var baseUri) && baseUri.Scheme is "http" or "https"
            ? baseUri
            : requestUri;
    }

    /// <summary>Content-Type → BOM → meta charset 순으로 인코딩을 판정한다.</summary>
    private static Encoding ResolveEncoding(HttpResponseMessage response, byte[] bytes)
    {
        var charset = response.Content.Headers.ContentType?.CharSet?.Trim('"', '\'');
        if (TryGetEncoding(charset, out var fromHeader))
            return fromHeader;

        if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
            return Encoding.UTF8;

        var head = Encoding.ASCII.GetString(bytes, 0, Math.Min(bytes.Length, 4096));
        var match = MetaCharsetRegex().Match(head);
        if (match.Success && TryGetEncoding(match.Groups["c"].Value, out var fromMeta))
            return fromMeta;

        return Encoding.UTF8;
    }

    private static bool TryGetEncoding(string? name, out Encoding encoding)
    {
        encoding = Encoding.UTF8;
        if (string.IsNullOrWhiteSpace(name))
            return false;

        try
        {
            encoding = Encoding.GetEncoding(name.Trim().Trim('"', '\''));
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    [GeneratedRegex(@"<(?:a|area)\s[^>]*>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex AnchorTagRegex();

    [GeneratedRegex("""\bhref\s*=\s*(?:"(?<u>[^"]*)"|'(?<u>[^']*)'|(?<u>[^\s"'>]+))""", RegexOptions.IgnoreCase)]
    private static partial Regex HrefRegex();

    [GeneratedRegex("""<base\s[^>]*?\bhref\s*=\s*(?:"(?<u>[^"]*)"|'(?<u>[^']*)'|(?<u>[^\s"'>]+))""", RegexOptions.IgnoreCase)]
    private static partial Regex BaseTagRegex();

    [GeneratedRegex("""charset\s*=\s*["']?(?<c>[A-Za-z0-9_\-]+)""", RegexOptions.IgnoreCase)]
    private static partial Regex MetaCharsetRegex();

    [GeneratedRegex("<[^>]*>", RegexOptions.Singleline)]
    private static partial Regex TagRegex();

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();
}
