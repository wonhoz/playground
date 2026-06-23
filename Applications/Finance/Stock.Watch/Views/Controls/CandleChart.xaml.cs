using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using Stock.Watch.Indicators;
using Stock.Watch.Models;

namespace Stock.Watch.Views.Controls;

/// <summary>
/// 캔들 + 볼린저밴드(가격 패널) + 거래량 막대(중단) + RSI(하단)를 Canvas에 직접 렌더링하는 차트.
/// 외부 차트 라이브러리 없이 WPF Shape으로 그려 다크 테마를 완전히 제어한다.
/// 한국식 색상: 상승=빨강, 하락=파랑.
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

    private IndicatorSet? _set;

    public CandleChart()
    {
        InitializeComponent();
        SizeChanged += (_, _) => Redraw();
    }

    public void Show(IndicatorSet set)
    {
        _set = set;
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
        double plotW = w - rightPad - leftPad;

        // 패널 높이 배분: 가격 58% / 거래량 18% / RSI 24%
        double priceH = h * 0.58;
        double volTop = priceH + 6;
        double volH = h * 0.18;
        double rsiTop = volTop + volH + 6;
        double rsiH = h - rsiTop - 18;

        // 화면 폭에 맞춰 표시할 캔들 수 결정
        double slot = 9.0; // 캔들 하나당 가로 픽셀
        int visible = Math.Min(_set.Candles.Count, Math.Max(20, (int)(plotW / slot)));
        int start = _set.Candles.Count - visible;
        slot = plotW / visible;
        double bodyW = Math.Max(2, slot * 0.62);

        // 가격 범위(캔들 고저 + 볼린저 상하단 포함)
        double pMin = double.MaxValue, pMax = double.MinValue;
        for (int i = start; i < _set.Candles.Count; i++)
        {
            pMin = Math.Min(pMin, (double)_set.Candles[i].Low);
            pMax = Math.Max(pMax, (double)_set.Candles[i].High);
            if (!double.IsNaN(_set.BollUpper[i])) pMax = Math.Max(pMax, _set.BollUpper[i]);
            if (!double.IsNaN(_set.BollLower[i])) pMin = Math.Min(pMin, _set.BollLower[i]);
        }
        double pad = (pMax - pMin) * 0.06;
        pMin -= pad; pMax += pad;
        if (pMax - pMin < 1e-6) pMax = pMin + 1;

        double X(int idx) => leftPad + (idx - start + 0.5) * slot;
        double YPrice(double p) => (1 - (p - pMin) / (pMax - pMin)) * priceH;

        DrawPriceGrid(pMin, pMax, priceH, w, leftPad, plotW);

        // ── 볼린저 밴드 ──
        AddPolyline(start, _set.BollUpper, X, YPrice, BollBand, 1, 0.7);
        AddPolyline(start, _set.BollLower, X, YPrice, BollBand, 1, 0.7);
        AddPolyline(start, _set.BollMiddle, X, YPrice, BollMid, 1.2, 0.9);

        // ── 캔들 ──
        for (int i = start; i < _set.Candles.Count; i++)
        {
            var c = _set.Candles[i];
            var brush = c.IsBullish ? Up : Down;
            double x = X(i);
            double yO = YPrice((double)c.Open), yC = YPrice((double)c.Close);
            double yH = YPrice((double)c.High), yL = YPrice((double)c.Low);

            // 심지
            PlotCanvas.Children.Add(new Line
            {
                X1 = x, X2 = x, Y1 = yH, Y2 = yL,
                Stroke = brush, StrokeThickness = 1
            });
            // 몸통
            double top = Math.Min(yO, yC);
            double bh = Math.Max(1, Math.Abs(yC - yO));
            var body = new Rectangle { Width = bodyW, Height = bh, Fill = brush };
            Canvas.SetLeft(body, x - bodyW / 2);
            Canvas.SetTop(body, top);
            PlotCanvas.Children.Add(body);
        }

        // ── 거래량 ──
        double vMax = 1;
        for (int i = start; i < _set.Candles.Count; i++) vMax = Math.Max(vMax, _set.Candles[i].Volume);
        for (int i = start; i < _set.Candles.Count; i++)
        {
            var c = _set.Candles[i];
            double bh = (c.Volume / vMax) * volH;
            var bar = new Rectangle
            {
                Width = bodyW, Height = Math.Max(1, bh),
                Fill = c.IsBullish ? Up : Down, Opacity = 0.65
            };
            Canvas.SetLeft(bar, X(i) - bodyW / 2);
            Canvas.SetTop(bar, volTop + volH - bh);
            PlotCanvas.Children.Add(bar);
        }
        AddPolyline(start, _set.VolumeMa20, X, v => volTop + volH - (v / vMax) * volH, BollMid, 1, 0.8);
        AddLabel("거래량", leftPad + 2, volTop, Axis, 10);

        // ── RSI ──
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

    private void DrawPriceGrid(double pMin, double pMax, double priceH, double w, double leftPad, double plotW)
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
