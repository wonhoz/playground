using MouseFlick.Core;
using MouseFlick.Forms;
using MouseFlick.Models;
using MouseFlick.Rendering;
using MouseFlick.Services;

namespace MouseFlick;

internal sealed class TrayApplicationContext : ApplicationContext
{
    private readonly NotifyIcon       _trayIcon;
    private readonly ContextMenuStrip _menu;
    private readonly GlobalMouseHook  _hook;
    private readonly GestureOverlay   _overlay;
    private readonly AppSettings      _settings;
    private readonly ToolStripMenuItem _miEnabled;
    private SettingsForm? _settingsForm;

    public TrayApplicationContext()
    {
        _settings = AppSettings.Load();

        // 첫 실행: 기본 프로필 초기화
        if (_settings.Profiles.Count == 0)
        {
            var defaultPreset = ProfileManager.BuiltinPresets.First(p => p.IsDefault);
            _settings.Profiles.Add(new GestureProfile
            {
                Id           = defaultPreset.Id,
                Name         = defaultPreset.Name,
                IsDefault    = true,
                ProcessNames = [],
                Actions      = [..defaultPreset.Actions.Select(a =>
                    new GestureAction { Gesture = a.Gesture, Description = a.Description, KeyCombo = a.KeyCombo })]
            });
            _settings.Save();
        }

        _overlay = new GestureOverlay();
        _menu    = BuildMenu(out _miEnabled);
        _hook    = new GlobalMouseHook { Threshold = _settings.GestureThreshold };

        _trayIcon = new NotifyIcon
        {
            Icon             = CreateTrayIcon(),
            Text             = "Mouse.Flick",
            ContextMenuStrip = _menu,
            Visible          = true,
        };

        _hook.GestureStarted   += OnGestureStarted;
        _hook.GestureUpdated   += OnGestureUpdated;
        _hook.GestureCompleted += OnGestureCompleted;

        if (_settings.Enabled) _hook.Install();

        // 600ms 딜레이 후 시작 풍선 알림
        var t = new System.Windows.Forms.Timer { Interval = 600 };
        t.Tick += (s, e) =>
        {
            t.Stop(); t.Dispose();
            _trayIcon.ShowBalloonTip(3000, "Mouse.Flick",
                "Mouse.Flick 실행 중입니다!\n오른쪽 버튼을 누른 채 드래그하면 제스처를 인식합니다.",
                ToolTipIcon.Info);
        };
        t.Start();
    }

    // ── 트레이 메뉴 ──────────────────────────────────────────────────────────
    private ContextMenuStrip BuildMenu(out ToolStripMenuItem miEnabled)
    {
        var menu = new ContextMenuStrip
        {
            Renderer        = new DarkMenuRenderer(),
            ShowImageMargin = false,
            AutoSize        = true,
            Font            = new Font("Segoe UI", 9.5f),
        };

        miEnabled = new ToolStripMenuItem("🖱 제스처 활성화", null, OnToggleEnabled)
        {
            Checked      = _settings.Enabled,
            CheckOnClick = true,
        };

        menu.Items.Add(miEnabled);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(new ToolStripMenuItem("⚙ 설정...", null, OnSettings));
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(new ToolStripMenuItem("✕ 종료", null, OnExit));

        return menu;
    }

    private void OnToggleEnabled(object? sender, EventArgs e)
    {
        _settings.Enabled = _miEnabled.Checked;
        _settings.Save();
        if (_settings.Enabled) _hook.Install();
        else                    _hook.Uninstall();
    }

    private void OnSettings(object? sender, EventArgs e)
    {
        if (_settingsForm != null && !_settingsForm.IsDisposed)
        {
            _settingsForm.Activate();
            return;
        }
        _settingsForm = new SettingsForm(_settings);
        _settingsForm.FormClosed += (_, _) =>
        {
            // 설정 변경 반영
            _hook.Threshold = _settings.GestureThreshold;
            if (_settings.Enabled) { _hook.Uninstall(); _hook.Install(); }
        };
        _settingsForm.Show();
    }

    private void OnExit(object? sender, EventArgs e)
    {
        _hook.Uninstall();
        _trayIcon.Visible = false;
        Application.Exit();
    }

    // ── 제스처 처리 ───────────────────────────────────────────────────────────
    private void OnGestureStarted(object? sender, List<Point> pts)
    {
        if (_settings.ShowOverlay) _overlay.BeginGesture(pts);
    }

    private void OnGestureUpdated(object? sender, Point pt)
    {
        if (_settings.ShowOverlay) _overlay.AddPoint(pt);
    }

    private void OnGestureCompleted(object? sender, GestureCompletedEventArgs e)
    {
        var gesture = GestureRecognizer.Recognize(e.Points, _settings.GestureThreshold);

        if (_settings.ShowOverlay)
        {
            if (gesture != null) _overlay.UpdateGestureText(ToArrow(gesture));
            var hideTimer = new System.Windows.Forms.Timer { Interval = 350 };
            hideTimer.Tick += (_, _) => { hideTimer.Stop(); hideTimer.Dispose(); _overlay.EndGesture(); };
            hideTimer.Start();
        }

        if (gesture == null) return;

        var processName = WindowDetector.GetForegroundProcessName();
        var profile     = ProfileManager.GetActiveProfile(_settings, processName);
        var action      = ProfileManager.FindAction(profile, gesture);
        if (action != null) ActionExecutor.Execute(action.KeyCombo);
    }

    private static string ToArrow(string gesture) =>
        gesture.Replace("L", "←").Replace("R", "→")
               .Replace("U", "↑").Replace("D", "↓");

    // ── 트레이 아이콘 (런타임 생성) ──────────────────────────────────────────
    private static Icon CreateTrayIcon()
    {
        var bmp = new Bitmap(32, 32);
        using var g = Graphics.FromImage(bmp);
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        g.Clear(Color.FromArgb(20, 20, 36));

        // 마우스 본체 (둥근 직사각형)
        using var mouseBrush = new SolidBrush(Color.FromArgb(170, 170, 205));
        var path = new System.Drawing.Drawing2D.GraphicsPath();
        path.AddArc(9, 1, 14, 10, 180, 180);
        path.AddLine(23, 6, 23, 24);
        path.AddArc(9, 20, 14, 10, 0, 180);
        path.CloseFigure();
        g.FillPath(mouseBrush, path);

        // 버튼 분리선
        using var sep = new Pen(Color.FromArgb(20, 20, 36), 1.5f);
        g.DrawLine(sep, 16, 2, 16, 11);

        // 오른쪽 버튼 하이라이트 (파란색)
        using var highlightBrush = new SolidBrush(Color.FromArgb(130, 80, 160, 255));
        g.FillRectangle(highlightBrush, 17, 2, 5, 8);

        // 제스처 화살표 (초록색 →)
        using var arrowBrush = new SolidBrush(Color.FromArgb(80, 220, 140));
        using var arrowFont  = new Font("Segoe UI", 8f, FontStyle.Bold);
        g.DrawString("→", arrowFont, arrowBrush, 5f, 17f);

        return Icon.FromHandle(bmp.GetHicon());
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _hook.Dispose();
            _overlay.Dispose();
            _trayIcon.Dispose();
            _menu.Dispose();
        }
        base.Dispose(disposing);
    }
}
