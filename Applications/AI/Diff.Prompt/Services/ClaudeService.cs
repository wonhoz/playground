using System.Diagnostics;
using System.Net.Http;
using System.Text;
using System.Text.Json;

namespace DiffPrompt.Services;

/// <summary>
/// Anthropic Messages API (스트리밍) 직접 HTTP 구현
/// </summary>
public class ClaudeService
{
    private static readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(300) };
    private const string ApiUrl = "https://api.anthropic.com/v1/messages";
    private const string AnthropicVersion = "2023-06-01";

    private readonly string _apiKey;

    public ClaudeService(string apiKey)
    {
        _apiKey = apiKey;
    }

    /// <summary>
    /// 스트리밍 메시지 전송. 각 텍스트 청크가 도착할 때마다 onChunk 호출.
    /// </summary>
    public async Task<(string output, int inputTokens, int outputTokens, double latencyMs)>
        RunAsync(string systemPrompt, string userMessage, string model,
                 Action<string> onChunk, CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        var sb = new StringBuilder();
        int inputTokens = 0, outputTokens = 0;

        var payload = JsonSerializer.Serialize(new
        {
            model,
            max_tokens = 4096,
            stream = true,
            system = systemPrompt,
            messages = new[] { new { role = "user", content = userMessage } }
        });

        using var request = new HttpRequestMessage(HttpMethod.Post, ApiUrl)
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json")
        };
        request.Headers.Add("x-api-key", _apiKey);
        request.Headers.Add("anthropic-version", AnthropicVersion);

        using var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);

        if (!response.IsSuccessStatusCode)
        {
            var errBody = await response.Content.ReadAsStringAsync(ct);
            throw new HttpRequestException($"API 오류 ({(int)response.StatusCode}): {errBody}");
        }

        using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var reader = new System.IO.StreamReader(stream);

        while (!reader.EndOfStream)
        {
            ct.ThrowIfCancellationRequested();
            var line = await reader.ReadLineAsync(ct);
            if (line == null) break;
            if (!line.StartsWith("data: ")) continue;

            var json = line["data: ".Length..];
            if (json == "[DONE]") break;

            try
            {
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;
                var type = root.GetProperty("type").GetString();

                switch (type)
                {
                    case "message_start":
                        inputTokens = root
                            .GetProperty("message")
                            .GetProperty("usage")
                            .GetProperty("input_tokens").GetInt32();
                        break;

                    case "content_block_delta":
                        if (root.GetProperty("delta").TryGetProperty("text", out var textEl))
                        {
                            var text = textEl.GetString() ?? "";
                            sb.Append(text);
                            onChunk(text);
                        }
                        break;

                    case "message_delta":
                        if (root.GetProperty("usage").TryGetProperty("output_tokens", out var otEl))
                            outputTokens = otEl.GetInt32();
                        break;
                }
            }
            catch (JsonException) { /* 파싱 실패 라인 무시 */ }
        }

        sw.Stop();
        return (sb.ToString(), inputTokens, outputTokens, sw.Elapsed.TotalMilliseconds);
    }
}
