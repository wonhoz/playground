using System.Windows.Media;
using System.Windows.Shapes;
using Brush = System.Windows.Media.Brush;
using Point = System.Windows.Point;

namespace BurnRate;

public partial class MainWindow : Window
{
    private readonly BatteryService _svc;

    public MainWindow(BatteryService svc)
    {
        InitializeComponent();
        _svc = svc;

        if (_svc.Latest != null)
            RefreshData(_svc.Latest);

        SessionList.ItemsSource = _svc.Sessions;
        DrawGraph();
    }

    // ── 외부에서 호출 (App.xaml.cs) ──────────────────────────────
    public void RefreshData(BatteryInfo info)
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.Invoke(() => RefreshData(info));
            return;
        }

        // ① 충전 % 헤더
        TxtPercent.Text  = $"{info.ChargePercent}%";
        TxtPercent.Foreground = info.ChargePercent > 20
            ? (Brush)FindResource("GreenBrush")
            : (Brush)FindResource("RedBrush");

        var statusText = info.Status switch
        {
            ChargingStatus.Charging      => "⚡ 충전 중",
            ChargingStatus.FullyCharged  => "✅ 완충",
            ChargingStatus.Discharging   => "🔋 방전 중",
            _                            => "알 수 없음"
        };
        TxtStatus.Text = statusText;

        if (info.EstRunTimeMinutes > 0)
        {
            int h = info.EstRunTimeMinutes / 60;
            int m = info.EstRunTimeMinutes % 60;
            TxtRunTime.Text = h > 0 ? $"잔여 {h}시간 {m}분" : $"잔여 {m}분";
        }
        else
        {
            TxtRunTime.Text = "";
        }

        // ② 건강도
        int health = info.HealthPercent;
        if (health >= 0)
        {
            TxtHealth.Text = $"{health}%";
            TxtHealth.Foreground = health >= 80
                ? (Brush)FindResource("GreenBrush")
                : health >= 60
                    ? (Brush)FindResource("OrangeBrush")
                    : (Brush)FindResource("RedBrush");

            // 건강도 바 너비 (최대 116px = 120 - 4 패딩)
            HealthBar.Width      = Math.Max(4, 116 * health / 100.0);
            HealthBar.Background = TxtHealth.Foreground;

            TxtHealthNote.Text = health >= 80 ? "양호" : health >= 60 ? "주의" : "교체 권장";
        }
        else
        {
            TxtHealth.Text    = "─";
            TxtHealthNote.Text = "";
        }

        // ③ 상세 스탯
        TxtDesignCap.Text  = info.DesignCapMwh > 0
            ? $"{info.DesignCapMwh / 1000.0:F1} Wh"
            : "─";
        TxtFullCap.Text    = info.FullChargeCapMwh > 0
            ? $"{info.FullChargeCapMwh / 1000.0:F1} Wh"
            : "─";
        TxtCycles.Text     = info.CycleCount > 0 ? $"{info.CycleCount} 회" : "─";

        TxtRemain.Text     = info.EstRunTimeMinutes > 0
            ? (info.EstRunTimeMinutes >= 60
                ? $"{info.EstRunTimeMinutes / 60}h {info.EstRunTimeMinutes % 60}m"
                : $"{info.EstRunTimeMinutes}분")
            : "─";

        TxtLastUpdate.Text = info.Timestamp.ToString("HH:mm:ss");

        TxtTip.Text = GetChargingTip(info);

        // 그래프 업데이트
        DrawGraph();

        // 세션 리스트 갱신
        SessionList.Items.Refresh();
    }

    // ── 방전 곡선 그래프 ──────────────────────────────────────────
    private void DrawGraph()
    {
        GraphCanvas.Children.Clear();

        List<ChargePoint> history;
        lock (_svc.History)
            history = [.. _svc.History];

        if (history.Count < 2) return;

        double w = GraphCanvas.ActualWidth;
        double h = GraphCanvas.ActualHeight;
        if (w < 10 || h < 10) return;

        double xStep = w / (history.Count - 1);

        // 방전 라인 (파랑)
        var dischargePts = new PointCollection();
        // 충전 라인 (초록)
        var chargePts    = new PointCollection();

        for (int i = 0; i < history.Count; i++)
        {
            var pt = history[i];
            double x = i * xStep;
            double y = h - (pt.Percent / 100.0 * (h - 4)) - 2;

            if (pt.IsCharging)
                chargePts.Add(new Point(x, y));
            else
                dischargePts.Add(new Point(x, y));
        }

        if (dischargePts.Count >= 2)
        {
            GraphCanvas.Children.Add(new Polyline
            {
                Points           = dischargePts,
                Stroke           = (Brush)FindResource("AccentBrush"),
                StrokeThickness  = 1.5,
                StrokeLineJoin   = PenLineJoin.Round
            });
        }

        if (chargePts.Count >= 2)
        {
            GraphCanvas.Children.Add(new Polyline
            {
                Points           = chargePts,
                Stroke           = (Brush)FindResource("GreenBrush"),
                StrokeThickness  = 1.5,
                StrokeLineJoin   = PenLineJoin.Round
            });
        }

        // 현재 위치 마커
        var last = history[^1];
        double lx = (history.Count - 1) * xStep;
        double ly = h - (last.Percent / 100.0 * (h - 4)) - 2;
        GraphCanvas.Children.Add(new Ellipse
        {
            Width  = 7, Height = 7,
            Fill   = last.IsCharging ? (Brush)FindResource("GreenBrush") : (Brush)FindResource("AccentBrush"),
            Margin = new Thickness(lx - 3.5, ly - 3.5, 0, 0)
        });
    }

    // ── 충전 팁 ──────────────────────────────────────────────────
    private static string GetChargingTip(BatteryInfo info) => info.ChargePercent switch
    {
        > 95 when info.IsCharging => "완충 시 플러그를 뽑으면 수명이 늘어요",
        < 20                      => "빠른 충전을 권장합니다",
        >= 20 and <= 80           => "20~80% 유지가 최적 수명 범위입니다",
        _                         => "배터리 상태 양호"
    };

    // ── 윈도우 이벤트 ─────────────────────────────────────────────
    private void TitleBar_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (e.LeftButton == System.Windows.Input.MouseButtonState.Pressed)
            DragMove();
    }

    private void MinBtn_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
    private void CloseBtn_Click(object sender, RoutedEventArgs e) => Hide();

    protected override void OnContentRendered(EventArgs e)
    {
        base.OnContentRendered(e);
        DrawGraph();
    }
}
