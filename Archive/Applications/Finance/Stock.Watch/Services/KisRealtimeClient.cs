using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Collections.Concurrent;

namespace Stock.Watch.Services;

/// <summary>실시간 체결 1틱.</summary>
public sealed record RealtimeTick(string Code, decimal Price, decimal ChangeRate, long CumVolume, DateTime Time);

/// <summary>
/// 한국투자증권 실시간 시세 WebSocket 클라이언트.
/// 체결가(tr_id H0STCNT0, 평문 파이프 구분)를 구독하고 PINGPONG에 응답하며, 끊기면 자동 재연결한다.
/// </summary>
public sealed class KisRealtimeClient : IDisposable
{
    private const string TrId = "H0STCNT0"; // 주식체결가

    private readonly KisApiClient _api;
    private readonly ConcurrentDictionary<string, byte> _codes = new();
    private ClientWebSocket? _ws;
    private CancellationTokenSource? _cts;
    private readonly SemaphoreSlim _sendLock = new(1, 1);

    public KisRealtimeClient(KisApiClient api) => _api = api;

    public bool IsRunning => _cts is { IsCancellationRequested: false };

    public event Action<RealtimeTick>? Tick;
    public event Action<string>? StatusChanged;
    public event Action<string>? ErrorOccurred;

    public void Start(IEnumerable<string> codes)
    {
        if (IsRunning) return;
        foreach (var c in codes) _codes.TryAdd(c, 0);
        _cts = new CancellationTokenSource();
        _ = RunAsync(_cts.Token);
    }

    public void Stop()
    {
        _cts?.Cancel();
        _cts = null;
        try { _ws?.Abort(); } catch { }
        _ws = null;
    }

    public async Task AddCodeAsync(string code)
    {
        if (!_codes.TryAdd(code, 0)) return;
        if (_ws?.State == WebSocketState.Open) await SubscribeAsync(code, true, _cts?.Token ?? CancellationToken.None);
    }

    public async Task RemoveCodeAsync(string code)
    {
        if (!_codes.TryRemove(code, out _)) return;
        if (_ws?.State == WebSocketState.Open) await SubscribeAsync(code, false, _cts?.Token ?? CancellationToken.None);
    }

    // ──────────────────────────── 연결 루프 ────────────────────────────
    private async Task RunAsync(CancellationToken ct)
    {
        int backoff = 2;
        while (!ct.IsCancellationRequested)
        {
            try
            {
                string approvalKey = await _api.GetApprovalKeyAsync(ct);
                _ws = new ClientWebSocket();
                await _ws.ConnectAsync(new Uri(_api.WebSocketUrl), ct);
                StatusChanged?.Invoke("실시간 연결됨");
                backoff = 2;

                _currentApprovalKey = approvalKey;
                foreach (var code in _codes.Keys)
                    await SubscribeAsync(code, true, ct);

                await ReceiveLoopAsync(ct);
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex) { ErrorOccurred?.Invoke($"실시간 연결 오류: {ex.Message}"); }

            if (ct.IsCancellationRequested) break;
            StatusChanged?.Invoke($"실시간 재연결 대기 {backoff}s");
            try { await Task.Delay(TimeSpan.FromSeconds(backoff), ct); } catch { break; }
            backoff = Math.Min(backoff * 2, 30);
        }
        StatusChanged?.Invoke("실시간 중지");
    }

    private string _currentApprovalKey = string.Empty;

    private async Task SubscribeAsync(string code, bool subscribe, CancellationToken ct)
    {
        if (_ws is not { State: WebSocketState.Open }) return;
        var msg = new
        {
            header = new
            {
                approval_key = _currentApprovalKey,
                custtype = "P",
                tr_type = subscribe ? "1" : "2",
                content_type = "utf-8"
            },
            body = new { input = new { tr_id = TrId, tr_key = code } }
        };
        // content-type 키에 하이픈이 필요하므로 직접 치환
        string json = JsonSerializer.Serialize(msg).Replace("\"content_type\"", "\"content-type\"");
        await SendRawAsync(json, ct);
    }

    private async Task SendRawAsync(string text, CancellationToken ct)
    {
        if (_ws is not { State: WebSocketState.Open }) return;
        await _sendLock.WaitAsync(ct);
        try
        {
            var bytes = Encoding.UTF8.GetBytes(text);
            await _ws.SendAsync(bytes, WebSocketMessageType.Text, true, ct);
        }
        finally { _sendLock.Release(); }
    }

    private async Task ReceiveLoopAsync(CancellationToken ct)
    {
        var buffer = new byte[8192];
        var sb = new StringBuilder();
        while (!ct.IsCancellationRequested && _ws is { State: WebSocketState.Open })
        {
            sb.Clear();
            WebSocketReceiveResult result;
            do
            {
                result = await _ws.ReceiveAsync(buffer, ct);
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    StatusChanged?.Invoke("실시간 서버 연결 종료");
                    return;
                }
                sb.Append(Encoding.UTF8.GetString(buffer, 0, result.Count));
            } while (!result.EndOfMessage);

            HandleMessage(sb.ToString(), ct);
        }
    }

    private void HandleMessage(string message, CancellationToken ct)
    {
        if (message.Length == 0) return;

        // JSON 제어 메시지(구독 응답 / PINGPONG)
        if (message[0] == '{')
        {
            try
            {
                using var doc = JsonDocument.Parse(message);
                if (doc.RootElement.TryGetProperty("header", out var h) &&
                    h.TryGetProperty("tr_id", out var t) && t.GetString() == "PINGPONG")
                {
                    _ = SendRawAsync(message, ct); // 그대로 echo
                }
            }
            catch { /* 무시 */ }
            return;
        }

        // 실시간 데이터: flag|tr_id|count|body(^로 구분)
        var parts = message.Split('|');
        if (parts.Length < 4 || parts[1] != TrId) return;
        if (!int.TryParse(parts[2], out int count) || count <= 0) return;

        var f = parts[3].Split('^');
        int per = f.Length / count;
        if (per < 14) return;

        // 가장 최근(마지막) 체결 레코드 사용
        int b = (count - 1) * per;
        string code = f[b + 0];
        if (!decimal.TryParse(f[b + 2], out var price)) return;
        decimal.TryParse(f[b + 5], out var rate);
        long.TryParse(f[b + 13], out var vol);
        var time = ParseHms(f[b + 1]);

        Tick?.Invoke(new RealtimeTick(code, price, rate, vol, time));
    }

    private static DateTime ParseHms(string hms)
    {
        if (hms.Length == 6 &&
            int.TryParse(hms[..2], out int h) && int.TryParse(hms.Substring(2, 2), out int m) && int.TryParse(hms.Substring(4, 2), out int s))
        {
            var today = DateTime.Today;
            return today.AddHours(h).AddMinutes(m).AddSeconds(s);
        }
        return DateTime.Now;
    }

    public void Dispose()
    {
        Stop();
        _ws?.Dispose();
        _sendLock.Dispose();
    }
}
