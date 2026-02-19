using System.Windows;
using System.Windows.Interop;
using QuickLauncher.Models;
using QuickLauncher.Services;

namespace QuickLauncher;

public partial class App : Application
{
    private LauncherWindow?      _launcher;
    private GlobalHotkeyService? _hotkeys;
    private LauncherSettings     _settings = new();
    private readonly AppSearchProvider  _appProvider = new();
    private readonly BuiltinProvider    _builtinProvider = new();
    private SearchEngine?        _engine;

    // WinForms tray (fully-qualified to avoid WPF type conflicts)
    private System.Windows.Forms.NotifyIcon? _tray;

    public SearchEngine Engine => _engine!;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _settings = SettingsService.Load();
        _builtinProvider.Reload(_settings.CustomItems);
        _engine = new SearchEngine(_appProvider, _builtinProvider);

        // 앱 목록을 백그라운드에서 인덱싱 (오류는 무시)
        _ = Task.Run(_appProvider.BuildIndex);

        // LauncherWindow 생성 (표시하지 않음)
        _launcher = new LauncherWindow();

        // HWND 생성 (창 표시 없이)
        var helper = new WindowInteropHelper(_launcher);
        helper.EnsureHandle();

        // 전역 단축키 설정
        _hotkeys = new GlobalHotkeyService(helper.Handle);
        var src  = HwndSource.FromHwnd(helper.Handle);
        src?.AddHook(WndProc);
        RegisterHotkey();

        InitTray();
    }

    private void RegisterHotkey()
    {
        _hotkeys?.UnregisterAll();
        _hotkeys?.Register(_settings.HotkeyMods, _settings.HotkeyVk, ToggleLauncher);
    }

    private void ToggleLauncher()
    {
        Dispatcher.Invoke(() =>
        {
            if (_launcher is null) return;
            if (_launcher.IsVisible) _launcher.HideLauncher();
            else                     _launcher.ShowLauncher();
        });
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == GlobalHotkeyService.WM_HOTKEY && _hotkeys is not null)
            handled = _hotkeys.HandleMessage(wParam);
        return IntPtr.Zero;
    }

    private void InitTray()
    {
        _tray = new System.Windows.Forms.NotifyIcon
        {
            Text    = "Quick.Launcher",
            Visible = true,
            Icon    = CreateTrayIcon(),
        };

        var menu = new System.Windows.Forms.ContextMenuStrip();
        menu.Items.Add("Quick.Launcher 열기",  null, (_, _) => ToggleLauncher());
        menu.Items.Add("설정",                  null, (_, _) => OpenSettings());
        menu.Items.Add(new System.Windows.Forms.ToolStripSeparator());
        menu.Items.Add("종료",                  null, (_, _) => ExitApp());

        _tray.ContextMenuStrip = menu;
        _tray.DoubleClick     += (_, _) => ToggleLauncher();
    }

    private void OpenSettings()
    {
        Dispatcher.Invoke(() =>
        {
            var win = new SettingsWindow(_settings) { Owner = _launcher };
            if (win.ShowDialog() != true) return;

            _settings = SettingsService.Load();
            _builtinProvider.Reload(_settings.CustomItems);
            RegisterHotkey();
        });
    }

    private void ExitApp()
    {
        _tray?.Dispose();
        _hotkeys?.Dispose();
        Shutdown();
    }

    private static System.Drawing.Icon CreateTrayIcon()
    {
        using var bmp = new System.Drawing.Bitmap(32, 32);
        using var g   = System.Drawing.Graphics.FromImage(bmp);
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        g.Clear(System.Drawing.Color.FromArgb(255, 255, 193, 7));  // amber
        using var font = new System.Drawing.Font("Segoe UI", 17, System.Drawing.FontStyle.Bold);
        using var sf   = new System.Drawing.StringFormat
        {
            Alignment     = System.Drawing.StringAlignment.Center,
            LineAlignment = System.Drawing.StringAlignment.Center
        };
        g.DrawString("Q", font, System.Drawing.Brushes.Black,
            new System.Drawing.RectangleF(0, 0, 32, 32), sf);
        return System.Drawing.Icon.FromHandle(bmp.GetHicon());
    }
}
