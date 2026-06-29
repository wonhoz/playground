using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using Stock.Fetch.Indicators;

namespace Stock.Fetch.Views.Controls;

/// <summary>차트에 표시할 지표 토글 + x축 날짜 포맷.</summary>
public sealed record ChartOptions(bool Bollinger, bool Ma, bool Rsi, bool Volume, string DateFormat = "MM-dd");

/// <summary>
/// 캔들 + 볼린저밴드 + 이동평균선(가격 패널) + 거래량(중단) + RSI(하단)를 Canvas에 직접 렌더링.
/// 외부 라이브러리 없이 WPF Shape으로 그려 다크 테마를 완전히 제어한다. 한국식: 상승=빨강, 하락=파랑.
/// (Stock.Watch CandleChart에서 포팅 + 이동평균선·지표 토글·x축 라벨 추가)
/// </summary>
public partial class CandleChart : UserControl
{
    private static readonly Brush Up = new SolidColorBrush(Color.FromRgb(0xEF, 0x53, 0x50));
    private static readonly Brush Down = new SolidColorBrush(Color.FromRgb(0x4A, 0x9E, 0xFF));
    private static readonly Brush Grid = new SolidColorBrush(Color.FromRgb(0x2A, 0x2A, 0x38));
    private static readonly Brush Axis = new SolidColorBrush(Color.FromRgb(0x88, 0x88, 0x88));
    private static readonly Brush BollMid = new SolidColorBrush(Color.FromRgb(0xFF, 0xC1, 0x07));
    private static readonly Brush BollBand = new SolidColorBrush(Color.FromRgb(0x6A, 0x9A, 0xE0));
    private static readonly Brush RsiLine = new SolidColorBrush(Color.FromRgb(0xCE, 0x93, 0xD8));
    private static readonly Brush Sma5B = new SolidColorBrush(Color.FromRgb(0x66, 0xBB, 0x6A));   // 초록
    private static readonly Brush Sma20B = new SolidColorBrush(Color.FromRgb(0xFF, 0x98, 0x00));  // 주황
    private static readonly Brush Sma60B = new SolidColorBrush(Color.FromRgb(0xAB, 0x47, 0xBC));  // 보라
    private static readonly Brush Cross = new SolidColorBrush(Color.FromRgb(0xAA, 0xAA, 0xAA));

    static CandleChart()
    {
        foreach (var b in new[] { Up, Down, Grid, Axis, BollMid, BollBand, RsiLine, Sma5B, Sma20B, Sma60B, Cross })
            b.Freeze();
    }

    private IndicatorSet? _set;
    private ChartOptions _opt = new(true, true, true, true);

    // 마우스 오버 히트테스트용(Redraw에서 갱신)
    private int _mStart;
    private double _mSlot;
    private double _mLeftPad;

    public CandleChart()
    {
        InitializeComponent();
        SizeChanged += (_, _) => Redraw();
        MouseMove += OnMouseMove;
        MouseLeave += OnMouseLeave;
    }

    public void Show(IndicatorSet set, ChartOptions opt)
    {
        _set = set;
        _opt = opt;
        EmptyHint.Visibility = Visibility.Collapsed;
        Redraw();
    }

    public void Clear()
    {
        _set = null;
        PlotCanvas.Children.Clear();
        EmptyHint.Visibility = Visibility.Visible;
    }

