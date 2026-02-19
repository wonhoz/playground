using System.Net.Http;
using System.Text;
using System.Text.Json;
using AiClip.Models;

namespace AiClip.Services
{
    public class ClaudeService
    {
        private readonly AppSettings _settings;

        private static readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(90) };
        private const string ApiUrl = "https://api.anthropic.com/v1/messages";
        private const string Model  = "claude-haiku-4-5-20251001";

        public ClaudeService(AppSettings settings) => _settings = settings;

        // ── 4가지 AI 작업 ─────────────────────────────────────────

        public Task<string> SummarizeAsync(string text, CancellationToken ct = default) =>
            CallAsync(
                "You are a helpful assistant that creates clear, concise summaries. " +
                "Respond in the same language as the input text. " +
                "Structure the summary with bullet points when appropriate.",
                $"Summarize the following text:\n\n{text}",
                ct);

        public Task<string> TranslateAsync(string text, string targetLanguage, CancellationToken ct = default) =>
            CallAsync(
                $"You are a professional translator. " +
                $"Translate the given text to {targetLanguage}. " +
                $"Output only the translation — no explanations, no notes.",
                text,
                ct);

        public Task<string> ProofreadAsync(string text, CancellationToken ct = default) =>
            CallAsync(
                "You are a professional editor and proofreader. " +
                "Fix grammar, spelling, punctuation, and style errors. " +
                "First show the corrected text between <corrected> tags, " +
                "then briefly list the changes made. " +
                "Respond in the same language as the input.",
                $"Proofread and correct:\n\n{text}",
                ct);

        public Task<string> ConvertCodeAsync(string text, string targetLanguage, CancellationToken ct = default) =>
            CallAsync(
                $"You are an expert programmer. " +
                $"Convert the given code to {targetLanguage}. " +
                $"Output only the converted code in a code block. " +
                $"If the input is not code, generate equivalent {targetLanguage} code from the description.",
                $"Convert to {targetLanguage}:\n\n{text}",
                ct);

        // ── Anthropic Messages API ────────────────────────────────

        private async Task<string> CallAsync(string system, string user, CancellationToken ct)
        {
            if (!_settings.HasApiKey)
                throw new InvalidOperationException(
                    "Anthropic API 키가 설정되지 않았습니다.\n" +
                    "트레이 아이콘 우클릭 → Settings에서 입력해주세요.");

            var payload = JsonSerializer.Serialize(new
            {
                model      = Model,
                max_tokens = 2048,
                system,
                messages   = new[] { new { role = "user", content = user } }
            });

            using var request = new HttpRequestMessage(HttpMethod.Post, ApiUrl)
            {
                Content = new StringContent(payload, Encoding.UTF8, "application/json")
            };
            request.Headers.Add("x-api-key",          _settings.ApiKey);
            request.Headers.Add("anthropic-version",  "2023-06-01");

            using var response = await _http.SendAsync(request, ct);

            if (!response.IsSuccessStatusCode)
            {
                var errBody = await response.Content.ReadAsStringAsync(ct);
                try
                {
                    using var errDoc = JsonDocument.Parse(errBody);
                    var msg = errDoc.RootElement
                        .GetProperty("error").GetProperty("message").GetString();
                    throw new HttpRequestException($"API 오류 ({(int)response.StatusCode}): {msg}");
                }
                catch (Exception e) when (e is not HttpRequestException)
                {
                    throw new HttpRequestException($"API 오류 ({(int)response.StatusCode}): {errBody}");
                }
            }

            var json = await response.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement
                .GetProperty("content")[0]
                .GetProperty("text")
                .GetString() ?? "";
        }
    }
}
