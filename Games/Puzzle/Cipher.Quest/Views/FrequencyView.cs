using System.Globalization;
using System.Windows.Media;
using CipherQuest.Services;

namespace CipherQuest.Views;

/// <summary>글자 빈도 히스토그램 (암호문 vs 영어 평균)</summary>
public class FrequencyView : System.Windows.FrameworkElement
{
    private double[] _freq = new double[26];

    private static readonly SolidColorBrush BrBar  = new(WpfColor.FromRgb(0x21, 0x96, 0xF3));
    private static readonly SolidColorBrush BrBg   = new(WpfColor.FromRgb(0x12, 0x1E, 0x2E));
    private static readonly SolidColorBrush BrLbl  = new(WpfColor.FromRgb(0x88, 0x88, 0x88));
    private static readonly Pen             PenEng = new(new SolidColorBrush(WpfColor.FromArgb(160, 0xFF, 0xC1, 0x07)), 1.5);
    private static readonly Typeface        TfSeg  = new("Segoe UI");

    private const double W = 22, MaxH = 90, BaseY = 100, TotalW = 26 * W + 4;

    protected override System.Windows.Size MeasureOverride(System.Windows.Size _)
        => new(TotalW, BaseY + 16);

    public void Update(string cipherText)
    {
        _freq = CipherEngine.LetterFrequency(cipherText);
        InvalidateVisual();
    }

    protected override void OnRender(DrawingContext dc)
    {
        dc.DrawRectangle(BrBg, null, new System.Windows.Rect(0, 0, TotalW, BaseY + 16));

        for (int i = 0; i < 26; i++)
        {
            double x = 2 + i * W;

            // 암호문 빈도 (파란색 바)
            double barH = Math.Min(_freq[i] / 15.0 * MaxH, MaxH);
            dc.DrawRectangle(BrBar, null, new System.Windows.Rect(x + 2, BaseY - barH, W - 4, barH));

            // 영어 평균 (주황색 수평선)
            double engH = CipherEngine.EnglishFreq[i] / 15.0 * MaxH;
            dc.DrawLine(PenEng,
                new System.Windows.Point(x + 1, BaseY - engH),
                new System.Windows.Point(x + W - 1, BaseY - engH));

            // 글자 레이블
            var ft = new FormattedText(((char)('A' + i)).ToString(),
                CultureInfo.InvariantCulture, System.Windows.FlowDirection.LeftToRight,
                TfSeg, 9, BrLbl, 96);
            dc.DrawText(ft, new System.Windows.Point(x + (W - ft.Width) / 2, BaseY + 2));
        }
    }
}
