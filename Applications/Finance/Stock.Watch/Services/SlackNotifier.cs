using System.Net.Http;
using System.Text;
using System.Text.Json;
using Stock.Watch.Models;

namespace Stock.Watch.Services;

/// <summary>
/// Slack Incoming Webhook으로 알림을 전송한다. Webhook URL이 비어 있으면 조용히 건너뛴다.
/// </summary>
public sealed class SlackNotifier : IDisposable
{
    private readonly AppConfig _config;
    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(10) };

    public SlackNotifier(AppConfig config) => _config = config;

    public bool IsConfigured => !string.IsNullOrWhiteSpace(_config.SlackWebhookUrl);

    public async Task SendAsync(AlertLog alert, CancellationToken ct = default)
    {
        if (!IsConfigured) return;

        string emoji = alert.Kind == RuleKind.Buy ? ":large_green_circle:" : ":red_circle:";
        var sb = new StringBuilder();
        sb.AppendLine($"{emoji} *[{alert.KindText} 신호] {alert.Name} ({alert.Code})*");
        sb.AppendLine($"• 현재가: *{alert.Price:N0}원*");
        sb.AppendLine($"• 조건: {alert.RuleSummary}");
        if (!string.IsNullOrWhiteSpace(alert.IndicatorDetail))
            sb.AppendLine($"• 지표: {alert.IndicatorDetail}");
        sb.AppendLine($"• 시각: {alert.Time:yyyy-MM-dd HH:mm:ss}");

        var payload = JsonSerializer.Serialize(new { text = sb.ToString() });
        using var content = new StringContent(payload, Encoding.UTF8, "application/json");
        try
        {
            using var resp = await _http.PostAsync(_config.SlackWebhookUrl, content, ct);
            resp.EnsureSuccessStatusCode();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Slack 전송 실패: {ex.Message}", ex);
        }
    }

    /// <summary>설정 화면의 "테스트 전송" 버튼용.</summary>
    public async Task SendTestAsync(CancellationToken ct = default)
    {
        if (!IsConfigured) throw new InvalidOperationException("Slack Webhook URL이 설정되지 않았습니다.");
        var payload = JsonSerializer.Serialize(new { text = ":white_check_mark: *Stock.Watch* 연결 테스트 — 알림이 정상 수신되었습니다." });
        using var content = new StringContent(payload, Encoding.UTF8, "application/json");
        using var resp = await _http.PostAsync(_config.SlackWebhookUrl, content, ct);
        resp.EnsureSuccessStatusCode();
    }

    public void Dispose() => _http.Dispose();
}
