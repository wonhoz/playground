using System.Diagnostics;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using ApiProbe.Models;

namespace ApiProbe.Services;

public record HttpResponse(
    int    StatusCode,
    string StatusText,
    string Headers,
    string Body,
    long   ElapsedMs);

public static class HttpService
{
    private static readonly HttpClient _client = new()
    {
        Timeout = TimeSpan.FromSeconds(30)
    };

    public static async Task<HttpResponse> SendAsync(ApiRequest req, Dictionary<string, string> envVars)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            var url     = ApplyEnv(req.Url, envVars);
            using var request = new HttpRequestMessage(new HttpMethod(req.Method), url);

            foreach (var h in req.Headers)
            {
                if (h.Enabled && !string.IsNullOrWhiteSpace(h.Key))
                    request.Headers.TryAddWithoutValidation(h.Key, ApplyEnv(h.Value, envVars));
            }

            if (!string.IsNullOrWhiteSpace(req.Body) &&
                req.Method is "POST" or "PUT" or "PATCH")
            {
                var body = ApplyEnv(req.Body, envVars);
                request.Content = new StringContent(body, Encoding.UTF8, req.ContentType);
            }

            var resp = await _client.SendAsync(request);
            sw.Stop();

            var headerSb = new StringBuilder();
            foreach (var h in resp.Headers)
                headerSb.AppendLine($"{h.Key}: {string.Join(", ", h.Value)}");
            foreach (var h in resp.Content.Headers)
                headerSb.AppendLine($"{h.Key}: {string.Join(", ", h.Value)}");

            var rawBody    = await resp.Content.ReadAsStringAsync();
            var prettyBody = TryPrettyJson(rawBody);

            return new HttpResponse(
                (int)resp.StatusCode,
                resp.ReasonPhrase ?? "",
                headerSb.ToString().TrimEnd(),
                prettyBody,
                sw.ElapsedMilliseconds);
        }
        catch (UriFormatException ex)
        {
            sw.Stop();
            return new HttpResponse(0, "잘못된 URL", "", $"URL 형식 오류: {ex.Message}", sw.ElapsedMilliseconds);
        }
        catch (TaskCanceledException)
        {
            sw.Stop();
            return new HttpResponse(0, "시간 초과", "", "요청 시간이 초과되었습니다 (30초).", sw.ElapsedMilliseconds);
        }
        catch (HttpRequestException ex)
        {
            sw.Stop();
            return new HttpResponse(0, "연결 실패", "", $"연결 오류: {ex.Message}", sw.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            sw.Stop();
            return new HttpResponse(0, "오류", "", ex.Message, sw.ElapsedMilliseconds);
        }
    }

    private static string ApplyEnv(string text, Dictionary<string, string> vars)
    {
        foreach (var kv in vars)
            text = text.Replace($"{{{{{kv.Key}}}}}", kv.Value);
        return text;
    }

    private static string TryPrettyJson(string raw)
    {
        try
        {
            var obj = JsonSerializer.Deserialize<object>(raw);
            return JsonSerializer.Serialize(obj, new JsonSerializerOptions { WriteIndented = true });
        }
        catch { return raw; }
    }
}
