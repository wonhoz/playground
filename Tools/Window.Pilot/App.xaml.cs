using System.Windows;
using System.Windows.Interop;
using WindowPilot.Services;

namespace WindowPilot;

public partial class App : Application
{
    private System.Windows.Forms.NotifyIcon? _tray;
    private WindowManager?      _wm;
    private GlobalHotkeyService? _hotkeys;
    private MouseHookService?    _mouseHook;
    private ToastOverlay?        _toast;
    private IntPtr               _hwnd;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var resDir = Path.Combine(AppContext.BaseDirectory, "Resources");
        IconGenerator.Generate(resDir);

        _wm         = new WindowManager();
        _toast      = new ToastOverlay();
        _mouseHook  = new MouseHookService();

        _mouseHook.WheelWithCtrlShift += delta =>
            Dispatcher.Invoke(() => ShowToast(_wm.AdjustOpacity(delta)));
        _mouseHook.Install();

        // HWND 확보용 숨김 창
        var hidden = new System.Windows.Window
        {
            Width = 0, Height = 0,
            WindowStyle = WindowStyle.None,
            ShowInTaskbar = false,
            Visibility = Visibility.Hidden,
        };
        hidden.Show();
        _hwnd = new WindowInteropHelper(hidden).EnsureHandle();

        var src = HwndSource.FromHwnd(_hwnd);
        src?.AddHook(WndProc);

        _hotkeys = new GlobalHotkeyService(_hwnd);

        // Ctrl+Shift+T → Always-on-Top 토글
        _hotkeys.Register(
            GlobalHotkeyService.MOD_CONTROL | GlobalHotkeyService.MOD_SHIFT,
            0x54, // T
            () => { var (_, msg) = _wm.ToggleAlwaysOnTop(); ShowToast(msg); });

        InitTray(resDir);
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == GlobalHotkeyService.WM_HOTKEY)
        {
            _hotkeys?.HandleMessage(wParam);
            handled = true;
        }
        return IntPtr.Zero;
    }

    private void ShowToast(string msg)
    {
        if (string.IsNullOrEmpty(msg)) return;
        Dispatcher.Invoke(() => _toast?.Show(msg));
    }

    // ─────────────────────────────────────────────
    // 트레이
    // ─────────────────────────────────────────────

    private void InitTray(string resDir)
    {
        _tray = new System.Windows.Forms.NotifyIcon
        {
            Text    = "Window Pilot",
            Visible = true,
        };

        var iconPath = Path.Combine(resDir, IconGenerator.IconFileName);
        if (File.Exists(iconPath))
            _tray.Icon = new System.Drawing.Icon(iconPath);

        var menu = new System.Windows.Forms.ContextMenuStrip();
        menu.Renderer = new DarkMenuRenderer();

        // Always-on-Top
        var topItem = menu.Items.Add("📌 항상 위 토글  (Ctrl+Shift+T)");
        topItem.Click += (_, _) => { var (_, msg) = _wm!.ToggleAlwaysOnTop(); ShowToast(msg); };

        // 투명도 서브메뉴
        var opacMenu = new System.Windows.Forms.ToolStripMenuItem("🔆 투명도");
        foreach (int pct in new[] { 100, 90, 80, 70, 60, 50, 40, 30, 20, 10 })
        {
            int p = pct;
            var item = new System.Windows.Forms.ToolStripMenuItem($"{p}%");
            item.Click += (_, _) => ShowToast(_wm!.SetOpacityPct(p));
            opacMenu.DropDownItems.Add(item);
        }
        menu.Items.Add(opacMenu);

        // 크기 프리셋 서브메뉴
        var sizeMenu = new System.Windows.Forms.ToolStripMenuItem("⬛ 크기 프리셋");
        var presets = new[]
        {
            ("1/4 화면",   WindowManager.SizePreset.Quarter),
            ("1/3 화면",   WindowManager.SizePreset.Third),
            ("1/2 화면",   WindowManager.SizePreset.Half),
            ("미니 200×150", WindowManager.SizePreset.Mini),
            ("↩ 원래 크기",  WindowManager.SizePreset.Restore),
        };
        foreach (var (label, preset) in presets)
        {
            var p = preset;
            var item = new System.Windows.Forms.ToolStripMenuItem(label);
            item.Click += (_, _) => ShowToast(_wm!.ApplyPreset(p));
            sizeMenu.DropDownItems.Add(item);
        }
        menu.Items.Add(sizeMenu);

        menu.Items.Add(new System.Windows.Forms.ToolStripSeparator());

        var exitItem = menu.Items.Add("종료");
        exitItem.Click += (_, _) => ExitApp();

        _tray.ContextMenuStrip = menu;
        _tray.DoubleClick += (_, _) =>
        {
            var (_, msg) = _wm!.ToggleAlwaysOnTop();
            ShowToast(msg);
        };

        _tray.ShowBalloonTip(2000, "Window Pilot",
            "Ctrl+Shift+T: 항상 위 토글\nCtrl+Shift+휠: 투명도 조절",
            System.Windows.Forms.ToolTipIcon.Info);
    }

    private void ExitApp()
    {
        _mouseHook?.Dispose();
        _hotkeys?.Dispose();
        _tray?.Dispose();
        Shutdown();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _mouseHook?.Dispose();
        _hotkeys?.Dispose();
        _tray?.Dispose();
        base.OnExit(e);
    }
}
