using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace TextForge.Views;

public partial class TimestampView : UserControl
{
    private readonly DispatcherTimer _clockTimer;
    private static readonly TimeZoneInfo KstZone = GetKstZone();
    private static TimeZoneInfo GetKstZone()
    {
        try { return TimeZoneInfo.FindSystemTimeZoneById("Korea Standard Time"); }
        catch { return TimeZoneInfo.Utc; }
    }

    public TimestampView()
    {
        InitializeComponent();

        _clockTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _clockTimer.Tick += (_, _) => UpdateClock();
        _clockTimer.Start();
        UpdateClock();

        Loaded   += (_, _) => _clockTimer.Start();
        Unloaded += (_, _) => _clockTimer.Stop();
    }

    private void UpdateClock()
    {
        var kst = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, KstZone);
        NowText.Text = kst.ToString("yyyy-MM-dd HH:mm:ss");
    }

    private void UseNow_Click(object sender, RoutedEventArgs e)
    {
        var now = DateTimeOffset.UtcNow;
        UnixInputBox.Text = now.ToUnixTimeSeconds().ToString();
        SecRadio.IsChecked = true;
    }

    private void Unix_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (!IsLoaded) return;
        ConvertUnixToDate();
    }

    private void Unit_Changed(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded) return;
        ConvertUnixToDate();
    }

    private void ConvertUnixToDate()
    {
        var input = UnixInputBox.Text.Trim();
        if (!long.TryParse(input, out var ts))
        {
            UtcBox.Text      = string.Empty;
            KstBox.Text      = string.Empty;
            RelativeBox.Text = string.Empty;
            return;
        }

        try
        {
            DateTimeOffset dt;
            if (MsRadio.IsChecked == true)
                dt = DateTimeOffset.FromUnixTimeMilliseconds(ts);
            else
                dt = DateTimeOffset.FromUnixTimeSeconds(ts);

            UtcBox.Text = dt.UtcDateTime.ToString("yyyy-MM-dd HH:mm:ss") + " UTC";

            var kst = TimeZoneInfo.ConvertTimeFromUtc(dt.UtcDateTime, KstZone);
            KstBox.Text = kst.ToString("yyyy-MM-dd HH:mm:ss") + " KST";

            RelativeBox.Text = GetRelativeTime(dt.UtcDateTime);
        }
        catch
        {
            UtcBox.Text      = "변환 오류";
            KstBox.Text      = string.Empty;
            RelativeBox.Text = string.Empty;
        }
    }

    private void Date_TextChanged(object sender, TextChangedEventArgs e) { }

    private void DateConvert_Click(object sender, RoutedEventArgs e)
    {
        var input = DateInputBox.Text.Trim();
        if (!DateTime.TryParse(input, out var dt))
        {
            UnixSecOutput.Text = "파싱 오류";
            UnixMsOutput.Text  = string.Empty;
            return;
        }

        var offset = new DateTimeOffset(dt, TimeSpan.Zero);
        UnixSecOutput.Text = offset.ToUnixTimeSeconds().ToString();
        UnixMsOutput.Text  = offset.ToUnixTimeMilliseconds().ToString();
    }

    private static string GetRelativeTime(DateTime utc)
    {
        var diff = DateTime.UtcNow - utc;

        if (Math.Abs(diff.TotalSeconds) < 60)
            return $"{(diff.TotalSeconds < 0 ? "+" : "-")}{Math.Abs((int)diff.TotalSeconds)}초";

        if (Math.Abs(diff.TotalMinutes) < 60)
            return $"{(diff.TotalMinutes < 0 ? "+" : "")}{(int)diff.TotalMinutes}분 전";

        if (Math.Abs(diff.TotalHours) < 24)
            return $"{(diff.TotalHours < 0 ? "+" : "")}{(int)diff.TotalHours}시간 전";

        if (Math.Abs(diff.TotalDays) < 30)
            return $"{(diff.TotalDays < 0 ? "+" : "")}{(int)diff.TotalDays}일 전";

        return $"{(int)diff.TotalDays}일 전";
    }
}
