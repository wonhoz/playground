using System.Net.Http;
using System.Text;
using System.Text.Json;
using Stock.Fetch.Models;

namespace Stock.Fetch.Services;

/// <summary>
/// Slack Incoming Webhook으로 보유 종목 알림을 전송한다. Webhook URL이 비어 있으면 조용히 건너뛴다.
/// (Stock.Watch SlackNotifier에서 포팅)
/// </summary>
public sealed class SlackNotifier(AppConfig config) : IDisposable
{
    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(10) };

    public bool IsConfigured => !string.IsNullOrWhiteSpace(config.SlackWebhookUrl);

    private string BuildPayload(string text)
        => string.IsNullOrWhiteSpace(config.SlackChannel)
            ? JsonSerializer.Serialize(new { username = "Stock.Fetch", text })
            : JsonSerializer.Serialize(new { channel = config.SlackChannel, username = "Stock.Fetch", text });

    public async Task SendAsync(PortfolioAlert a, CancellationToken ct = default)
    {
        if (!IsConfigured) return;

        string emoji = a.IsUp ? ":red_circle:" : ":large_blue_circle:";   // 한국식: 상승 빨강 / 하락 파랑
        string arrow = a.IsUp ? "▲" : "▼";
        var sb = new StringBuilder();
        sb.AppendLine($"{emoji} *{a.Display}* {arrow} 평단 대비 *{a.ReturnPct:+0.0;-0.0}%* (임계 {Math.Abs(a.Threshold):0.#}%)");
        sb.AppendLine($"• 현재가 *{a.Price:N0}원* / 평단 {a.AvgPrice:N0}원 · {a.Quantity}주");
        sb.AppendLine($"• 평가손익 {a.EvalPL:+#,0;-#,0;0}원");
        sb.AppendLine($"• 시각 {a.Time:yyyy-MM-dd HH:mm:ss}");

        await PostAsync(BuildPayload(sb.ToString()), ct);
    }

    /// <summary>설정 화면의 "테스트 전송" 버튼용.</summary>
    public async Task SendTestAsync(CancellationToken ct = default)
    {
        if (!IsConfigured) throw new InvalidOperationException("Slack Webhook URL이 설정되지 않았습니다.");
        await PostAsync(BuildPayload(":white_check_mark: *Stock.Fetch* 연결 테스트 — 알림이 정상 수신되었습니다."), ct);
    }

    private async Task PostAsync(string payload, CancellationToken ct)
    {
        using var content = new StringContent(payload, Encoding.UTF8, "application/json");
        using var resp = await _http.PostAsync(config.SlackWebhookUrl, content, ct);
        resp.EnsureSuccessStatusCode();
    }

    public void Dispose() => _http.Dispose();
}
