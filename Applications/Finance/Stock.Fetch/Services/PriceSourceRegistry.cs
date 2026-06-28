using System.Net.Http;
using Stock.Fetch.Models;

namespace Stock.Fetch.Services;

/// <summary>
/// 4개 데이터 소스 인스턴스를 생성·보관하고 공유 <see cref="HttpClient"/>를 관리한다.
/// 네이버/Yahoo/KRX는 브라우저 User-Agent가 없으면 차단되므로 공통 헤더로 설정한다.
/// </summary>
public sealed class PriceSourceRegistry : IDisposable
{
    private readonly HttpClient _http;
    private readonly Dictionary<SourceKind, IPriceSource> _sources;
    private readonly NameResolver _nameResolver;

    public PriceSourceRegistry(AppConfig config, Action saveConfig)
    {
        _http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        _http.DefaultRequestHeaders.Add("User-Agent",
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 " +
            "(KHTML, like Gecko) Chrome/124.0 Safari/537.36");
        _http.DefaultRequestHeaders.Add("Accept", "application/json, text/plain, */*");

        _sources = new()
        {
            [SourceKind.Naver] = new NaverPriceSource(_http),
            [SourceKind.Yahoo] = new YahooPriceSource(_http),
            [SourceKind.Daum] = new DaumPriceSource(_http),
            [SourceKind.Kis] = new KisPriceSource(config, saveConfig, _http),
        };
        _nameResolver = new NameResolver(_http);
    }

    public IPriceSource Get(SourceKind kind) => _sources[kind];

    public IReadOnlyList<IPriceSource> All => _sources.Values.ToList();

    /// <summary>단축 종목코드 → 한글 종목명(못 찾으면 null).</summary>
    public Task<string?> LookupNameAsync(string code, CancellationToken ct = default)
        => _nameResolver.LookupAsync(code, ct);

    public void Dispose() => _http.Dispose();
}
