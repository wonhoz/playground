using System.Windows;
using System.Windows.Controls;
using Stock.Catch.Services;

namespace Stock.Catch;

/// <summary>
/// 분봉 CSV 다운로드용 기간 선택 다이얼로그. 확인 시 <see cref="FromDate"/>~<see cref="ToDate"/>에
/// 영업일 범위가 담긴다(하루만 받으려면 동일 날짜).
/// </summary>
public partial class MinuteCsvWindow : Window
{
    public DateTime FromDate { get; private set; } = DateTime.Today;
    public DateTime ToDate { get; private set; } = DateTime.Today;

    /// <summary>과도한 KIS 호출 방지용 최대 기간(달력일).</summary>
    private const int MaxRangeDays = 45;

    /// <summary>초기 기간을 지정하면(메인 창의 조회 기간) 그 값으로 시작한다. 미지정 시 최근 영업일 하루.</summary>
    public MinuteCsvWindow(DateTime? initFrom = null, DateTime? initTo = null)
    {
        InitializeComponent();
        NativeTheme.ApplyDarkTitleBar(this);

        var to = LastBusinessDay(initTo?.Date is { } t && t <= DateTime.Today ? t : DateTime.Today);
        var from = initFrom?.Date is { } f && f <= to ? f : to;
        if ((to - from).TotalDays >= MaxRangeDays)
        {
            from = to.AddDays(-(MaxRangeDays - 1));
            ErrorText.Text = $"⚠ 기간 최대 {MaxRangeDays}일 — 시작일을 보정했습니다.";
        }
        FromBox.Text = from.ToString("yyyy-MM-dd");
        ToBox.Text = to.ToString("yyyy-MM-dd");
    }

    /// <summary>주말이면 직전 금요일로 보정.</summary>
    private static DateTime LastBusinessDay(DateTime d)
    {
        while (d.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday) d = d.AddDays(-1);
        return d;
    }

    private void Today_Click(object sender, RoutedEventArgs e)
    {
        string d = LastBusinessDay(DateTime.Today).ToString("yyyy-MM-dd");
        FromBox.Text = d;
        ToBox.Text = d;
        ErrorText.Text = "";
    }

    /// <summary>프리셋: 종료=최근 영업일, 시작=종료−N일.</summary>
    private void Preset_Click(object sender, RoutedEventArgs e)
    {
        int days = int.Parse((string)((Button)sender).Tag!);
        var to = LastBusinessDay(DateTime.Today);
        FromBox.Text = to.AddDays(-days + 1).ToString("yyyy-MM-dd");
        ToBox.Text = to.ToString("yyyy-MM-dd");
        ErrorText.Text = "";
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (!DateTime.TryParse(FromBox.Text.Trim(), out var from) || !DateTime.TryParse(ToBox.Text.Trim(), out var to))
        { ErrorText.Text = "⚠ 날짜 형식은 yyyy-MM-dd 입니다."; return; }
        if (from.Date > to.Date)
        { ErrorText.Text = "⚠ 시작일이 종료일보다 늦습니다."; return; }
        if (to.Date > DateTime.Today)
        { ErrorText.Text = "⚠ 미래 날짜는 조회할 수 없습니다."; return; }
        if ((to.Date - from.Date).TotalDays >= MaxRangeDays)
        { ErrorText.Text = $"⚠ 기간은 최대 {MaxRangeDays}일까지입니다."; return; }

        FromDate = from.Date;
        ToDate = to.Date;
        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
