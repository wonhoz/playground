namespace Dict.Cast.Services;

/// <summary>
/// MyMemory 무료 번역 API (en → ko).
/// API 키 불필요, 500단어/일 무료. 번역 결과는 AppDatabase에 캐싱됨.
/// </summary>
public class TranslationService : IDisposable
{
    readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(12) };

    /// <summary>
    /// 영어 텍스트를 한국어로 번역. 실패/오프라인 시 null 반환.
    /// </summary>
    public async Task<string?> TranslateAsync(string text, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        try
        {
            var url = "https://api.mymemory.translated.net/get?q="
                    + Uri.EscapeDataString(text)
                    + "&langpair=en|ko";
            var json = await _http.GetStringAsync(url, ct);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            // responseStatus 200 확인
            if (root.TryGetProperty("responseStatus", out var status) && status.GetInt32() != 200)
                return null;

            var translated = root.GetProperty("responseData")
                                 .GetProperty("translatedText")
                                 .GetString();

            // MyMemory가 쿼리 그대로 반환하거나 오류 메시지 돌려보낼 때 null 처리
            if (translated == null || translated.Equals(text, StringComparison.OrdinalIgnoreCase))
                return null;
            if (translated.StartsWith("QUERY LENGTH LIMIT", StringComparison.OrdinalIgnoreCase))
                return null;

            return translated;
        }
        catch (OperationCanceledException) { return null; }
        catch { return null; }
    }

    public void Dispose() => _http.Dispose();
}
