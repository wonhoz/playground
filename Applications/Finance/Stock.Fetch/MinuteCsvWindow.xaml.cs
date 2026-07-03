using System.Windows;
using System.Windows.Controls;
using Stock.Fetch.Services;

namespace Stock.Fetch;

/// <summary>분봉 CSV 다운로드용 날짜 선택 다이얼로그. 확인 시 <see cref="SelectedDate"/>에 영업일이 담긴다.</summary>
public partial class MinuteCsvWindow : Window
{
    public DateTime SelectedDate { get; private set; } = DateTime.Today;

    public MinuteCsvWindow()
    {
        InitializeComponent();
        NativeTheme.ApplyDarkTitleBar(this);
        DateBox.Text = LastBusinessDay(DateTime.Today).ToString("yyyy-MM-dd");
    }

    /// <summary>주말이면 직전 금요일로 보정.</summary>
    private static DateTime LastBusinessDay(DateTime d)
    {
        while (d.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday) d = d.AddDays(-1);
        return d;
    }

    private void Shift_Click(object sender, RoutedEventArgs e)
    {
        if (!DateTime.TryParse(DateBox.Text.Trim(), out var d)) d = DateTime.Today;
        int dir = int.Parse((string)((Button)sender).Tag!);
        d = d.AddDays(dir);
        while (d.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday) d = d.AddDays(dir);   // 주말 건너뛰기
        DateBox.Text = d.ToString("yyyy-MM-dd");
        ErrorText.Text = "";
    }

    private void Today_Click(object sender, RoutedEventArgs e)
    {
        DateBox.Text = LastBusinessDay(DateTime.Today).ToString("yyyy-MM-dd");
        ErrorText.Text = "";
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (!DateTime.TryParse(DateBox.Text.Trim(), out var d))
        { ErrorText.Text = "⚠ 날짜 형식은 yyyy-MM-dd 입니다."; return; }
        if (d.Date > DateTime.Today)
        { ErrorText.Text = "⚠ 미래 날짜는 조회할 수 없습니다."; return; }
        if (d.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
        { ErrorText.Text = "⚠ 주말은 휴장일입니다."; return; }

        SelectedDate = d.Date;
        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
