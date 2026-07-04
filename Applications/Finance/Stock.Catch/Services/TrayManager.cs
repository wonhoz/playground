using System.Drawing;
using System.Windows.Forms;
using DrawingIcon = System.Drawing.Icon;

namespace Stock.Catch.Services;

/// <summary>
/// 알림 영역(트레이) 상주 아이콘 + 다크 우클릭 메뉴 + 풍선 알림 관리.
/// 앱을 닫아도 트레이에 남아 백그라운드 모니터링을 지속한다.
/// </summary>
public sealed class TrayManager : IDisposable
{
    private readonly NotifyIcon _icon;
    private readonly ToolStripMenuItem _monitorItem;

    public event Action? OpenRequested;
    public event Action? ToggleMonitorRequested;
    public event Action? SettingsRequested;
    public event Action? ExitRequested;

    public TrayManager()
    {
        var menu = new ContextMenuStrip
        {
            Renderer = new DarkMenuRenderer(),
            ShowImageMargin = false,
            Font = new Font("Segoe UI", 9.5f),
            AutoSize = true,
        };

        var open = new ToolStripMenuItem("열기");
        open.Click += (_, _) => OpenRequested?.Invoke();

        _monitorItem = new ToolStripMenuItem("▶ 모니터링 시작");
        _monitorItem.Click += (_, _) => ToggleMonitorRequested?.Invoke();

        var settings = new ToolStripMenuItem("설정");
        settings.Click += (_, _) => SettingsRequested?.Invoke();

        // 시그널 로그: Slack 2줄 알림의 상세 분석(근거·컨텍스트)이 일자별 CSV로 쌓이는 폴더.
        var signalLog = new ToolStripMenuItem("시그널 로그 폴더");
        signalLog.Click += (_, _) =>
        {
            try
            {
                string dir = System.IO.Path.Combine(AppConfig.ConfigDir, "signals");
                System.IO.Directory.CreateDirectory(dir);
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(dir) { UseShellExecute = true });
            }
            catch { /* 탐색기 열기 실패 무시 */ }
        };

        var exit = new ToolStripMenuItem("종료");
        exit.Click += (_, _) => ExitRequested?.Invoke();

        menu.Items.Add(open);
        menu.Items.Add(_monitorItem);
        menu.Items.Add(signalLog);
        menu.Items.Add(settings);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(exit);

        _icon = new NotifyIcon
        {
            Icon = LoadIcon(),
            Visible = true,
            Text = "Stock.Catch — 보유 종목 모니터링",
            ContextMenuStrip = menu,
        };
        _icon.DoubleClick += (_, _) => OpenRequested?.Invoke();
    }

    public void SetMonitorState(bool running)
        => _monitorItem.Text = running ? "■ 모니터링 중지" : "▶ 모니터링 시작";

    public void ShowBalloon(string title, string text, bool warning = false)
        => _icon.ShowBalloonTip(5000, title, text, warning ? ToolTipIcon.Warning : ToolTipIcon.Info);

    private static DrawingIcon LoadIcon()
    {
        try
        {
            var info = System.Windows.Application.GetResourceStream(
                new Uri("pack://application:,,,/Resources/app.ico"));
            if (info != null) return new DrawingIcon(info.Stream);
        }
        catch { /* 폴백 */ }
        return SystemIcons.Application;
    }

    public void Dispose()
    {
        _icon.Visible = false;
        _icon.Dispose();
    }
}
