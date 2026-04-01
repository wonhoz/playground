using System.Drawing;
using System.Windows.Forms;

namespace CaseForge;

public partial class App : System.Windows.Application
{
    [DllImport("user32.dll")] static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);
    [DllImport("user32.dll")] static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    private const int HotkeyId  = 9002;
    private const int WM_HOTKEY = 0x0312;

    private NotifyIcon?          _tray;
    private PopupWindow?         _popup;
    private HwndSource?          _hwndSource;
    private System.Threading.Mutex? _mutex;
    private System.Windows.Window?  _hotkeyWindow; // GC 방지용 레퍼런스
    private SettingsWindow?      _settingsWindow;
    private HelpWindow?          _helpWindow;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        _mutex = new System.Threading.Mutex(true, "CaseForge_SingleInstance", out bool isNew);
        if (!isNew) { Shutdown(); return; }

        var settings = SettingsService.Load();
        BuildTray(settings);
        RegisterGlobalHotkey(settings);
    }

    // pack 리소스로 내장된 app.ico를 System.Drawing.Icon으로 변환
    private static Icon LoadTrayIcon()
    {
        try
        {
            var sri = System.Windows.Application.GetResourceStream(
                new Uri("pack://application:,,,/Resources/app.ico"));
            if (sri != null)
            {
                using var ms = new System.IO.MemoryStream();
                sri.Stream.CopyTo(ms);
                ms.Position = 0;
                return new Icon(ms);
            }
        }
        catch { }
        return SystemIcons.Application;
    }

    private void BuildTray(AppSettings settings)
    {
        _tray = new NotifyIcon
        {
            Icon    = LoadTrayIcon(),
            Text    = $"Case.Forge — {SettingsService.FormatHotkey(settings.HotkeyModifiers, settings.HotkeyVK)}",
            Visible = true
        };

        var menu = new ContextMenuStrip
        {
            ShowImageMargin = false,
            Font            = new Font("Segoe UI", 9.5f),
            BackColor       = DrawingColor.FromArgb(255, 26, 42, 30),
            ForeColor       = DrawingColor.FromArgb(255, 224, 240, 228),
            Renderer        = new DarkMenuRenderer()
        };
        menu.Items.Add("Cc  Case.Forge 열기", null, (_, _) => ShowPopup());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("⚙  설정",   null, (_, _) => ShowSettings());
        menu.Items.Add("?  도움말", null, (_, _) => ShowHelp());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("✕  종료",   null, (_, _) => Shutdown());
        _tray.ContextMenuStrip = menu;
        _tray.MouseClick += (_, ev) => { if (ev.Button == MouseButtons.Left) ShowPopup(); };
        _tray.ShowBalloonTip(2000, "Case.Forge",
            $"{SettingsService.FormatHotkey(settings.HotkeyModifiers, settings.HotkeyVK)} 로 케이스 변환 팝업",
            ToolTipIcon.Info);
    }

    private void RegisterGlobalHotkey(AppSettings settings)
    {
        _hotkeyWindow = new System.Windows.Window
            { Width=0, Height=0, WindowStyle=System.Windows.WindowStyle.None, ShowInTaskbar=false, Opacity=0 };
        var helper = new WindowInteropHelper(_hotkeyWindow);
        helper.EnsureHandle();
        _hwndSource = HwndSource.FromHwnd(helper.Handle);
        _hwndSource?.AddHook(WndProc);

        bool ok = RegisterHotKey(helper.Handle, HotkeyId, settings.HotkeyModifiers, settings.HotkeyVK);
        if (!ok)
            _tray?.ShowBalloonTip(3000, "Case.Forge",
                $"단축키 등록 실패 — 설정에서 다른 키를 지정하세요", ToolTipIcon.Warning);
    }

    // 설정 저장 후 단축키 재등록 (SettingsWindow에서 호출)
    internal void ReapplyHotkey(AppSettings settings)
    {
        if (_hwndSource == null) return;
        UnregisterHotKey(_hwndSource.Handle, HotkeyId);
        bool ok = RegisterHotKey(_hwndSource.Handle, HotkeyId, settings.HotkeyModifiers, settings.HotkeyVK);
        if (_tray != null)
        {
            _tray.Text = $"Case.Forge — {SettingsService.FormatHotkey(settings.HotkeyModifiers, settings.HotkeyVK)}";
            if (!ok)
                _tray.ShowBalloonTip(3000, "Case.Forge", "단축키 등록 실패 — 다른 앱과 충돌합니다", ToolTipIcon.Warning);
        }
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_HOTKEY && wParam.ToInt32() == HotkeyId)
        {
            ShowPopup(); handled = true;
        }
        return IntPtr.Zero;
    }

    internal void ShowPopup()
    {
        if (_popup == null || !_popup.IsLoaded)
        {
            _popup = new PopupWindow();
            _popup.Closed += (_, _) => _popup = null;
        }
        _popup.ShowAndActivate();
    }

    private void ShowSettings()
    {
        if (_settingsWindow?.IsLoaded == true) { _settingsWindow.Activate(); return; }
        _settingsWindow = new SettingsWindow();
        _settingsWindow.Show();
    }

    private void ShowHelp()
    {
        if (_helpWindow?.IsLoaded == true) { _helpWindow.Activate(); return; }
        _helpWindow = new HelpWindow();
        _helpWindow.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        if (_hwndSource != null) UnregisterHotKey(_hwndSource.Handle, HotkeyId);
        _tray?.Dispose();
        _mutex?.Dispose();
        base.OnExit(e);
    }
}

internal class DarkMenuRenderer : ToolStripRenderer
{
    protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e)
        => e.Graphics.Clear(DrawingColor.FromArgb(255, 26, 42, 30));
    protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
    {
        if (!e.Item.Selected) return;
        using var b = new DrawingBrush(DrawingColor.FromArgb(255, 30, 52, 38));
        e.Graphics.FillRectangle(b, new System.Drawing.Rectangle(2, 0, e.Item.Width-4, e.Item.Height));
    }
    protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
    { e.TextColor = DrawingColor.FromArgb(255, 224, 240, 228); base.OnRenderItemText(e); }
    protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
    {
        int y = e.Item.Height / 2;
        using var p = new DrawingPen(DrawingColor.FromArgb(255, 42, 74, 50));
        e.Graphics.DrawLine(p, 8, y, e.Item.Width-8, y);
    }
}
