using System.Net.Http;
using System.Text;
using System.Text.Json;
using Stock.Catch.Models;

namespace Stock.Catch.Services;

/// <summary>
/// Slack Incoming Webhook으로 보유 종목 알림을 전송한다. Webhook URL이 비어 있으면 조용히 건너뛴다.
/// (Stock.Watch SlackNotifier에서 포팅)
/// </summary>
public sealed class SlackNotifier(AppConfig config) : IDisposable
{
    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(10) };

    public bool IsConfigured => !string.IsNullOrWhiteSpace(config.SlackWebhookUrl);

    /// <summary>
    /// 전송 채널 결정: 종목 코드가 관심 종목에 있고 전용 채널이 지정돼 있으면 그 채널,
    /// 없으면 전역 기본 채널(SlackChannel). 둘 다 비어 있으면 webhook 기본 채널.
    /// </summary>
    private string? ChannelFor(string? code)
    {
        if (!string.IsNullOrEmpty(code))
        {
            var own = config.Watchlist.FirstOrDefault(w =>
                string.Equals(w.Symbol, code, StringComparison.OrdinalIgnoreCase))?.SlackChannel;
            if (!string.IsNullOrWhiteSpace(own)) return own;
        }
        return string.IsNullOrWhiteSpace(config.SlackChannel) ? null : config.SlackChannel;
    }

    private string BuildPayload(string text, string? code = null)
        => ChannelFor(code) is { } channel
            ? JsonSerializer.Serialize(new { channel, username = "Stock.Catch", text })
            : JsonSerializer.Serialize(new { username = "Stock.Catch", text });

    public async Task SendAsync(PortfolioAlert a, CancellationToken ct = default)
    {
        if (!IsConfigured) return;

        string emoji = a.IsUp ? ":red_circle:" : ":large_blue_circle:";   // 한국식: 상승 빨강 / 하락 파랑
        string arrow = a.IsUp ? "▲" : "▼";
        var sb = new StringBuilder();
        sb.AppendLine($"{emoji} *{a.Display}* {arrow} 평단比 *{a.ReturnPct:+0.0;-0.0}%* (임계 {Math.Abs(a.Threshold):0.#}%) · *{a.Price:N0}원*");
        sb.AppendLine($"평단 {a.AvgPrice:N0} × {a.Quantity}주 · 평가손익 {a.EvalPL:+#,0;-#,0;0}원");

        await PostAsync(BuildPayload(sb.ToString(), a.Code), ct);
    }

    /// <summary>관심 종목 추세 알림(시작 알림 또는 기준값 대비 step 변동). 모바일 가독성 우선 — 2~3줄.</summary>
    public async Task SendWatchAlertAsync(WatchAlert a, CancellationToken ct = default)
    {
        if (!IsConfigured) return;

        string emoji = a.IsUp ? ":red_circle:" : ":large_blue_circle:";   // 한국식: 상승 빨강 / 하락 파랑
        string arrow = a.IsUp ? "▲" : "▼";
        var sb = new StringBuilder();

        if (a.IsStartup)
        {
            sb.AppendLine($":satellite_antenna: *{a.Item}* 모니터링 시작 · 전일比 *{a.CurrentRate:+0.0;-0.0;0.0}%* · {a.PriceText}");
            sb.AppendLine($"조건 {a.RulesText}");
        }
        else
        {
            sb.AppendLine($"{emoji} *{a.Item}* {arrow} *{a.Delta:+0.0;-0.0}%p* ({a.WindowMinutes:0.#}분/{a.Step:0.###}%) · 전일比 {a.CurrentRate:+0.0;-0.0;0.0}% · *{a.PriceText}*");
            if (a.ReversalProb is { } rp)
                sb.AppendLine($":arrows_counterclockwise: {a.ReversalDirText} ~{rp:P0} ({a.ReversalText} · {a.ReversalBasis})");
        }

        await PostAsync(BuildPayload(sb.ToString(), a.Item.Symbol), ct);
    }

    /// <summary>모니터링 시작 시 종목별 현재 수준을 한 메시지로 요약.</summary>
    public async Task SendWatchStartupSummaryAsync(IReadOnlyList<WatchAlert> alerts, CancellationToken ct = default)
    {
        if (!IsConfigured || alerts.Count == 0) return;

        var sb = new StringBuilder();
        sb.AppendLine($":satellite_antenna: *관심 종목 모니터링 시작* ({alerts.Count}종목)");
        foreach (var a in alerts)
        {
            string arrow = a.CurrentRate >= 0 ? "▲" : "▼";
            sb.AppendLine($"• {a.Item} {arrow} {a.CurrentRate:+0.0;-0.0;0.0}% · {a.PriceText}");
        }
        await PostAsync(BuildPayload(sb.ToString()), ct);
    }

    /// <summary>관심 종목 다이제스트(주기 요약) 알림.</summary>
    public async Task SendDigestAsync(IReadOnlyList<WatchQuote> quotes, CancellationToken ct = default)
    {
        if (!IsConfigured || quotes.Count == 0) return;

        var sb = new StringBuilder();
        sb.AppendLine($":bar_chart: *관심 종목 시세* ({DateTime.Now:yyyy-MM-dd HH:mm})");
        foreach (var q in quotes)
        {
            string arrow = q.ChangeRate >= 0 ? "▲" : "▼";
            sb.AppendLine($"• {q.Item} ({q.Item.MarketLabel}) {arrow} {q.ChangeRate:+0.0;-0.0;0.0}% · {FormatPrice(q.Item, q.Price)}");
        }
        await PostAsync(BuildPayload(sb.ToString()), ct);
    }

    private static string FormatPrice(WatchItem item, decimal price)
        => item.IsIndex ? $"{price:N2}p"
           : item.Market == MarketKind.US ? $"${price:N2}" : $"{price:N0}원";

    /// <summary>시세 조회 연속 실패 알림(엣지 — 임계 도달 시 1회).</summary>
    public async Task SendFetchFailureAsync(string display, string context, string source, string reason, int fails, string? code = null, CancellationToken ct = default)
    {
        if (!IsConfigured) return;
        var sb = new StringBuilder();
        sb.AppendLine($":warning: *{display}* 조회 실패 {fails}회 ({context} · {source})");
        sb.AppendLine(reason);
        await PostAsync(BuildPayload(sb.ToString(), code), ct);
    }

    /// <summary>시세 조회 복구 알림(실패 알림을 보냈던 종목이 정상 복구됐을 때 1회).</summary>
    public async Task SendFetchRecoveryAsync(string display, string context, string? code = null, CancellationToken ct = default)
    {
        if (!IsConfigured) return;
        await PostAsync(BuildPayload($":white_check_mark: *{display}* 조회 복구 ({context})", code), ct);
    }

    /// <summary>매수/익절 래더·갭다운 알림.</summary>
    public async Task SendLadderAlertAsync(LadderAlert a, CancellationToken ct = default)
    {
        if (!IsConfigured) return;
        (string emoji, string head) = a.Kind switch
        {
            LadderAlertKind.BuyTouch => (":large_blue_circle:", "매수 호가 도달"),
            LadderAlertKind.SellBreak => (":red_circle:", "익절가 돌파"),
            _ => (":warning:", "갭다운 취소선"),
        };
        var sb = new StringBuilder();
        sb.AppendLine($"{emoji} *{head}* · *{a.Display}*");
        sb.AppendLine(a.Kind == LadderAlertKind.GapDown ? a.Detail : $"{a.Detail} · 현재 {a.Price:N0}원");
        await PostAsync(BuildPayload(sb.ToString(), a.Code), ct);
    }

    /// <summary>1분봉 시그널(바닥 반등·골든크로스·고점 경고·데드크로스) 알림. 경광등 이모지로 강조해 눈에 띄게.</summary>
    public async Task SendMinuteSignalAsync(MinuteSignal s, CancellationToken ct = default)
    {
        if (!IsConfigured) return;
        (string emoji, string head) = s.Kind switch
        {
            MinuteSignalKind.MorningBrief => (":sunny:", "개장 브리핑"),
            MinuteSignalKind.Rebound => (":rotating_light::chart_with_upwards_trend:", "바닥 반등 시그널"),
            MinuteSignalKind.FollowThrough => (":rotating_light::arrow_upper_right:", "반등 지속 (직후 양봉)"),
            MinuteSignalKind.GoldenCross => (":rotating_light::white_check_mark:", "반등 확인 (골든크로스)"),
            MinuteSignalKind.StrongGoldenCross => (":rotating_light::fire:", "강력 확인 (골든크로스)"),
            MinuteSignalKind.WeakGoldenCross => (":warning:", "약한 확인 (횡보성 크로스)"),
            MinuteSignalKind.TopWarn => (":rotating_light::chart_with_downwards_trend:", "고점 경고 시그널"),
            _ => (":rotating_light::small_red_triangle_down:", "하락 확인 (데드크로스)"),
        };
        var sb = new StringBuilder();
        sb.AppendLine($"{emoji} *{s.TfLabel}{head}* · *{s.Display}* · *{s.Price:N0}원* ({s.Time:HH:mm})");
        sb.AppendLine(s.Detail);
        await PostAsync(BuildPayload(sb.ToString(), s.Code), ct);
    }

    /// <summary>장 세션 시작·마감 5분 전 알림.</summary>
    public async Task SendMarketScheduleAsync(string title, string detail, CancellationToken ct = default)
    {
        if (!IsConfigured) return;
        var sb = new StringBuilder();
        sb.AppendLine($":bell: *{title}*");
        if (!string.IsNullOrEmpty(detail)) sb.AppendLine($"• {detail}");
        await PostAsync(BuildPayload(sb.ToString()), ct);
    }

    /// <summary>설정 화면의 "테스트 전송" 버튼용.</summary>
    public async Task SendTestAsync(CancellationToken ct = default)
    {
        if (!IsConfigured) throw new InvalidOperationException("Slack Webhook URL이 설정되지 않았습니다.");
        await PostAsync(BuildPayload(":white_check_mark: *Stock.Catch* 연결 테스트 — 알림이 정상 수신되었습니다."), ct);
    }

    private async Task PostAsync(string payload, CancellationToken ct)
    {
        using var content = new StringContent(payload, Encoding.UTF8, "application/json");
        using var resp = await _http.PostAsync(config.SlackWebhookUrl, content, ct);
        resp.EnsureSuccessStatusCode();
    }

    public void Dispose() => _http.Dispose();
}
