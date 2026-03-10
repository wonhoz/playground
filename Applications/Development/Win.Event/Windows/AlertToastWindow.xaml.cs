using System.Windows.Threading;

namespace WinEvent.Windows;

public partial class AlertToastWindow : Window
{
    private readonly DispatcherTimer _timer;

    public AlertToastWindow(string ruleName, EventItem item)
    {
        InitializeComponent();

        TxtRule.Text = $"🔔 알림 규칙 일치: {ruleName}";
        TxtInfo.Text = $"[{item.LevelTag}] EventID {item.EventId}  {item.TimeDisplay}  {item.ProviderName}";
        TxtMsg.Text  = item.MessageShort;

        // 5초 후 자동 닫기
        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
        _timer.Tick += (_, _) => Close();
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        // 화면 우측 하단 배치
        var area  = SystemParameters.WorkArea;
        Left = area.Right - Width - 12;
        Top  = area.Bottom - Height - 12;
        _timer.Start();
    }

    private void BtnClose_Click(object sender, RoutedEventArgs e)
    {
        _timer.Stop();
        Close();
    }
}