    private void Redraw()
    {
        PlotCanvas.Children.Clear();
        if (_set == null || _set.Candles.Count == 0) return;

        double w = ActualWidth, h = ActualHeight;
        if (w < 40 || h < 40) return;

        const double rightPad = 56;   // 가격 축 라벨 공간
        const double leftPad = 4;
        const double axisH = 18;      // 하단 x축 라벨 공간
        double plotW = w - rightPad - leftPad;

        // 패널 배분: 활성 보조패널(거래량·RSI) 수에 따라 가격 패널 높이 조정.
        double gap = 6;
        double usableH = h - axisH;
        int subCount = (_opt.Volume ? 1 : 0) + (_opt.Rsi ? 1 : 0);
        double priceFrac = subCount == 2 ? 0.58 : subCount == 1 ? 0.74 : 1.0;
        double priceH = usableH * priceFrac;

        double cursor = priceH + gap;
        double volTop = 0, volH = 0, rsiTop = 0, rsiH = 0;
        if (_opt.Volume) { volTop = cursor; volH = usableH * 0.18; cursor += volH + gap; }
        if (_opt.Rsi) { rsiTop = cursor; rsiH = usableH - rsiTop; }

        // 표시할 캔들 수. 캔들 너비(slot)는 일봉 기준 크기로 상한을 둬, 데이터가 적어도
        // 캔들이 과도하게 넓어지지 않게 한다(부족분은 우측 여백).
        const double maxSlot = 14.0;
        double slot = 9.0;
        int visible = Math.Min(_set.Candles.Count, Math.Max(20, (int)(plotW / slot)));
        int start = _set.Candles.Count - visible;
        slot = Math.Min(plotW / visible, maxSlot);
        double bodyW = Math.Max(2, slot * 0.62);

        // 가격 범위(캔들 고저 + 볼린저 상하단 포함)
        double pMin = double.MaxValue, pMax = double.MinValue;
        for (int i = start; i < _set.Candles.Count; i++)
        {
            pMin = Math.Min(pMin, (double)_set.Candles[i].Low);
            pMax = Math.Max(pMax, (double)_set.Candles[i].High);
            if (_opt.Bollinger && !double.IsNaN(_set.BollUpper[i])) pMax = Math.Max(pMax, _set.BollUpper[i]);
            if (_opt.Bollinger && !double.IsNaN(_set.BollLower[i])) pMin = Math.Min(pMin, _set.BollLower[i]);
        }
        double pad = (pMax - pMin) * 0.06;
        pMin -= pad; pMax += pad;
        if (pMax - pMin < 1e-6) pMax = pMin + 1;

        double X(int idx) => leftPad + (idx - start + 0.5) * slot;
        double YPrice(double p) => (1 - (p - pMin) / (pMax - pMin)) * priceH;

        DrawPriceGrid(pMin, pMax, priceH, leftPad, plotW);

        // ── 볼린저 밴드 ──
        if (_opt.Bollinger)
        {
            AddPolyline(start, _set.BollUpper, X, YPrice, BollBand, 1, 0.7);
            AddPolyline(start, _set.BollLower, X, YPrice, BollBand, 1, 0.7);
            AddPolyline(start, _set.BollMiddle, X, YPrice, BollMid, 1.2, 0.9);
        }

        // ── 이동평균선 ──
        if (_opt.Ma)
        {
            AddPolyline(start, _set.Sma5, X, YPrice, Sma5B, 1.1, 0.95);
            AddPolyline(start, _set.Sma20, X, YPrice, Sma20B, 1.1, 0.95);
            AddPolyline(start, _set.Sma60, X, YPrice, Sma60B, 1.1, 0.95);
            DrawMaLegend(leftPad + 2, 2);
        }

        // ── 캔들 ──
        for (int i = start; i < _set.Candles.Count; i++)
        {
            var c = _set.Candles[i];
            var brush = c.IsBullish ? Up : Down;
            double x = X(i);
            double yO = YPrice((double)c.Open), yC = YPrice((double)c.Close);
            double yH = YPrice((double)c.High), yL = YPrice((double)c.Low);

            PlotCanvas.Children.Add(new Line { X1 = x, X2 = x, Y1 = yH, Y2 = yL, Stroke = brush, StrokeThickness = 1 });
            double top = Math.Min(yO, yC);
            double bh = Math.Max(1, Math.Abs(yC - yO));
            var body = new Rectangle { Width = bodyW, Height = bh, Fill = brush };
            Canvas.SetLeft(body, x - bodyW / 2);
            Canvas.SetTop(body, top);
            PlotCanvas.Children.Add(body);
        }

        // ── 거래량 ──
        if (_opt.Volume)
        {
            double vMax = 1;
            for (int i = start; i < _set.Candles.Count; i++) vMax = Math.Max(vMax, _set.Candles[i].Volume);
            for (int i = start; i < _set.Candles.Count; i++)
            {
                var c = _set.Candles[i];
                double bh = (c.Volume / vMax) * volH;
                var bar = new Rectangle { Width = bodyW, Height = Math.Max(1, bh), Fill = c.IsBullish ? Up : Down, Opacity = 0.6 };
                Canvas.SetLeft(bar, X(i) - bodyW / 2);
                Canvas.SetTop(bar, volTop + volH - bh);
                PlotCanvas.Children.Add(bar);
            }
            AddPolyline(start, _set.VolumeMa20, X, v => volTop + volH - (v / vMax) * volH, BollMid, 1, 0.8);
            AddLabel("거래량", leftPad + 2, volTop, Axis, 10);
        }

        // ── RSI ──
        if (_opt.Rsi)
        {
            double YRsi(double r) => rsiTop + (1 - r / 100.0) * rsiH;
            foreach (var lvl in new[] { 70.0, 30.0 })
            {
                double y = YRsi(lvl);
                PlotCanvas.Children.Add(new Line { X1 = leftPad, X2 = leftPad + plotW, Y1 = y, Y2 = y, Stroke = Grid, StrokeThickness = 1, StrokeDashArray = new DoubleCollection { 3, 3 } });
                AddLabel(lvl.ToString("0"), leftPad + plotW + 4, y - 8, Axis, 9);
            }
            AddPolyline(start, _set.Rsi14, X, YRsi, RsiLine, 1.3, 1);
            AddLabel("RSI(14)", leftPad + 2, rsiTop, Axis, 10);
        }

        // ── x축 시간 라벨 ──
        DrawTimeAxis(start, X, usableH, plotW, leftPad);

        // 마우스 오버 히트테스트용 저장
        _mStart = start; _mSlot = slot; _mLeftPad = leftPad;
        OverlayCanvas.Children.Clear();
        InfoBox.Visibility = Visibility.Collapsed;
    }

