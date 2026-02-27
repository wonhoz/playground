using System.Diagnostics;
using System.Windows.Threading;

namespace SignalFlow;

public partial class MainWindow : Window
{
    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int val, int sz);

    // ── 서버 & 클라이언트 ─────────────────────────────────────────
    private readonly FlowServer          _server    = new();
    private readonly SignalRFlowClient   _srClient  = new();
    private readonly SseFlowClient       _sseClient = new();

    // ── 로그 컬렉션 ───────────────────────────────────────────────
    private readonly ObservableCollection<EventRow> _srItems  = [];
    private readonly ObservableCollection<EventRow> _sseItems = [];
    private int _srCount;
    private int _sseCount;

    // ── 자동 이벤트 타이머 ────────────────────────────────────────
    private readonly DispatcherTimer _autoTimer;
    private readonly string[]        _autoTypes = ["notify", "update", "warning", "error"];
    private readonly Random          _rng       = new();

    private const int MaxItems = 500;

    public MainWindow()
    {
        InitializeComponent();

        // 다크 타이틀바
        SourceInitialized += (_, _) =>
        {
            var handle = new WindowInteropHelper(this).Handle;
            int v = 1;
            DwmSetWindowAttribute(handle, 20, ref v, sizeof(int));
        };

        // ListBox 바인딩
        LbSR.ItemsSource  = _srItems;
        LbSSE.ItemsSource = _sseItems;

        // 자동 타이머 (3초)
        _autoTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
        _autoTimer.Tick += AutoTimer_Tick;

        // 서버 로그
        _server.OnLog = msg => Dispatcher.Invoke(() =>
            TxtStatusBar.Text = $"서버: {msg}");

        // SignalR 클라이언트 이벤트
        _srClient.EventReceived += e => Dispatcher.Invoke(() => AddRow(_srItems, e, ref _srCount, TxtSRCount, DotSR, LbSR));
        _srClient.StatusChanged += s => Dispatcher.Invoke(() => UpdateStatus(s, TxtSRStatus, DotSR));

        // SSE 클라이언트 이벤트
        _sseClient.EventReceived += e => Dispatcher.Invoke(() => AddRow(_sseItems, e, ref _sseCount, TxtSSECount, DotSSE, LbSSE));
        _sseClient.StatusChanged += s => Dispatcher.Invoke(() => UpdateStatus(s, TxtSSEStatus, DotSSE));

        Loaded  += MainWindow_Loaded;
        Closing += MainWindow_Closing;
    }

    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        SetEventButtonsEnabled(false);
    }

    private async void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        _autoTimer.Stop();
        _sseClient.Disconnect();
        await _srClient.DisconnectAsync();
        if (_server.IsRunning) await _server.StopAsync();
    }

    // ─────────────────────────────────────────────────────────────
    //  서버 시작 / 중지
    // ─────────────────────────────────────────────────────────────

    private async void BtnToggle_Click(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded) return;
        BtnToggle.IsEnabled = false;

        try
        {
            if (_server.IsRunning)
            {
                _autoTimer.Stop();
                ChkAuto.IsChecked = false;

                _sseClient.Disconnect();
                await _srClient.DisconnectAsync();
                await _server.StopAsync();

                BtnToggle.Content     = "▶  서버 시작";
                BtnToggle.Background  = new SolidColorBrush(Color.FromRgb(0x06, 0xB6, 0xD4));
                TxtServerUrl.Text     = "";
                TxtBrowserUrl.Text    = "";
                TxtStatusBar.Text     = "서버 중지됨";
                SetEventButtonsEnabled(false);
                BtnCopyUrl.IsEnabled  = false;
            }
            else
            {
                if (!int.TryParse(TxtPort.Text.Trim(), out var port) || port < 1 || port > 65535)
                {
                    MessageBox.Show("포트 번호가 올바르지 않습니다 (1~65535).", "오류",
                                    MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                await _server.StartAsync(port);

                BtnToggle.Content    = "■  서버 중지";
                BtnToggle.Background = new SolidColorBrush(Color.FromRgb(0xEF, 0x44, 0x44));

                var baseUrl = $"http://localhost:{port}";
                TxtServerUrl.Text  = $"● {baseUrl}";
                TxtBrowserUrl.Text = baseUrl;
                TxtStatusBar.Text  = $"서버 실행 중 — {baseUrl}";
                SetEventButtonsEnabled(true);
                BtnCopyUrl.IsEnabled = true;

                // 클라이언트 연결
                await _srClient.ConnectAsync($"{baseUrl}/hub");
                _sseClient.Connect($"{baseUrl}/sse");
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"서버 오류:\n{ex.Message}", "오류",
                            MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            BtnToggle.IsEnabled = true;
        }
    }

    // ─────────────────────────────────────────────────────────────
    //  이벤트 발송
    // ─────────────────────────────────────────────────────────────

    private async void BtnNotify_Click(object sender, RoutedEventArgs e)
        => await PublishAsync("notify",  "알림: 새 메시지가 도착했습니다.");

    private async void BtnUpdate_Click(object sender, RoutedEventArgs e)
        => await PublishAsync("update",  $"업데이트: 데이터 #{_rng.Next(1000, 9999)} 갱신됨.");

    private async void BtnWarn_Click(object sender, RoutedEventArgs e)
        => await PublishAsync("warning", "경고: 리소스 사용량이 높습니다.");

    private async void BtnError_Click(object sender, RoutedEventArgs e)
        => await PublishAsync("error",   "오류: 데이터베이스 연결에 실패했습니다.");

    private async void AutoTimer_Tick(object? sender, EventArgs e)
    {
        var type = _autoTypes[_rng.Next(_autoTypes.Length)];
        var messages = new Dictionary<string, string>
        {
            ["notify"]  = $"자동 알림 #{_rng.Next(100, 999)}",
            ["update"]  = $"자동 업데이트 — 값: {_rng.Next(0, 100)}%",
            ["warning"] = $"자동 경고 — CPU {_rng.Next(70, 100)}%",
            ["error"]   = $"자동 오류 — 코드 {_rng.Next(500, 600)}"
        };
        await PublishAsync(type, messages[type], "auto");
    }

    private async Task PublishAsync(string type, string message, string source = "manual")
    {
        if (!_server.IsRunning) return;
        try
        {
            var evt = new ServerEvent(
                Id:        Guid.NewGuid().ToString("N")[..8],
                Type:      type,
                Message:   message,
                Source:    source,
                Timestamp: DateTime.Now
            );
            await _server.PublishAsync(evt);
        }
        catch (Exception ex)
        {
            TxtStatusBar.Text = $"발송 오류: {ex.Message}";
        }
    }

    // ─────────────────────────────────────────────────────────────
    //  자동 이벤트
    // ─────────────────────────────────────────────────────────────

    private void ChkAuto_Checked(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded) return;
        _autoTimer.Start();
    }

    private void ChkAuto_Unchecked(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded) return;
        _autoTimer.Stop();
    }

    // ─────────────────────────────────────────────────────────────
    //  UI 헬퍼
    // ─────────────────────────────────────────────────────────────

    private void AddRow(ObservableCollection<EventRow> items, ServerEvent evt,
                        ref int count, TextBlock countLabel,
                        Ellipse dot, ListBox lb)
    {
        var row = new EventRow(evt);
        items.Insert(0, row);
        while (items.Count > MaxItems) items.RemoveAt(items.Count - 1);

        count++;
        countLabel.Text = $"{count}개";

        // 연결 상태를 초록으로 (이벤트를 받았으므로)
        dot.Fill = new SolidColorBrush(Color.FromRgb(0x22, 0xC5, 0x5E));
    }

    private static void UpdateStatus(string status, TextBlock label, Ellipse dot)
    {
        label.Text = status;
        dot.Fill = status switch
        {
            "연결됨"    => new SolidColorBrush(Color.FromRgb(0x22, 0xC5, 0x5E)),
            "재연결 중" => new SolidColorBrush(Color.FromRgb(0xF5, 0x9E, 0x0B)),
            "연결 중"   => new SolidColorBrush(Color.FromRgb(0xF5, 0x9E, 0x0B)),
            _            => new SolidColorBrush(Color.FromRgb(0xEF, 0x44, 0x44))
        };
    }

    private void SetEventButtonsEnabled(bool enabled)
    {
        BtnNotify.IsEnabled = enabled;
        BtnUpdate.IsEnabled = enabled;
        BtnWarn.IsEnabled   = enabled;
        BtnError.IsEnabled  = enabled;
        ChkAuto.IsEnabled   = enabled;
    }

    private void BtnClear_Click(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded) return;
        _srItems.Clear();
        _sseItems.Clear();
        _srCount  = 0;
        _sseCount = 0;
        TxtSRCount.Text  = "";
        TxtSSECount.Text = "";
    }

    private void TxtBrowserUrl_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (string.IsNullOrEmpty(TxtBrowserUrl.Text)) return;
        try { Process.Start(new ProcessStartInfo(TxtBrowserUrl.Text) { UseShellExecute = true }); }
        catch { }
    }

    private void BtnCopyUrl_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(TxtBrowserUrl.Text)) return;
        Clipboard.SetText(TxtBrowserUrl.Text);
        TxtStatusBar.Text = $"클립보드에 복사됨: {TxtBrowserUrl.Text}";
    }
}

// ── 이벤트 행 데이터 ──────────────────────────────────────────────

public class EventRow
{
    public string BadgeText  { get; }
    public string BadgeColor { get; }
    public string Time       { get; }
    public string Message    { get; }
    public string Source     { get; }

    public EventRow(ServerEvent e)
    {
        BadgeText  = e.Type.ToUpper();
        BadgeColor = e.Type switch
        {
            "notify"  => "#0E7490",
            "update"  => "#065F46",
            "warning" => "#92400E",
            "error"   => "#7F1D1D",
            _          => "#1E1B4B"
        };
        Time    = e.Timestamp.ToString("HH:mm:ss.fff");
        Message = e.Message;
        Source  = e.Source;
    }
}
