using System.Drawing;
using System.Windows.Forms;

namespace CopyPath;

public partial class App : System.Windows.Application
{
    [DllImport("user32.dll")] static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);
    [DllImport("user32.dll")] static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    private const int HotkeyId  = 9003;
    private const uint MOD_WIN   = 0x0008;
    private const uint MOD_SHIFT = 0x0004;
    private const uint VK_X      = 0x58;
    private const int  WM_HOTKEY = 0x0312;

    private NotifyIcon?  _tray;
    private PopupWindow? _popup;
    private UsageService _usage = null!;
    private HwndSource?  _hwndSource;
    private System.Windows.Window? _hotkeyWindow;  // GC 방지 — 수집되면 HWND 파괴됨
    private System.Threading.Mutex? _mutex;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        _mutex = new System.Threading.Mutex(true, "CopyPath_SingleInstance", out bool isNew);
        if (!isNew) { Shutdown(); return; }

        _usage = new UsageService();
        BuildTray();
        RegisterGlobalHotkey();
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

    private void BuildTray()
    {
        _tray = new NotifyIcon
        {
            Icon    = LoadTrayIcon(),
            Text    = "Copy.Path — Win+Shift+X",
            Visible = true
        };

        var darkBg  = DrawingColor.FromArgb(255, 26, 30, 42);
        var darkFg  = DrawingColor.FromArgb(255, 224, 232, 255);
        var darkRnd = new DarkMenuRenderer();

        // 딜레이 서브메뉴
        var delayMenu = new ContextMenuStrip
        {
            ShowImageMargin = false,
            Font            = new Font("Segoe UI", 9.5f),
            BackColor       = darkBg,
            ForeColor       = darkFg,
            Renderer        = darkRnd
        };
        foreach (var (label, ms) in new[] { ("즉시 닫기", 0), ("200ms", 200), ("400ms (기본)", 400), ("600ms", 600) })
        {
            int delay = ms;
            delayMenu.Items.Add(label, null, async (_, _) =>
            {
                await _usage.SetHideDelayAsync(delay);
            });
        }

        var menu = new ContextMenuStrip
        {
            ShowImageMargin = false,
            Font            = new Font("Segoe UI", 9.5f),
            BackColor       = darkBg,
            ForeColor       = darkFg,
            Renderer        = darkRnd
        };
        menu.Items.Add("📂  Copy.Path 열기", null, (_, _) => ShowPopup());
        menu.Items.Add("❓  사용법 / 단축키", null, (_, _) => ShowHelp());
        menu.Items.Add(new ToolStripSeparator());

        var delayItem = new ToolStripMenuItem("⏱  자동 닫힘 딜레이") { BackColor = darkBg, ForeColor = darkFg };
        delayItem.DropDown = delayMenu;
        menu.Items.Add(delayItem);

        menu.Items.Add("🔄  숨긴 포맷 복원", null, async (_, _) =>
        {
            await _usage.RestoreAllFormatsAsync();
        });
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("✕  종료", null, (_, _) => Shutdown());
        _tray.ContextMenuStrip = menu;
        _tray.MouseClick += (_, ev) => { if (ev.Button == MouseButtons.Left) ShowPopup(); };
        _tray.ShowBalloonTip(2000, "Copy.Path", "Win+Shift+X 로 파일 경로 복사 팝업", ToolTipIcon.Info);
    }

    private void RegisterGlobalHotkey()
    {
        _hotkeyWindow = new System.Windows.Window
            { Width=0, Height=0, WindowStyle=System.Windows.WindowStyle.None, ShowInTaskbar=false, Opacity=0 };
        var helper = new WindowInteropHelper(_hotkeyWindow);
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
        _ = _popup.ShowAndActivateAsync();
    }

    private void ShowHelp()
    {
        System.Windows.MessageBox.Show(
            "Copy.Path — 파일 경로 복사 도구\n\n" +
            "📌 단축키\n" +
            "  Win + Shift + X   팝업 열기\n" +
            "  Esc               팝업 닫기\n\n" +
            "📋 사용 방법\n" +
            "  1. 파일 탐색기에서 파일/폴더를 선택한 뒤\n" +
            "     Win+Shift+X 를 누르면 경로가 자동으로 로드됩니다.\n" +
            "  2. 원하는 포맷 행을 클릭하면 클립보드에 복사됩니다.\n" +
            "  3. 팝업 창에 파일/폴더를 직접 드래그해도 됩니다.\n" +
            "     (복수 파일 드래그 시 '복수 복사' 버튼이 표시됩니다)\n" +
            "  4. 하단 '최근 경로' 항목을 클릭하면 이전 경로를 재사용합니다.\n\n" +
            "⭐ 최근 경로 관리\n" +
            "  ☆ / ★ 아이콘 클릭   해당 경로 즐겨찾기 고정 / 해제\n" +
            "  ✕ 버튼 (마우스 올리면 표시)   해당 경로 삭제\n\n" +
            "🔧 포맷 관리\n" +
            "  포맷 행 우클릭   해당 포맷 숨기기\n" +
            "  트레이 우클릭 → 숨긴 포맷 복원   모든 포맷 다시 표시\n\n" +
            "⏱ 자동 닫힘 딜레이\n" +
            "  트레이 우클릭 → 자동 닫힘 딜레이   복사 후 팝업 닫힘 속도 설정\n\n" +
            "💡 지원 포맷 (9종)\n" +
            "  전체 경로 (백슬래시 / 슬래시) · C# 리터럴 · 파일명 ·\n" +
            "  확장자 없는 파일명 · 상위 폴더 · file:/// URL ·\n" +
            "  Unix 스타일 · UNC 경로",
            "Copy.Path 사용법",
            System.Windows.MessageBoxButton.OK,
            System.Windows.MessageBoxImage.Information);
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
