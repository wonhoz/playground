using System.Windows.Media;
using WpfSize   = System.Windows.Size;
using WpfBrush  = System.Windows.Media.Brush;
using WpfPoint  = System.Windows.Point;
using WpfRect   = System.Windows.Rect;
using WpfFlow   = System.Windows.FlowDirection;

namespace PadForge.Views;

/// <summary>D-pad 방향키 시각화 UserControl (코드-비하인드 전용)</summary>
public class DpadVisual : FrameworkElement
{
    private static readonly SolidColorBrush BgBrush  = new(WpfColor.FromArgb(255, 30, 30, 46));
    private static readonly SolidColorBrush ActBrush = new(WpfColor.FromArgb(255, 108, 99, 255));
    private static readonly SolidColorBrush InvBrush = new(WpfColor.FromArgb(255, 51, 51, 68));

    public static readonly DependencyProperty DpadUpProperty    = Dep("DpadUp");
    public static readonly DependencyProperty DpadDownProperty  = Dep("DpadDown");
    public static readonly DependencyProperty DpadLeftProperty  = Dep("DpadLeft");
    public static readonly DependencyProperty DpadRightProperty = Dep("DpadRight");

    private static DependencyProperty Dep(string name) =>
        DependencyProperty.Register(name, typeof(bool), typeof(DpadVisual),
            new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender));

    public bool Up    { get => (bool)GetValue(DpadUpProperty);    set => SetValue(DpadUpProperty, value); }
    public bool Down  { get => (bool)GetValue(DpadDownProperty);  set => SetValue(DpadDownProperty, value); }
    public bool Left  { get => (bool)GetValue(DpadLeftProperty);  set => SetValue(DpadLeftProperty, value); }
    public bool Right { get => (bool)GetValue(DpadRightProperty); set => SetValue(DpadRightProperty, value); }

    protected override void OnRender(DrawingContext dc)
    {
        double w = ActualWidth, h = ActualHeight;
        double cell = Math.Min(w, h) / 3;
        double cx = w / 2, cy = h / 2;

        DrawCell(dc, cx - cell / 2, cy - cell * 1.5, cell, cell, Up);   // 위
        DrawCell(dc, cx - cell / 2, cy + cell * 0.5, cell, cell, Down); // 아래
        DrawCell(dc, cx - cell * 1.5, cy - cell / 2, cell, cell, Left); // 왼
        DrawCell(dc, cx + cell * 0.5, cy - cell / 2, cell, cell, Right);// 오
        DrawCell(dc, cx - cell / 2, cy - cell / 2, cell, cell, false);  // 중앙
    }

    private static void DrawCell(DrawingContext dc, double x, double y, double w, double h, bool active)
    {
        dc.DrawRectangle(active ? ActBrush : InvBrush, null, new WpfRect(x + 1, y + 1, w - 2, h - 2));
    }
}

/// <summary>버튼 A/B/X/Y 시각화 (색상 원)</summary>
public class ButtonVisual : FrameworkElement
{
    public static readonly DependencyProperty AProperty = DProp("A");
    public static readonly DependencyProperty BProperty = DProp("B");
    public static readonly DependencyProperty XProperty = DProp("X");
    public static readonly DependencyProperty YProperty = DProp("Y");

    private static DependencyProperty DProp(string name) =>
        DependencyProperty.Register(name, typeof(bool), typeof(ButtonVisual),
            new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender));

    public bool A { get => (bool)GetValue(AProperty); set => SetValue(AProperty, value); }
    public bool B { get => (bool)GetValue(BProperty); set => SetValue(BProperty, value); }
    public bool X { get => (bool)GetValue(XProperty); set => SetValue(XProperty, value); }
    public bool Y { get => (bool)GetValue(YProperty); set => SetValue(YProperty, value); }

    private static readonly WpfBrush[] Colors =
    [
        new SolidColorBrush(WpfColor.FromRgb(76, 175, 80)),   // A 녹색
        new SolidColorBrush(WpfColor.FromRgb(244, 67, 54)),   // B 빨강
        new SolidColorBrush(WpfColor.FromRgb(33, 150, 243)),  // X 파랑
        new SolidColorBrush(WpfColor.FromRgb(255, 193, 7)),   // Y 노랑
    ];

    private static readonly SolidColorBrush DimBrush = new(WpfColor.FromArgb(80, 128, 128, 128));

    protected override WpfSize MeasureOverride(WpfSize availableSize)
        => new(80, 24);

    protected override void OnRender(DrawingContext dc)
    {
        double r = 9;
        double[] xs = [10, 30, 50, 70];
        double cy = ActualHeight / 2;
        bool[] states = [A, B, X, Y];
        string[] labels = ["A", "B", "X", "Y"];
        var ft = new Typeface("Segoe UI");

        for (int i = 0; i < 4; i++)
        {
            var brush = states[i] ? Colors[i] : DimBrush;
            dc.DrawEllipse(brush, null, new WpfPoint(xs[i], cy), r, r);

            var text = new FormattedText(labels[i],
                System.Globalization.CultureInfo.CurrentCulture,
                WpfFlow.LeftToRight, ft, 8,
                System.Windows.Media.Brushes.White, 96);
            dc.DrawText(text, new WpfPoint(xs[i] - text.Width / 2, cy - text.Height / 2));
        }
    }
}
