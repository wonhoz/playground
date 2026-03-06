using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace Tray.Stats;

sealed class TrayApp : ApplicationContext
{
    readonly StatsCollector _collector = new();
    readonly PopupForm      _popup     = new();
    readonly NotifyIcon     _tray      = new();
    readonly System.Windows.Forms.Timer _timer = new();

    // 임계치
    const float AlertCpu  = 90f;
    const float AlertRam  = 90f;
    const float AlertDisk = 95f;

    bool _cpuAlerted, _ramAlerted, _diskAlerted;

    public TrayApp()
    {
        _popup.Bind(_collector);

        // 트레이 아이콘 초기 설정
        _tray.Visible = true;
        _tray.Text    = "Tray.Stats";
        _tray.Icon    = CreateTrayIcon(0);
        _tray.ContextMenuStrip = BuildMenu();
        _tray.MouseClick += OnTrayClick;

        // 실행 알림
        _tray.ShowBalloonTip(2000, "Tray.Stats", "시스템 모니터가 트레이에서 실행 중입니다.", ToolTipIcon.Info);

        // 1초 타이머
        _timer.Interval = 1000;
        _timer.Tick    += OnTick;
        _timer.Start();
    }

    void OnTick(object? s, EventArgs e)
    {
        var snap = _collector.Sample();
        _tray.Icon = CreateTrayIcon(snap.CpuPercent);

        string tooltip = $"CPU {snap.CpuPercent:F0}%  RAM {snap.RamPercent:F0}%  DISK {snap.DiskPercent:F0}%";
        _tray.Text = tooltip.Length > 63 ? tooltip[..63] : tooltip;

        _popup.UpdateSnapshot(snap);

        // 임계치 알림
        CheckAlert(snap.CpuPercent  >= AlertCpu,  ref _cpuAlerted,  "CPU",  snap.CpuPercent);
        CheckAlert(snap.RamPercent  >= AlertRam,  ref _ramAlerted,  "RAM",  snap.RamPercent);
        CheckAlert(snap.DiskPercent >= AlertDisk, ref _diskAlerted, "DISK", snap.DiskPercent);
    }

    void CheckAlert(bool over, ref bool alerted, string name, float val)
    {
        if (over && !alerted)
        {
            _tray.ShowBalloonTip(3000, "Tray.Stats 경고",
                $"{name} 사용률이 {val:F0}%에 도달했습니다.", ToolTipIcon.Warning);
            alerted = true;
        }
        else if (!over && alerted)
        {
            alerted = false;
        }
    }

    void OnTrayClick(object? s, MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left)
        {
            if (_popup.Visible)
                _popup.Hide();
            else
                _popup.ShowNear(_tray);
        }
    }

    ContextMenuStrip BuildMenu()
    {
        var menu = new ContextMenuStrip
        {
            Renderer       = new DarkMenuRenderer(),
            AutoSize       = true,
            ShowImageMargin = false,
            Font           = new Font("Segoe UI", 9.5f)
        };

        var itemStats  = new ToolStripMenuItem("📊  통계 보기");
        itemStats.Click += (_, _) =>
        {
            if (_popup.Visible) _popup.Hide();
            else _popup.ShowNear(_tray);
        };

        var itemExit = new ToolStripMenuItem("✕  종료");
        itemExit.Click += (_, _) => ExitApp();

        menu.Items.Add(itemStats);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(itemExit);
        return menu;
    }

    void ExitApp()
    {
        _timer.Stop();
        _popup.Dispose();
        _tray.Visible = false;
        _tray.Dispose();
        _collector.Dispose();
        Application.Exit();
    }

    // ── 트레이 아이콘 동적 드로잉 ────────────────────────────────────────────
    Icon CreateTrayIcon(float cpu)
    {
        const int sz = 32;
        using var bmp = new Bitmap(sz, sz, PixelFormat.Format32bppArgb);
        using var g   = Graphics.FromImage(bmp);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(Color.Transparent);

        // 배경 원
        Color bg = Color.FromArgb(0xFF, 0x1A, 0x1A, 0x2A);
        using var bgBrush = new SolidBrush(bg);
        g.FillEllipse(bgBrush, 1, 1, sz - 2, sz - 2);

        // CPU 호
        Color arc = cpu < 50 ? Color.FromArgb(0xFF, 0x4A, 0xDE, 0x80)
                  : cpu < 80 ? Color.FromArgb(0xFF, 0xFA, 0xCC, 0x15)
                             : Color.FromArgb(0xFF, 0xF8, 0x71, 0x71);
        float sweep = 360f * Math.Clamp(cpu / 100f, 0, 1);
        using var arcPen = new Pen(arc, 4f);
        g.DrawArc(arcPen, 5, 5, sz - 10, sz - 10, -90f, sweep);

        // 중앙 퍼센트 텍스트
        string txt = cpu < 10 ? $"{cpu:F0}" : cpu >= 100 ? "!!" : $"{cpu:F0}";
        using var font  = new Font("Segoe UI", cpu >= 100 ? 7.5f : 7f, FontStyle.Bold);
        using var brush = new SolidBrush(Color.FromArgb(0xFF, 0xE0, 0xE0, 0xE0));
        var fmt = new StringFormat { Alignment = StringAlignment.Center,
                                     LineAlignment = StringAlignment.Center };
        g.DrawString(txt, font, brush, new RectangleF(0, 0, sz, sz), fmt);

        var hIcon = bmp.GetHicon();
        var icon  = Icon.FromHandle(hIcon);
        var copy  = (Icon)icon.Clone();
        DestroyIcon(hIcon);
        return copy;
    }

    [DllImport("user32.dll")]
    static extern bool DestroyIcon(IntPtr hIcon);

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _timer.Dispose();
            _collector.Dispose();
            _popup.Dispose();
            _tray.Dispose();
        }
        base.Dispose(disposing);
    }
}

// ── 다크 컨텍스트 메뉴 렌더러 ─────────────────────────────────────────────────
sealed class DarkMenuRenderer : ToolStripRenderer
{
    static readonly Color MenuBg    = Color.FromArgb(0xFF, 0x1E, 0x1E, 0x2E);
    static readonly Color HoverBg   = Color.FromArgb(0xFF, 0x2D, 0x2D, 0x42);
    static readonly Color TxtColor  = Color.FromArgb(0xFF, 0xE0, 0xE0, 0xE0);
    static readonly Color SepColor  = Color.FromArgb(0xFF, 0x33, 0x33, 0x4A);
    static readonly Color BorderCol = Color.FromArgb(0xFF, 0x33, 0x33, 0x4A);

    protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e)
    {
        using var b = new SolidBrush(MenuBg);
        e.Graphics.FillRectangle(b, e.AffectedBounds);
        using var p = new Pen(BorderCol);
        e.Graphics.DrawRectangle(p, 0, 0, e.ToolStrip.Width - 1, e.ToolStrip.Height - 1);
    }

    protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
    {
        if (e.Item.Selected)
        {
            using var b = new SolidBrush(HoverBg);
            var r = new Rectangle(2, 0, e.Item.Width - 4, e.Item.Height);
            e.Graphics.FillRectangle(b, r);
        }
    }

    protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
    {
        e.TextColor = TxtColor;
        base.OnRenderItemText(e);
    }

    protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
    {
        int y = e.Item.Height / 2;
        using var p = new Pen(SepColor);
        e.Graphics.DrawLine(p, 8, y, e.Item.Width - 8, y);
    }
}
