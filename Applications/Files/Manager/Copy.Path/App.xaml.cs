using Microsoft.Win32;
using System.Drawing;
using System.Windows.Forms;

namespace CopyPath;

public partial class App : System.Windows.Application
{
    [DllImport("user32.dll")] static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);
    [DllImport("user32.dll")] static extern bool UnregisterHotKey(IntPtr hWnd, int id);
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    static extern int SetCurrentProcessExplicitAppUserModelID(string id);

    private const int HotkeyId  = 9003;
    private const uint MOD_WIN   = 0x0008;
    private const uint MOD_SHIFT = 0x0004;
    private const uint VK_X      = 0x58;
    private const int  WM_HOTKEY = 0x0312;

    private NotifyIcon?  _tray;
    private PopupWindow? _popup;
    private UsageService _usage = null!;
    private HwndSource?  _hwndSource;
    private System.Threading.Mutex? _mutex;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        _mutex = new System.Threading.Mutex(true, "CopyPath_SingleInstance", out bool isNew);
        if (!isNew) { Shutdown(); return; }

        RegisterAumid("Playground.CopyPath");
        _usage = new UsageService();
        BuildTray();
        RegisterGlobalHotkey();
    }

    private static void RegisterAumid(string aumid)
    {
        SetCurrentProcessExplicitAppUserModelID(aumid);
        using var key = Registry.CurrentUser.CreateSubKey($@"SOFTWARE\Classes\AppUserModelId\{aumid}");
        key.SetValue("DisplayName", "Copy.Path");
        key.SetValue("IconUri", Path.Combine(AppContext.BaseDirectory, "Resources", "app.ico"));
    }

    private void BuildTray()
    {
        var icoPath = Path.Combine(AppContext.BaseDirectory, "Resources", "app.ico");
        var icon = File.Exists(icoPath) ? new Icon(icoPath) : SystemIcons.Application;

        _tray = new NotifyIcon
        {
            Icon    = icon,
            Text    = "Copy.Path — Win+Shift+X",
            Visible = true
        };

        var menu = new ContextMenuStrip
        {
            ShowImageMargin = false,
            Font            = new Font("Segoe UI", 9.5f),
            BackColor       = DrawingColor.FromArgb(255, 26, 30, 42),
            ForeColor       = DrawingColor.FromArgb(255, 224, 232, 255),
            Renderer        = new DarkMenuRenderer()
        };
        menu.Items.Add("📂  Copy.Path 열기", null, (_, _) => ShowPopup());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("✕  종료", null, (_, _) => Shutdown());
        _tray.ContextMenuStrip = menu;
        _tray.MouseClick += (_, ev) => { if (ev.Button == MouseButtons.Left) ShowPopup(); };
        _tray.ShowBalloonTip(2000, "Copy.Path", "Win+Shift+X 로 파일 경로 복사 팝업", ToolTipIcon.Info);
    }

    private void RegisterGlobalHotkey()
    {
        var helper = new WindowInteropHelper(new System.Windows.Window
            { Width=0, Height=0, WindowStyle=System.Windows.WindowStyle.None, ShowInTaskbar=false, Opacity=0 });
        helper.EnsureHandle();
        _hwndSource = HwndSource.FromHwnd(helper.Handle);
        _hwndSource?.AddHook(WndProc);
        RegisterHotKey(helper.Handle, HotkeyId, MOD_WIN | MOD_SHIFT, VK_X);
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
            _popup = new PopupWindow(_usage);
            _popup.Closed += (_, _) => _popup = null;
        }
        _popup.ShowAndActivate();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        if (_hwndSource != null) UnregisterHotKey(_hwndSource.Handle, HotkeyId);
        _tray?.Dispose();
        _usage?.Dispose();
        _mutex?.Dispose();
        base.OnExit(e);
    }
}

internal class DarkMenuRenderer : ToolStripRenderer
{
    protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e)
        => e.Graphics.Clear(DrawingColor.FromArgb(255, 26, 30, 42));
    protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
    {
        if (!e.Item.Selected) return;
        using var b = new DrawingBrush(DrawingColor.FromArgb(255, 30, 40, 64));
        e.Graphics.FillRectangle(b, new System.Drawing.Rectangle(2, 0, e.Item.Width-4, e.Item.Height));
    }
    protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
    { e.TextColor = DrawingColor.FromArgb(255, 224, 232, 255); base.OnRenderItemText(e); }
    protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
    {
        int y = e.Item.Height / 2;
        using var p = new DrawingPen(DrawingColor.FromArgb(255, 42, 48, 80));
        e.Graphics.DrawLine(p, 8, y, e.Item.Width-8, y);
    }
}
