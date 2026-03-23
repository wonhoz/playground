using System.Windows;
using System.Windows.Threading;

namespace WindowPilot;

public partial class ToastOverlay : Window
{
    private readonly DispatcherTimer _timer;

    public ToastOverlay()
    {
        InitializeComponent();
        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1.8) };
        _timer.Tick += (_, _) => { _timer.Stop(); Hide(); };
    }

    public void Show(string message)
    {
        TxtMsg.Text = message;

        // 우하단 배치
        var screen = System.Windows.Forms.Screen.PrimaryScreen!.WorkingArea;
        UpdateLayout();
        Left = screen.Right  - ActualWidth  - 20;
        Top  = screen.Bottom - ActualHeight - 20;
        if (ActualWidth == 0)   // 첫 표시 전이라 측정 안 된 경우
        {
            Left = screen.Right  - 260;
            Top  = screen.Bottom - 60;
        }

        Show();
        Activate();

        _timer.Stop();
        _timer.Start();
    }

    protected override void OnActivated(EventArgs e)
    {
        // 포커스 뺏지 않음
    }
}