    // ────────────────────────────── 마우스 오버 ──────────────────────────────
    private void OnMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (_set == null || _set.Candles.Count == 0 || _mSlot <= 0) return;
        var pos = e.GetPosition(PlotCanvas);
        int count = _set.Candles.Count;
        int idx = _mStart + (int)Math.Floor((pos.X - _mLeftPad) / _mSlot);
        idx = Math.Clamp(idx, _mStart, count - 1);

        OverlayCanvas.Children.Clear();
        double cx = _mLeftPad + (idx - _mStart + 0.5) * _mSlot;
        OverlayCanvas.Children.Add(new Line
        {
            X1 = cx, X2 = cx, Y1 = 0, Y2 = ActualHeight,
            Stroke = Cross, StrokeThickness = 0.8,
            StrokeDashArray = new DoubleCollection { 3, 3 }
        });

        InfoText.Text = BuildInfo(idx);
        InfoBox.Visibility = Visibility.Visible;
    }

    private void OnMouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
    {
        OverlayCanvas.Children.Clear();
        InfoBox.Visibility = Visibility.Collapsed;
    }

    private string BuildInfo(int idx)
    {
        var c = _set!.Candles[idx];
        string s = $"{c.Date.ToString(_opt.DateFormat)}\n" +
                   $"시 {c.Open:N0}  고 {c.High:N0}  저 {c.Low:N0}  종 {c.Close:N0}\n" +
                   $"거래량 {c.Volume:N0}";
        var extras = new List<string>();
        if (_opt.Ma && !double.IsNaN(_set.Sma20[idx])) extras.Add($"MA20 {_set.Sma20[idx]:N0}");
        if (_opt.Bollinger && !double.IsNaN(_set.BollUpper[idx]))
            extras.Add($"BB {_set.BollLower[idx]:N0}~{_set.BollUpper[idx]:N0}");
        if (_opt.Rsi && !double.IsNaN(_set.Rsi14[idx])) extras.Add($"RSI {_set.Rsi14[idx]:F1}");
        if (extras.Count > 0) s += "\n" + string.Join("  ", extras);
        return s;
    }

    private void DrawTimeAxis(int start, Func<int, double> x, double usableH, double plotW, double leftPad)
    {
        if (_set == null) return;
        int n = _set.Candles.Count;
        int ticks = Math.Min(6, n - start);
        if (ticks < 2) return;
        double y = usableH + 2;
        for (int t = 0; t < ticks; t++)
        {
            int idx = start + (int)((n - 1 - start) * (t / (double)(ticks - 1)));
            string label = _set.Candles[idx].Date.ToString(_opt.DateFormat);
            double px = x(idx);
            var tb = new TextBlock { Text = label, Foreground = Axis, FontSize = 9 };
            tb.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            double tx = Math.Clamp(px - tb.DesiredSize.Width / 2, leftPad, leftPad + plotW - tb.DesiredSize.Width);
            Canvas.SetLeft(tb, tx);
            Canvas.SetTop(tb, y);
            PlotCanvas.Children.Add(tb);
        }
    }

    private void DrawMaLegend(double x, double y)
    {
        double cx = x;
        foreach (var (label, brush) in new[] { ("MA5", Sma5B), ("MA20", Sma20B), ("MA60", Sma60B) })
        {
            var tb = new TextBlock { Text = label, Foreground = brush, FontSize = 10, FontWeight = FontWeights.SemiBold };
            Canvas.SetLeft(tb, cx);
            Canvas.SetTop(tb, y);
            PlotCanvas.Children.Add(tb);
            tb.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            cx += tb.DesiredSize.Width + 8;
        }
    }

    private void DrawPriceGrid(double pMin, double pMax, double priceH, double leftPad, double plotW)
    {
        const int lines = 4;
        for (int i = 0; i <= lines; i++)
        {
            double p = pMin + (pMax - pMin) * i / lines;
            double y = (1 - (p - pMin) / (pMax - pMin)) * priceH;
            PlotCanvas.Children.Add(new Line { X1 = leftPad, X2 = leftPad + plotW, Y1 = y, Y2 = y, Stroke = Grid, StrokeThickness = 1 });
            AddLabel(p.ToString("N0"), leftPad + plotW + 4, y - 8, Axis, 9);
        }
    }

    private void AddPolyline(int start, double[] series, Func<int, double> x, Func<double, double> y,
        Brush brush, double thickness, double opacity)
    {
        var pl = new Polyline { Stroke = brush, StrokeThickness = thickness, Opacity = opacity };
        for (int i = start; i < series.Length; i++)
        {
            if (double.IsNaN(series[i])) continue;
            pl.Points.Add(new Point(x(i), y(series[i])));
        }
        if (pl.Points.Count > 1) PlotCanvas.Children.Add(pl);
    }

    private void AddLabel(string text, double x, double y, Brush brush, double size)
    {
        var tb = new TextBlock { Text = text, Foreground = brush, FontSize = size };
        Canvas.SetLeft(tb, x);
        Canvas.SetTop(tb, y);
        PlotCanvas.Children.Add(tb);
    }
}
