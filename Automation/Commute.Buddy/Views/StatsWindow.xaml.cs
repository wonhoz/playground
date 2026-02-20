using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using CommuteBuddy.Services;

namespace CommuteBuddy.Views;

public partial class StatsWindow : Window
{
    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

    private readonly CommuteLogger _logger;
    private int _year  = DateTime.Now.Year;
    private int _month = DateTime.Now.Month;

    public StatsWindow(CommuteLogger logger)
    {
        InitializeComponent();
        _logger = logger;
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        // 다크 타이틀바
        var handle = new WindowInteropHelper(this).EnsureHandle();
        int dark   = 1;
        DwmSetWindowAttribute(handle, 20, ref dark, sizeof(int));

        LoadMonth();
    }

    private void LoadMonth()
    {
        TxtMonth.Text = $"{_year}년 {_month:00}월";

        var log = _logger.GetMonth(_year, _month);

        if (log == null || log.Entries.Count == 0)
        {
            SummaryGrid.ItemsSource     = null;
            DataGridEntries.ItemsSource = null;
            TxtEmpty.Visibility         = Visibility.Visible;
            return;
        }

        TxtEmpty.Visibility = Visibility.Collapsed;

        // ── 장소별 요약 ──────────────────────────────────────────────────
        SummaryGrid.ItemsSource = log.Entries
            .GroupBy(e => e.LocationName)
            .Select(g =>
            {
                var arrived = g.Where(e => e.Direction.StartsWith("arrived")).ToList();
                var left    = g.Where(e => e.Direction == "left").ToList();
                var days    = arrived.Select(e => e.Timestamp.Date).Distinct().Count();

                var avgArr = arrived.Count > 0
                    ? TimeSpan.FromTicks((long)arrived.Average(e => e.Timestamp.TimeOfDay.Ticks))
                              .ToString(@"hh\:mm")
                    : "-";
                var avgDep = left.Count > 0
                    ? TimeSpan.FromTicks((long)left.Average(e => e.Timestamp.TimeOfDay.Ticks))
                              .ToString(@"hh\:mm")
                    : "-";

                return new
                {
                    장소     = g.Key,
                    출근일수  = $"{days}일",
                    평균도착  = avgArr,
                    평균퇴근  = avgDep,
                };
            })
            .ToList();

        // ── 전체 로그 목록 ────────────────────────────────────────────────
        DataGridEntries.ItemsSource = log.Entries
            .OrderByDescending(e => e.Timestamp)
            .Select(e => new
            {
                날짜 = e.Timestamp.ToString("MM/dd (ddd)"),
                시각 = e.Timestamp.ToString("HH:mm"),
                장소 = e.LocationName,
                방향 = e.Direction.StartsWith("arrived") ? "▶ 도착" : "◀ 퇴근",
            })
            .ToList();
    }

    // ── 월 네비게이션 ─────────────────────────────────────────────────────

    private void PrevMonth_Click(object sender, RoutedEventArgs e)
    {
        if (_month == 1) { _year--; _month = 12; }
        else _month--;
        LoadMonth();
    }

    private void NextMonth_Click(object sender, RoutedEventArgs e)
    {
        if (_month == 12) { _year++; _month = 1; }
        else _month++;
        LoadMonth();
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
