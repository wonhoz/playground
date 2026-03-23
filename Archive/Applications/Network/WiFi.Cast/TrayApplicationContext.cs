using WiFiCast.Views;

namespace WiFiCast;

/// <summary>시스템 트레이 애플리케이션 컨텍스트</summary>
public sealed class TrayApplicationContext : ApplicationContext
{
    private readonly NotifyIcon   _tray;
    private readonly MainForm     _mainForm = new();
    private readonly System.Windows.Forms.Timer _autoScan = new() { Interval = 30_000 };
    private readonly ToolStripMenuItem _statusItem;

    public TrayApplicationContext()
    {
        _statusItem = new ToolStripMenuItem("스캔 대기 중") { Enabled = false };

        var menu = new ContextMenuStrip
        {
            Renderer      = new DarkMenuRenderer(),
            ShowImageMargin = false,
            BackColor     = Color.FromArgb(28, 28, 28),
            ForeColor     = Color.FromArgb(224, 224, 224),
            Font          = new Font("Segoe UI", 9.5f),
        };

        menu.Items.Add(new ToolStripMenuItem("📶 WiFi.Cast 열기", null, (_, _) => ShowMain()));
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(_statusItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(new ToolStripMenuItem("🔍 지금 스캔", null, (_, _) => ScanNow()));
        menu.Items.Add(new ToolStripMenuItem("📄 CSV 내보내기", null, (_, _) => { ShowMain(); _mainForm.Scan(); }));
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(new ToolStripMenuItem("❌ 종료", null, (_, _) => Exit()));

        _tray = new NotifyIcon
        {
            Text             = "WiFi.Cast — Wi-Fi 채널 분석기",
            Icon             = SystemIcons.Application,
            ContextMenuStrip = menu,
            Visible          = true,
        };
        _tray.DoubleClick += (_, _) => ShowMain();

        // 앱 아이콘 로드 시도
        TryLoadIcon();

        // 실행 알림
        _tray.ShowBalloonTip(2000, "WiFi.Cast", "Wi-Fi 채널 분석기가 시작되었습니다.", ToolTipIcon.Info);

        // 자동 스캔 타이머
        _autoScan.Tick += (_, _) => ScanNow();
        _autoScan.Start();

        // 초기 스캔
        ScanNow();
    }

    private void ShowMain()
    {
        if (!_mainForm.Visible)
        {
            _mainForm.Show();
            _mainForm.WindowState = FormWindowState.Normal;
        }
        _mainForm.Activate();
    }

    private void ScanNow()
    {
        _statusItem.Text = "스캔 중...";
        Task.Run(WlanScanner.Scan).ContinueWith(t =>
        {
            var nets = t.Result;
            int best24 = ChannelAnalyzer.BestChannel24(nets);
            int best5  = ChannelAnalyzer.BestChannel5(nets);
            _statusItem.Text = $"감지: {nets.Count}개  |  최적 2.4G: CH{best24}  5G: CH{best5}";
            _tray.Text = $"WiFi.Cast  |  {nets.Count}개  2.4G→CH{best24}";

            if (_mainForm.Visible)
                _mainForm.Scan();
        }, TaskScheduler.FromCurrentSynchronizationContext());
    }

    private void Exit()
    {
        _autoScan.Stop();
        _tray.Visible = false;
        Application.Exit();
    }

    private void TryLoadIcon()
    {
        try
        {
            string iconPath = Path.Combine(
                AppContext.BaseDirectory, "Resources", "app.ico");
            if (File.Exists(iconPath))
                _tray.Icon = new Icon(iconPath);
        }
        catch { /* 아이콘 로드 실패 시 기본값 유지 */ }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _autoScan.Dispose();
            _tray.Dispose();
            _mainForm.Dispose();
        }
        base.Dispose(disposing);
    }
}
