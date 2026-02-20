using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace TextForge.Views;

public partial class RegexView : UserControl
{
    // 매칭 하이라이트 색상
    private static readonly SolidColorBrush HighlightBg =
        new(Color.FromArgb(180, 255, 160, 0));
    private static readonly SolidColorBrush HighlightFg =
        new(Color.FromArgb(255, 17, 17, 34));

    public RegexView() => InitializeComponent();

    private void Pattern_TextChanged(object sender, TextChangedEventArgs e) => RunMatch();
    private void Flag_Changed(object sender, RoutedEventArgs e) => RunMatch();

    private void RunMatch()
    {
        var pattern = PatternBox.Text;

        // 기존 하이라이트 제거 후 플레인 텍스트 복원
        var rawText = GetRichTextBoxText();

        // TestInput 내용 변경 이벤트 연결 (최초 1회)
        TestInput.TextChanged -= TestInput_TextChanged;
        TestInput.TextChanged += TestInput_TextChanged;

        if (string.IsNullOrEmpty(pattern))
        {
            SetPlainText(rawText);
            MatchList.Items.Clear();
            MatchCountText.Text = string.Empty;
            StatusText.Text = string.Empty;
            return;
        }

        try
        {
            var options = RegexOptions.None;
            if (FlagIgnoreCase.IsChecked == true)  options |= RegexOptions.IgnoreCase;
            if (FlagMultiline.IsChecked  == true)  options |= RegexOptions.Multiline;
            if (FlagSingleline.IsChecked == true)  options |= RegexOptions.Singleline;

            var regex   = new Regex(pattern, options);
            var matches = regex.Matches(rawText);

            MatchList.Items.Clear();
            foreach (Match m in matches)
            {
                MatchList.Items.Add(
                    $"[{m.Index,4}:{m.Index + m.Length,4}]  {Truncate(m.Value, 80)}");
            }

            MatchCountText.Text = matches.Count > 0
                ? $"{matches.Count}개 매치"
                : "매치 없음";

            MatchCountText.Foreground = matches.Count > 0
                ? new SolidColorBrush(Colors.Orange)
                : new SolidColorBrush(Color.FromRgb(0x88, 0x88, 0x88));

            ApplyHighlights(rawText, matches);
            StatusText.Text = string.Empty;
        }
        catch (RegexParseException ex)
        {
            SetPlainText(rawText);
            MatchList.Items.Clear();
            MatchCountText.Text = string.Empty;
            StatusText.Text = $"✕ 정규식 오류: {ex.Error}";
            StatusText.Foreground = new SolidColorBrush(Color.FromRgb(0xEF, 0x9A, 0x9A));
        }
    }

    private void TestInput_TextChanged(object sender, TextChangedEventArgs e) => RunMatch();

    private void ApplyHighlights(string text, MatchCollection matches)
    {
        // 기존 내용 클리어 후 재구성
        TestInput.TextChanged -= TestInput_TextChanged;

        var doc = TestInput.Document;
        doc.Blocks.Clear();

        var para = new Paragraph { Margin = new Thickness(0) };

        int cursor = 0;
        foreach (Match m in matches)
        {
            // 매치 전 일반 텍스트
            if (m.Index > cursor)
                para.Inlines.Add(new Run(text[cursor..m.Index])
                    { Foreground = new SolidColorBrush(Color.FromRgb(0xE0, 0xE0, 0xE0)) });

            // 하이라이트된 매치
            para.Inlines.Add(new Run(m.Value)
            {
                Background = HighlightBg,
                Foreground = HighlightFg,
                FontWeight = FontWeights.Bold
            });

            cursor = m.Index + m.Length;
        }

        // 나머지 텍스트
        if (cursor < text.Length)
            para.Inlines.Add(new Run(text[cursor..])
                { Foreground = new SolidColorBrush(Color.FromRgb(0xE0, 0xE0, 0xE0)) });

        doc.Blocks.Add(para);

        TestInput.TextChanged += TestInput_TextChanged;
    }

    private string GetRichTextBoxText()
    {
        return new TextRange(
            TestInput.Document.ContentStart,
            TestInput.Document.ContentEnd).Text.TrimEnd('\r', '\n');
    }

    private void SetPlainText(string text)
    {
        TestInput.TextChanged -= TestInput_TextChanged;

        var doc  = TestInput.Document;
        doc.Blocks.Clear();
        var para = new Paragraph(new Run(text)
            { Foreground = new SolidColorBrush(Color.FromRgb(0xE0, 0xE0, 0xE0)) })
        { Margin = new Thickness(0) };
        doc.Blocks.Add(para);

        TestInput.TextChanged += TestInput_TextChanged;
    }

    private static string Truncate(string s, int maxLen)
        => s.Length <= maxLen ? s : s[..maxLen] + "…";
}
