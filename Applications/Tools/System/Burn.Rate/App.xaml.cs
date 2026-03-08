using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using System.Windows.Interop;

namespace BurnRate;

public partial class App : System.Windows.Application
{
    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int v, int sz);

    private BatteryService   _svc  = null!;
    private NotifyIcon       _tray = null!;
    private MainWindow?      _win;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // 다크 타이틀바 전역 등록
        EventManager.RegisterClassHandler(typeof(Window), Window.LoadedEvent,
            new RoutedEventHandler((s, _) =>
            {
                var hwnd = new WindowInteropHelper((Window)s).Handle;
                int dark = 1;
                DwmSetWindowAttribute(hwnd, 20, ref dark, sizeof(int));
            }));

        _svc = new BatteryService();
        _svc.BatteryUpdated    += OnBatteryUpdated;
        _svc.StatusChanged     += OnStatusChanged;
        _svc.LowHealthDetected += OnLowHealth;

        _tray = BuildTray();
        _svc.Start();

        _tray.ShowBalloonTip(3000, "Burn.Rate", "배터리 모니터링이 시작되었습니다.", ToolTipIcon.Info);
    }

    private NotifyIcon BuildTray()
    {
        var menu = new ContextMenuStrip
        {
            Renderer = new DarkMenuRenderer(),
            AutoSize  = true,
            ShowImageMargin = false,
            Font = new Font("Segoe UI", 9.5f)
        };
        menu.Items.Add("📊 대시보드 열기", null, (_, _) => ShowMain());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("❌ 종료", null, (_, _) => Shutdown());

        var icon = new NotifyIcon
        {
            Icon             = BuildTrayIcon(0, false),
            Text             = "Burn.Rate — 배터리 모니터링",
            Visible          = true,
            ContextMenuStrip = menu
        };
        icon.DoubleClick += (_, _) => ShowMain();
        return icon;
    }

    private void ShowMain()
    {
        if (_win == null || !_win.IsVisible)
        {
            _win = new MainWindow(_svc);
            _win.Show();
        }
        else
        {
            _win.Activate();
        }
    }

    private void OnBatteryUpdated(BatteryInfo info)
    {
        Dispatcher.Invoke(() =>
        {
            _tray.Icon = BuildTrayIcon(info.ChargePercent, info.IsCharging);
            _tray.Text = $"Burn.Rate  {info.ChargePercent}%  " +
                         $"{(info.IsOnAc ? "⚡ 충전 중" : "🔋 방전 중")}";
            _win?.RefreshData(info);
        });
    }

    private void OnStatusChanged(ChargingStatus status)
    {
        Dispatcher.Invoke(() =>
        {
            var msg = status == ChargingStatus.Charging || status == ChargingStatus.FullyCharged
                ? "AC 어댑터가 연결되었습니다."
                : "AC 어댑터가 분리되었습니다.";
            _tray.ShowBalloonTip(3000, "Burn.Rate", msg, ToolTipIcon.Info);
        });
    }

    private void OnLowHealth()
    {
        Dispatcher.Invoke(() =>
            _tray.ShowBalloonTip(5000, "Burn.Rate ⚠ 배터리 교체 권장",
                "배터리 건강도가 80% 미만입니다. 교체를 고려해 주세요.", ToolTipIcon.Warning));
    }

    // ── 트레이 아이콘 동적 생성 ─────────────────────────────────
    private static System.Drawing.Icon BuildTrayIcon(int pct, bool charging)
    {
        using var bmp = new Bitmap(32, 32, PixelFormat.Format32bppArgb);
        using var g   = Graphics.FromImage(bmp);
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;
        g.Clear(Color.Transparent);

        // 배터리 외형
        var bodyColor = pct > 20
            ? (charging ? Color.FromArgb(255, 80, 200, 100) : Color.FromArgb(255, 50, 160, 255))
            : Color.FromArgb(255, 255, 80, 60);

        using var pen = new Pen(bodyColor, 2f);
        g.DrawRectangle(pen, 2, 5, 24, 22);
        // 터미널
        g.FillRectangle(new SolidBrush(bodyColor), 26, 12, 4, 8);
        // 충전 레벨
        int fillH = (int)(20 * pct / 100.0);
        int fillY = 7 + (20 - fillH);
        g.FillRectangle(new SolidBrush(Color.FromArgb(120, bodyColor)), 4, fillY, 21, fillH);

        // % 텍스트
        string text = pct <= 0 ? "?" : pct >= 100 ? "F" : pct.ToString();
        using var font = new Font("Consolas", text.Length > 2 ? 7f : 8f, System.Drawing.FontStyle.Bold);
        using var br   = new SolidBrush(Color.White);
        var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
        g.DrawString(text, font, br, new RectangleF(2, 5, 24, 22), sf);

        var hicon = bmp.GetHicon();
        return System.Drawing.Icon.FromHandle(hicon);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _tray.Visible = false;
        _tray.Dispose();
        _svc.Dispose();
        base.OnExit(e);
    }
}

// ── 다크 메뉴 렌더러 ────────────────────────────────────────────
internal class DarkMenuRenderer : ToolStripRenderer
{
    protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e)
        => e.Graphics.FillRectangle(new SolidBrush(Color.FromArgb(255, 22, 22, 38)), e.AffectedBounds);

    protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
    {
        var bg = e.Item.Selected
            ? Color.FromArgb(255, 42, 42, 72)
            : Color.FromArgb(255, 22, 22, 38);
        e.Graphics.FillRectangle(new SolidBrush(bg),
            new Rectangle(2, 0, e.Item.Width - 4, e.Item.Height));
    }

    protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
    {
        e.TextColor = Color.FromArgb(255, 224, 224, 240);
        base.OnRenderItemText(e);
    }

    protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
        => e.Graphics.DrawLine(new Pen(Color.FromArgb(255, 46, 46, 80)),
            4, e.Item.Height / 2, e.Item.Width - 4, e.Item.Height / 2);

    protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
        => e.Graphics.DrawRectangle(new Pen(Color.FromArgb(255, 46, 46, 80)),
            new Rectangle(0, 0, e.ToolStrip.Width - 1, e.ToolStrip.Height - 1));
}
