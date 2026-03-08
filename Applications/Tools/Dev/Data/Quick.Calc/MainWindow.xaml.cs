using System.Runtime.InteropServices;
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
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        UnregisterHotKey(hwnd, HOTKEY_ID);
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

    private void TitleBar_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        => DragMove();

    private void MinimizeBtn_Click(object sender, RoutedEventArgs e)
        => WindowState = WindowState.Minimized;

    private void CloseBtn_Click(object sender, RoutedEventArgs e)
        => Hide(); // 닫기 대신 숨김 (Win+K로 다시 열기 가능)

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        // X 버튼: 숨김으로 처리하여 핫키 유지
        // Shift+X 또는 애플리케이션 종료 시에만 실제 닫기
        if (System.Windows.Input.Keyboard.IsKeyDown(System.Windows.Input.Key.LeftShift) ||
            System.Windows.Input.Keyboard.IsKeyDown(System.Windows.Input.Key.RightShift))
        {
            base.OnClosing(e);
            return;
        }
        e.Cancel = true;
        Hide();
    }
}
