using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace ScreenRecorder;

public partial class HudOverlay : Window
{
    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(nint hwnd, int attr, ref int value, int size);

    // WS_EX_NOACTIVATE + WS_EX_TOOLWINDOW — 클릭해도 포커스 빼앗지 않음
    [DllImport("user32.dll")]
    private static extern int GetWindowLong(nint hwnd, int nIndex);
    [DllImport("user32.dll")]
    private static extern int SetWindowLong(nint hwnd, int nIndex, int dwNewLong);

    private const int GWL_EXSTYLE    = -20;
    private const int WS_EX_NOACTIVATE = 0x08000000;
    private const int WS_EX_TOOLWINDOW = 0x00000080;

    public event Action? PauseRequested;
    public event Action? StopRequested;

    private static SolidColorBrush Brush(string hex) =>
        new((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(hex)!);

    public HudOverlay()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (PresentationSource.FromVisual(this) is HwndSource src)
        {
            int v = 1;
            DwmSetWindowAttribute(src.Handle, 20, ref v, sizeof(int));

            // 포커스 비활성화
            var style = GetWindowLong(src.Handle, GWL_EXSTYLE);
            SetWindowLong(src.Handle, GWL_EXSTYLE, style | WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW);
        }

        // 우하단 고정 (작업 표시줄 위 24px 여유)
        var screen = System.Windows.Forms.Screen.PrimaryScreen!.WorkingArea;
        Left = screen.Right - ActualWidth  - 16;
        Top  = screen.Bottom - ActualHeight - 16;
    }

    public void UpdateState(bool isRecording, string timeText)
    {
        HudTime.Text = timeText;
        if (isRecording)
        {
            HudDot.Fill        = Brush("#E74C3C");
            HudPauseBtn.Content = "⏸";
        }
        else
        {
            HudDot.Fill        = Brush("#F39C12");
            HudPauseBtn.Content = "▶";
        }
    }

    private void HudPause_Click(object sender, RoutedEventArgs e) => PauseRequested?.Invoke();
    private void HudStop_Click(object sender, RoutedEventArgs e)  => StopRequested?.Invoke();

    protected override void OnMouseLeftButtonDown(System.Windows.Input.MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        DragMove();
    }
}
