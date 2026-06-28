using System.Net.Http;
using System.Text.Json;

namespace Stock.Fetch.Services;

/// <summary>
/// 단축 종목코드(6자리)로 한글 종목명을 조회한다. KRX 종목검색(finder_stkisu) 무인증 API 사용.
/// (KRX 통계 데이터는 차단되지만 종목 자동완성 finder는 무인증으로 동작한다.)
/// </summary>
public sealed class NameResolver(HttpClient http)
{
    private const string Endpoint = "http://data.krx.co.kr/comm/bldAttendant/getJsonData.cmd";
    private const string Referer = "http://data.krx.co.kr/contents/MDC/MDI/mdiLoader/index.cmd";

    /// <summary>종목명을 반환. 못 찾으면 null.</summary>
    public async Task<string?> LookupAsync(string code, CancellationToken ct = default)
    {
        var form = new Dictionary<string, string>
        {
            ["bld"] = "dbms/comm/finder/finder_stkisu",
            ["mktsel"] = "ALL",
            ["typeNo"] = "0",
            ["searchText"] = code,
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
            string text = await resp.Content.ReadAsStringAsync(ct);

            using var doc = JsonDocument.Parse(text);
            if (!doc.RootElement.TryGetProperty("block1", out var arr) || arr.ValueKind != JsonValueKind.Array)
                return null;

            // 단축코드 정확 일치 우선, 없으면 첫 항목.
            foreach (var e in arr.EnumerateArray())
                if (GetStr(e, "short_code") == code) return GetStr(e, "codeName");
            return arr.GetArrayLength() > 0 ? GetStr(arr[0], "codeName") : null;
        }
        catch
        {
            return null; // 네트워크/파싱 실패는 '못 찾음'으로 처리
        }
    }

    private static string GetStr(JsonElement el, string prop)
        => el.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? "" : "";
}
