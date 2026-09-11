using KeyMacro.Models;
using KeyMacro.Services;

namespace KeyMacro.Views;

/// <summary>웹 시각을 읽어 PC 시계를 맞추는 창</summary>
public partial class TimeSyncDialog : Window
{
    private readonly AppSettings _settings;
    private TimeProbe? _probe;

    private TimeSyncDialog(AppSettings settings)
    {
        InitializeComponent();
        DarkTitle.Apply(this);

        _settings = settings;
        TxtUrl.Text = settings.TimeSyncUrl;
        ChkPrecise.IsChecked = settings.TimeSyncPrecise;

        if (SystemClock.IsElevated)
            TxtNote.Text = "관리자 권한으로 실행 중이라 바로 적용됩니다.";
    }

    public static void Show(Window owner, AppSettings settings)
        => new TimeSyncDialog(settings) { Owner = owner }.ShowDialog();

    private void BtnPreset_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as Button)?.Tag is string url) TxtUrl.Text = url;
    }

    private async void BtnProbe_Click(object sender, RoutedEventArgs e)
    {
        BtnProbe.IsEnabled = false;
        BtnApply.IsEnabled = false;
        TxtDetail.Text = "확인 중...";

        try
        {
            _settings.TimeSyncUrl = TxtUrl.Text.Trim();
            _settings.TimeSyncPrecise = ChkPrecise.IsChecked == true;

            _probe = await TimeSync.ProbeAsync(_settings.TimeSyncUrl, _settings.TimeSyncPrecise);

            var serverLocal = _probe.ServerUtc.ToLocalTime();
            var pcLocal = _probe.LocalUtc.ToLocalTime();

            TxtServer.Text = serverLocal.ToString("yyyy-MM-dd HH:mm:ss.fff");
            TxtLocal.Text = pcLocal.ToString("yyyy-MM-dd HH:mm:ss.fff");

            double ms = _probe.Offset.TotalMilliseconds;
            TxtDiff.Text = $"{(ms >= 0 ? "+" : "")}{ms:N0} ms  ({(ms >= 0 ? "PC 가 느림" : "PC 가 빠름")})";
            TxtDiff.Foreground = (SolidColorBrush)FindResource(
                Math.Abs(ms) < 1000 ? "SuccessBrush" : "AccentBrush");

            TxtDetail.Text = $"왕복 지연 {_probe.RoundTrip.TotalMilliseconds:N0}ms · 요청 {_probe.Attempts}회 · " +
                             (_probe.Precise ? "초 경계 포착 (오차 ±0.1초 수준)" : "초 단위 헤더 기준 (오차 최대 1초)");

            BtnApply.IsEnabled = true;
        }
        catch (Exception ex)
        {
            TxtDetail.Text = $"확인 실패: {ex.Message}";
            TxtDiff.Text = "—";
        }
        finally
        {
            BtnProbe.IsEnabled = true;
        }
    }

    private void BtnApply_Click(object sender, RoutedEventArgs e)
    {
        if (_probe is null) return;

        // 확인 시점 이후 흐른 시간도 그대로 반영되도록 '차이'만 넘긴다
        var (ok, message) = SystemClock.Apply(_probe.Offset);
        TxtDetail.Text = message;

        if (!ok) return;

        TxtLocal.Text = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
        TxtDiff.Text = "0 ms  (맞춤 완료)";
        TxtDiff.Foreground = (SolidColorBrush)FindResource("SuccessBrush");
        BtnApply.IsEnabled = false;
    }

    private void BtnClose_Click(object sender, RoutedEventArgs e) => Close();
}
