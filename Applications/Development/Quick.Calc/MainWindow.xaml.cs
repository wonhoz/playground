using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using System.Windows.Interop;
using QuickCalc.ViewModels;

namespace QuickCalc;

public partial class MainWindow : Window
{
    private const int WM_HOTKEY = 0x0312;
    private const int HOTKEY_ID = 9001;
    private const uint MOD_WIN = 0x0008;
    private const uint VK_K = 0x4B;

    [DllImport("user32.dll")]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);
    [DllImport("user32.dll")]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    private NotifyIcon? _tray;
    private bool _realClose;   // true일 때만 실제 종료

    public MainWindow()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Closed += OnClosed;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        RegisterHotKey(hwnd, HOTKEY_ID, MOD_WIN, VK_K);
        HwndSource.FromHwnd(hwnd)?.AddHook(WndProc);
        InitTray();
    }

    // ── 트레이 아이콘 ──────────────────────────────────────────────

    private void InitTray()
    {
        var icoPath = System.IO.Path.Combine(
            AppContext.BaseDirectory, "Resources", "app.ico");
        Icon trayIcon = File.Exists(icoPath)
            ? new Icon(icoPath)
            : SystemIcons.Application;

        var menu = new ContextMenuStrip
        {
            Renderer       = new DarkMenuRenderer(),
            ShowImageMargin = false,
            AutoSize        = true,
            Font            = new Font("Segoe UI", 9.5f)
        };
        var itemOpen = new ToolStripMenuItem("Quick.Calc 열기");
        itemOpen.Click += (_, _) => ShowAndActivate();

        var itemExit = new ToolStripMenuItem("종료");
        itemExit.Click += (_, _) => ExitApp();

        menu.Items.Add(itemOpen);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(itemExit);

        _tray = new NotifyIcon
        {
            Icon             = trayIcon,
            Text             = "Quick.Calc  |  Win+K로 열기",
            ContextMenuStrip = menu,
            Visible          = true
        };
        _tray.DoubleClick += (_, _) => ShowAndActivate();
    }

    private void ExitApp()
    {
        _realClose = true;
        Close();
    }

    // ── 핫키·메시지 펌프 ──────────────────────────────────────────

    private void OnClosed(object? sender, EventArgs e)
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        UnregisterHotKey(hwnd, HOTKEY_ID);
        if (_tray is not null)
        {
            _tray.Visible = false;
            _tray.Dispose();
        }
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_HOTKEY && wParam.ToInt32() == HOTKEY_ID)
        {
            ShowAndActivate();
            handled = true;
        }
        return IntPtr.Zero;
    }

    private void ShowAndActivate()
    {
        if (WindowState == WindowState.Minimized)
            WindowState = WindowState.Normal;
        Show();
        Activate();
        Topmost = true;
        Topmost = false;
    }

    // ── 창 닫기 처리 ────────────────────────────────────────────

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        // 트레이 "종료" 클릭 또는 Shift+X → 실제 종료
        if (_realClose ||
            System.Windows.Input.Keyboard.IsKeyDown(System.Windows.Input.Key.LeftShift) ||
            System.Windows.Input.Keyboard.IsKeyDown(System.Windows.Input.Key.RightShift))
        {
            base.OnClosing(e);
            return;
        }
        // X 버튼 / 창 닫기 버튼 → 숨김 (핫키 유지)
        e.Cancel = true;
        Hide();
    }

    // ── UI 이벤트 ────────────────────────────────────────────────

    private void TitleBar_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        => DragMove();

    private void MinimizeBtn_Click(object sender, RoutedEventArgs e)
        => WindowState = WindowState.Minimized;

    private void CloseBtn_Click(object sender, RoutedEventArgs e)
        => Hide(); // 닫기 버튼: 숨김 (트레이 또는 Win+K로 다시 열기)
}

// ── 다크 트레이 메뉴 렌더러 ──────────────────────────────────────

file sealed class DarkMenuRenderer : ToolStripRenderer
{
    private static readonly System.Drawing.Color _bg     = System.Drawing.Color.FromArgb(26, 26, 40);
    private static readonly System.Drawing.Color _hover  = System.Drawing.Color.FromArgb(50, 128, 255, 100);
    private static readonly System.Drawing.Color _border = System.Drawing.Color.FromArgb(46, 46, 80);
    private static readonly System.Drawing.Color _fg     = System.Drawing.Color.FromArgb(224, 224, 240);
    private static readonly System.Drawing.Color _sep    = System.Drawing.Color.FromArgb(46, 46, 80);

    protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e)
        => e.Graphics.Clear(_bg);

    protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
    {
        var r = new Rectangle(2, 0, e.Item.Width - 4, e.Item.Height);
        using var b = new SolidBrush(e.Item.Selected
            ? System.Drawing.Color.FromArgb(42, 74, 130)
            : _bg);
        e.Graphics.FillRectangle(b, r);
    }

    protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
    {
        e.TextColor = e.Item.Enabled ? _fg : System.Drawing.Color.FromArgb(80, 80, 100);
        base.OnRenderItemText(e);
    }

    protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
    {
        int y = e.Item.Height / 2;
        using var pen = new Pen(_sep);
        e.Graphics.DrawLine(pen, 6, y, e.Item.Width - 6, y);
    }

    protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
    {
        using var pen = new Pen(_border);
        var r = e.AffectedBounds;
        e.Graphics.DrawRectangle(pen, r.X, r.Y, r.Width - 1, r.Height - 1);
    }
}
