using System.Net.Http;
using System.Text.Json;
using Stock.Fetch.Models;

namespace Stock.Fetch.Services;

/// <summary>
/// 단축 종목코드(5~6자리 영숫자)로 한글 종목명을 조회한다. KRX 종목검색 무인증 finder API 사용.
/// 주식은 finder_stkisu, ETF/ETN 등 증권상품(2025년~ 영숫자 코드 0193T0 등 포함)은
/// finder_secuprodisu에만 있으므로 두 finder를 순차/병합 조회한다.
/// (KRX 통계 데이터는 차단되지만 종목 자동완성 finder는 무인증으로 동작한다.)
/// </summary>
public sealed class NameResolver(HttpClient http)
{
    private const string Endpoint = "http://data.krx.co.kr/comm/bldAttendant/getJsonData.cmd";
    private const string Referer = "http://data.krx.co.kr/contents/MDC/MDI/mdiLoader/index.cmd";

    private const string StockFinder = "dbms/comm/finder/finder_stkisu";        // 주식
    private const string SecuProdFinder = "dbms/comm/finder/finder_secuprodisu"; // ETF·ETN·ELW 등 증권상품

    /// <summary>
    /// 코드 또는 이름(일부)으로 종목 후보 목록을 검색. 주식 + 증권상품(ETF/ETN) finder 결과를
    /// 병합(주식 우선·코드 중복 제거)해 반환한다.
    /// </summary>
    public async Task<List<StockHit>> SearchAsync(string text, CancellationToken ct = default)
    {
        var hits = new List<StockHit>();
        var seen = new HashSet<string>();

        foreach (var (bld, defaultMarket) in new[] { (StockFinder, ""), (SecuProdFinder, "ETF/ETN") })
        {
            var arr = await FinderBlock1Async(bld, text, ct);
            if (arr is not { ValueKind: JsonValueKind.Array }) continue;
            foreach (var e in arr.Value.EnumerateArray())
            {
                string code = GetStr(e, "short_code");
                if (code.Length == 0 || !seen.Add(code)) continue;
                string market = GetStr(e, "marketName");
                hits.Add(new StockHit(code, GetStr(e, "codeName"),
                    string.IsNullOrEmpty(market) ? defaultMarket : market));
            }
        }
        return hits;
    }

    /// <summary>종목명을 반환. 주식 finder → 증권상품 finder 순으로 조회하고 못 찾으면 null.</summary>
    public async Task<string?> LookupAsync(string code, CancellationToken ct = default)
    {
        // 두 finder에서 단축코드 정확 일치(영숫자 코드는 대소문자 무시)를 먼저 찾고,
        // 정확 일치가 어디에도 없을 때만 가장 먼저 나온 부분 일치 항목으로 폴백.
        string? firstPartial = null;
        foreach (var bld in new[] { StockFinder, SecuProdFinder })
        {
            var arr = await FinderBlock1Async(bld, code, ct);
            if (arr is not { ValueKind: JsonValueKind.Array }) continue;

            foreach (var e in arr.Value.EnumerateArray())
                if (string.Equals(GetStr(e, "short_code"), code, StringComparison.OrdinalIgnoreCase))
                    return GetStr(e, "codeName");
            if (firstPartial is null && arr.Value.GetArrayLength() > 0)
                firstPartial = GetStr(arr.Value[0], "codeName");
        }
        return firstPartial;
    }

    /// <summary>지정 finder 호출 후 block1 배열(검색 결과)을 Clone해 반환. 실패 시 null.</summary>
    private async Task<JsonElement?> FinderBlock1Async(string bld, string text, CancellationToken ct)
    {
        var form = new Dictionary<string, string>
        {
            ["bld"] = bld,
            ["mktsel"] = "ALL",
            ["typeNo"] = "0",
            ["searchText"] = text,
        };

        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Post, Endpoint)
            {
                Content = new FormUrlEncodedContent(form)
            };
            req.Headers.Referrer = new Uri(Referer);
            req.Headers.Add("X-Requested-With", "XMLHttpRequest");
            using var resp = await http.SendAsync(req, ct);
            string s = await resp.Content.ReadAsStringAsync(ct);

            using var doc = JsonDocument.Parse(s);
            if (doc.RootElement.TryGetProperty("block1", out var arr) && arr.ValueKind == JsonValueKind.Array)
                return arr.Clone();
            return null;
        }
        catch
        {
            return null; // 네트워크/파싱 실패는 '못 찾음'으로 처리
        }
    }

    private static string GetStr(JsonElement el, string prop)
        => el.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? "" : "";
}
