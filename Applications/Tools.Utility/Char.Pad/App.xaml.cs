using System.Drawing;
using System.Windows.Forms;

namespace CharPad;

public partial class App : System.Windows.Application
{
    // ── Win32 P/Invoke ──────────────────────────────────────────────────
    [DllImport("user32.dll")] static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);
    [DllImport("user32.dll")] static extern bool UnregisterHotKey(IntPtr hWnd, int id);
    [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
    [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int val, int size);

    private const int HotkeyId  = 9001;
    private const uint MOD_WIN   = 0x0008;
    private const uint MOD_SHIFT = 0x0004;
    private const uint VK_OEM_1  = 0xBA; // ;
    private const int WM_HOTKEY  = 0x0312;

    private NotifyIcon?    _tray;
    private PopupWindow?   _popup;
    private StorageService _storage = null!;
    private HwndSource?    _hwndSource;
    private System.Windows.Window? _hotkeyWindow; // GC 방지 — HWND 수명 보장
    private IntPtr         _prevHwnd;
    private System.Threading.Mutex? _mutex;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // 단일 인스턴스
        _mutex = new System.Threading.Mutex(true, "CharPad_SingleInstance", out bool isNew);
        if (!isNew)
        {
            Shutdown();
            return;
        }

        _storage = new StorageService();
        BuildTray();
        RegisterGlobalHotkey();
    }

    // ── 트레이 아이콘 ───────────────────────────────────────────────────
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

    private void BuildTray()
    {
        _tray = new NotifyIcon
        {
            Icon    = LoadTrayIcon(),
            Text    = "Char.Pad — Win+Shift+;",
            Visible = true
        };

        var menu = new ContextMenuStrip
        {
            ShowImageMargin = false,
            AutoSize        = true,
            Font            = new Font("Segoe UI", 9.5f),
            BackColor       = ColorTranslator.FromHtml("#1A1A2E"),
            ForeColor       = ColorTranslator.FromHtml("#E0E0E0"),
            Renderer        = new DarkMenuRenderer()
        };
        menu.Items.Add("⌨  Char.Pad 열기",  null, (_, _) => ShowPopup());
        menu.Items.Add("?  사용 방법",       null, (_, _) => ShowHelp());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("🗑  최근 사용 초기화", null, (_, _) => ClearRecents());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("✕  종료",            null, (_, _) => Shutdown());

        _tray.ContextMenuStrip = menu;
        _tray.MouseClick += (_, ev) =>
        {
            if (ev.Button == MouseButtons.Left) ShowPopup();
        };

        _tray.ShowBalloonTip(2000, "Char.Pad", "Win+Shift+; 로 특수문자 입력", ToolTipIcon.Info);
    }

    // ── 전역 단축키 등록 ────────────────────────────────────────────────
    private void RegisterGlobalHotkey()
    {
        // 메시지 훅을 위한 숨김 창 사용 (_hotkeyWindow 필드 저장으로 GC 방지)
        _hotkeyWindow = new System.Windows.Window { Width = 0, Height = 0, WindowStyle = System.Windows.WindowStyle.None, ShowInTaskbar = false, Opacity = 0 };
        var helper = new WindowInteropHelper(_hotkeyWindow);
        helper.EnsureHandle();
        _hwndSource = HwndSource.FromHwnd(helper.Handle);
        _hwndSource?.AddHook(WndProc);
        RegisterHotKey(helper.Handle, HotkeyId, MOD_WIN | MOD_SHIFT, VK_OEM_1);
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_HOTKEY && wParam.ToInt32() == HotkeyId)
        {
            ShowPopup();
            handled = true;
        }
        return IntPtr.Zero;
    }

    // ── 팝업 열기 ───────────────────────────────────────────────────────
    internal void ShowPopup()
    {
        _prevHwnd = GetForegroundWindow();

        if (_popup == null || !_popup.IsLoaded)
        {
            _popup = new PopupWindow(_storage);
            _popup.Closed += (_, _) => _popup = null;
        }

        _popup.ShowAt(_prevHwnd);
    }

    internal Task PasteToWindowAsync(IntPtr targetHwnd) => InputHelper.PasteToWindowAsync(targetHwnd);

    private void ClearRecents()
    {
        _storage.ClearRecents();
        _popup?.RefreshIfRecentTab();
    }

    private void ShowHelp()
    {
        _prevHwnd = GetForegroundWindow();
        if (_popup == null || !_popup.IsLoaded)
        {
            _popup = new PopupWindow(_storage);
            _popup.Closed += (_, _) => _popup = null;
        }
        _popup.ShowAt(_prevHwnd);
        _popup.ShowHelpOverlay();
    }

    // ── 종료 ─────────────────────────────────────────────────────────────
    protected override void OnExit(ExitEventArgs e)
    {
        if (_hwndSource != null)
            UnregisterHotKey(_hwndSource.Handle, HotkeyId);
        _tray?.Dispose();
        _storage?.Dispose();
        _mutex?.Dispose();
        base.OnExit(e);
    }
}

// ── 다크 메뉴 렌더러 ────────────────────────────────────────────────────
internal class DarkMenuRenderer : ToolStripRenderer
{
    private static readonly System.Drawing.Color BgColor    = ColorTranslator.FromHtml("#1A1A2E");
    private static readonly System.Drawing.Color HoverColor = ColorTranslator.FromHtml("#1A3550");
    private static readonly System.Drawing.Color TextColor  = ColorTranslator.FromHtml("#E0E0E0");
    private static readonly System.Drawing.Color SepColor   = ColorTranslator.FromHtml("#1A3A55");

    protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e)
        => e.Graphics.Clear(BgColor);

    protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
    {
        if (e.Item.Selected)
        {
            using var brush = new SolidBrush(HoverColor);
            var r = new Rectangle(2, 0, e.Item.Width - 4, e.Item.Height);
            e.Graphics.FillRectangle(brush, r);
        }
    }

    protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
    {
        e.TextColor = TextColor;
        base.OnRenderItemText(e);
    }

    protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
    {
        int y = e.Item.Height / 2;
        using var pen = new DrawingPen(SepColor);
        e.Graphics.DrawLine(pen, 8, y, e.Item.Width - 8, y);
    }
}
