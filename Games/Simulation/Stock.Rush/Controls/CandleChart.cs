using System.Globalization;

namespace StockRush.Controls;

/// <summary>
/// 5분봉 캔들차트 + 거래량 바. OnRender 직접 드로잉 (틱마다 InvalidateVisual 호출).
/// 한국식 색상: 양봉 빨강 / 음봉 파랑. 평균단가·현재가 라인 표시.
/// </summary>
public class CandleChart : FrameworkElement
{
    private const int VisibleCandles = 64;
    private const double VolumeRatio = 0.22;
    private const double RightAxisWidth = 64;

    private static readonly Brush BgBrush = MakeBrush(0x16, 0x16, 0x1E);
    private static readonly Pen GridPen = MakePen(0x2A, 0x2A, 0x36, 1);
    private static readonly Brush LabelBrush = MakeBrush(0x77, 0x77, 0x8A);
    private static readonly Pen UpPen = new(Ui.UpBrush, 1);
    private static readonly Pen DownPen = new(Ui.DownBrush, 1);
    private static readonly Pen LastPricePen = MakeDashPen(0xE8, 0xC4, 0x4A, 1);
    private static readonly Pen AvgPricePen = MakeDashPen(0x4A, 0xE8, 0x9A, 1);

    public Stock? Stock { get; set; }
    /// <summary>보유 평균단가 (0이면 미표시)</summary>
    public long AvgPrice { get; set; }

    static CandleChart()
    {
        UpPen.Freeze(); DownPen.Freeze();
    }

    private static SolidColorBrush MakeBrush(byte r, byte g, byte b)
    {
        var br = new SolidColorBrush(Color.FromRgb(r, g, b));
        br.Freeze();
        return br;
    }

    private static Pen MakePen(byte r, byte g, byte b, double w)
    {
        var p = new Pen(MakeBrush(r, g, b), w);
        p.Freeze();
        return p;
    }

    private static Pen MakeDashPen(byte r, byte g, byte b, double w)
    {
        var p = new Pen(MakeBrush(r, g, b), w) { DashStyle = new DashStyle(new double[] { 4, 3 }, 0) };
        p.Freeze();
        return p;
    }

    protected override void OnRender(DrawingContext dc)
    {
        var w = ActualWidth;
        var h = ActualHeight;
        dc.DrawRectangle(BgBrush, null, new Rect(0, 0, w, h));
        if (Stock == null || w < 80 || h < 80) return;

        var candles = new List<Candle>(Stock.Candles);
        if (Stock.CurrentCandle != null) candles.Add(Stock.CurrentCandle);
        if (candles.Count == 0) return;
        if (candles.Count > VisibleCandles) candles = candles.Skip(candles.Count - VisibleCandles).ToList();

        var chartW = w - RightAxisWidth;
        var priceH = h * (1 - VolumeRatio) - 8;
        var volTop = priceH + 8;
        var volH = h - volTop - 2;

        var minP = candles.Min(c => c.Low);
        var maxP = candles.Max(c => c.High);
        if (AvgPrice > 0) { minP = Math.Min(minP, AvgPrice); maxP = Math.Max(maxP, AvgPrice); }
        var pad = Math.Max(1, (long)((maxP - minP) * 0.08));
        minP -= pad; maxP += pad;
        if (maxP <= minP) maxP = minP + 1;
        var maxV = Math.Max(1, candles.Max(c => c.Volume));

        double Y(long price) => priceH * (1 - (double)(price - minP) / (maxP - minP));

        var dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;

        // 가로 그리드 + 가격 라벨
        for (var i = 0; i <= 4; i++)
        {
            var y = priceH * i / 4.0;
            dc.DrawLine(GridPen, new Point(0, y), new Point(chartW, y));
            var price = maxP - (long)((maxP - minP) * i / 4.0);
            var ft = Text(price.ToString("N0"), 10, LabelBrush, dpi);
            dc.DrawText(ft, new Point(chartW + 6, y - ft.Height / 2));
        }

        var slot = chartW / VisibleCandles;
        var bodyW = Math.Max(2, slot * 0.62);

        for (var i = 0; i < candles.Count; i++)
        {
            var c = candles[i];
            var x = slot * i + slot / 2;
            var up = c.Close >= c.Open;
            var brush = up ? Ui.UpBrush : Ui.DownBrush;
            var pen = up ? UpPen : DownPen;

            // 꼬리
            dc.DrawLine(pen, new Point(x, Y(c.High)), new Point(x, Y(c.Low)));

            // 몸통
            var yo = Y(c.Open);
            var yc = Y(c.Close);
            var top = Math.Min(yo, yc);
            var bh = Math.Max(1, Math.Abs(yo - yc));
            dc.DrawRectangle(brush, null, new Rect(x - bodyW / 2, top, bodyW, bh));

            // 거래량
            var vh = volH * c.Volume / maxV;
            dc.DrawRectangle(up ? Ui.UpBrush : Ui.DownBrush, null,
                new Rect(x - bodyW / 2, volTop + (volH - vh), bodyW, Math.Max(1, vh)));
        }

        // 평균단가 라인
        if (AvgPrice > 0 && AvgPrice >= minP && AvgPrice <= maxP)
        {
            var y = Y(AvgPrice);
            dc.DrawLine(AvgPricePen, new Point(0, y), new Point(chartW, y));
            var ft = Text($"평단 {AvgPrice:N0}", 10, AvgPricePen.Brush, dpi);
            dc.DrawText(ft, new Point(chartW - ft.Width - 4, y - ft.Height - 1));
        }

        // 현재가 라인 + 라벨
        var last = Stock.Price;
        if (last >= minP && last <= maxP)
        {
            var y = Y(last);
            dc.DrawLine(LastPricePen, new Point(0, y), new Point(chartW, y));
            var ft = Text(last.ToString("N0"), 11, Brushes.Black, dpi);
            var rect = new Rect(chartW + 2, y - ft.Height / 2 - 2, RightAxisWidth - 4, ft.Height + 4);
            dc.DrawRoundedRectangle(LastPricePen.Brush, null, rect, 3, 3);
            dc.DrawText(ft, new Point(chartW + 6, y - ft.Height / 2));
        }
    }

    private static FormattedText Text(string text, double size, Brush brush, double dpi) =>
        new(text, CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
            new Typeface("Segoe UI"), size, brush, dpi);
}
