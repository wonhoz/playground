using System.Runtime.InteropServices;
using System.Windows.Controls;
using System.Windows.Interop;
using Sched.Cast.Models;

namespace Sched.Cast.Views;

public partial class EditTaskWindow : Window
{
    [DllImport("dwmapi.dll")]
    static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int val, int size);

    public TaskInfo? Result { get; private set; }

    public EditTaskWindow()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            var h = new WindowInteropHelper(this).Handle;
            int v = 1;
            DwmSetWindowAttribute(h, 20, ref v, sizeof(int));
        };
    }

    void Browse_Click(object s, RoutedEventArgs e)
    {
        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "실행 파일|*.exe;*.cmd;*.bat;*.ps1|모든 파일|*.*",
            Title  = "실행 파일 선택",
        };
        if (dlg.ShowDialog(this) == true)
            TxtExe.Text = dlg.FileName;
    }

    void CmbTrigger_Changed(object s, SelectionChangedEventArgs e)
    {
        if (!IsLoaded) return;
        var tag = (CmbTrigger.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "";
        PnlTime.Visibility    = tag is "Daily" or "Weekly" or "Once" ? Visibility.Visible : Visibility.Collapsed;
        PnlWeekday.Visibility = tag == "Weekly"  ? Visibility.Visible : Visibility.Collapsed;
        PnlOnce.Visibility    = tag == "Once"    ? Visibility.Visible : Visibility.Collapsed;
    }

    void Ok_Click(object s, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(TxtName.Text))
        {
            MessageBox.Show("작업 이름을 입력하세요.", "입력 오류",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        if (string.IsNullOrWhiteSpace(TxtExe.Text))
        {
            MessageBox.Show("실행 파일 경로를 입력하세요.", "입력 오류",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var tag     = (CmbTrigger.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "Daily";
        var trigger = Enum.TryParse<TriggerKind>(tag, out var tk) ? tk : TriggerKind.Daily;

        TimeSpan time = TimeSpan.Zero;
        if (TimeSpan.TryParseExact(TxtTime.Text.Trim(), "hh\\:mm", null, out var t) ||
            TimeSpan.TryParseExact(TxtTime.Text.Trim(), "h\\:mm",  null, out t))
            time = t;

        DayOfWeek weekday = DayOfWeek.Monday;
        if (CmbWeekday.SelectedIndex >= 0)
            weekday = (DayOfWeek)((CmbWeekday.SelectedIndex + 1) % 7);

        DateTime startTime = DateTime.Today + time;
        if (trigger == TriggerKind.Once &&
            DateTime.TryParse(TxtDate.Text.Trim() + " " + TxtTime.Text.Trim(), out var dt))
            startTime = dt;

        Result = new TaskInfo
        {
            Name         = TxtName.Text.Trim(),
            Description  = TxtDesc.Text.Trim(),
            ExePath      = TxtExe.Text.Trim(),
            Arguments    = TxtArgs.Text.Trim(),
            WorkDir      = TxtWorkDir.Text.Trim(),
            TriggerType  = trigger,
            StartTime    = startTime,
            DailyTime    = time,
            WeeklyDay    = weekday,
            WeeklyTime   = time,
            RunAsHighest = ChkHighest.IsChecked == true,
            Enabled      = ChkEnabled.IsChecked == true,
        };
        DialogResult = true;
    }

    void Cancel_Click(object s, RoutedEventArgs e) => DialogResult = false;
}
