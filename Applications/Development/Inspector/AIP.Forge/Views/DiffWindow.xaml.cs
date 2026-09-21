using System.Windows.Documents;
using AIPForge.Services;

namespace AIPForge.Views;

/// <summary>디스크 원본 ↔ 현재 편집본 줄 단위 비교</summary>
public partial class DiffWindow : Window
{
    private readonly List<DiffLine> _lines;

    /// <summary>사용자가 이 창에서 "저장"을 눌렀는지</summary>
    public bool SaveRequested { get; private set; }

    private static readonly Brush AddedBg = Frozen(0x1E, 0x3A, 0x26);
    private static readonly Brush RemovedBg = Frozen(0x44, 0x22, 0x26);
    private static readonly Brush GapFg = Frozen(0x6A, 0x6A, 0x80);
    private static readonly Brush LineNoFg = Frozen(0x5E, 0x5E, 0x72);

    public DiffWindow(string oldText, string newText, bool canSave)
    {
        InitializeComponent();
        DarkTitle.Apply(this);
        _lines = LineDiff.Compute(oldText, newText);
        var added = _lines.Count(l => l.Kind == DiffKind.Added);
        var removed = _lines.Count(l => l.Kind == DiffKind.Removed);
        TxtSummary.Text = added + removed == 0
            ? "변경 사항이 없습니다."
            : $"추가 {added}줄 · 삭제 {removed}줄";
        BtnSave.Visibility = canSave && added + removed > 0 ? Visibility.Visible : Visibility.Collapsed;
        Render();
    }

    private static Brush Frozen(byte r, byte g, byte b)
    {
        var brush = new SolidColorBrush(Color.FromRgb(r, g, b));
        brush.Freeze();
        return brush;
    }

    private void ChkAll_Changed(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded) return;
        Render();
    }

    private void Render()
    {
        IEnumerable<DiffLine?> source = ChkAll.IsChecked == true ? _lines.Cast<DiffLine?>() : LineDiff.WithContext(_lines);
        LstDiff.Items.Clear();
        foreach (var line in source) LstDiff.Items.Add(MakeRow(line));
    }

    private static FrameworkElement MakeRow(DiffLine? line)
    {
        if (line == null)
            return new TextBlock { Text = "   ⋯", Foreground = GapFg, Padding = new Thickness(4, 2, 4, 2) };

        var (mark, bg) = line.Kind switch
        {
            DiffKind.Added => ("+", AddedBg),
            DiffKind.Removed => ("-", RemovedBg),
            _ => (" ", Brushes.Transparent),
        };
        var tb = new TextBlock { Background = bg, Padding = new Thickness(4, 1, 12, 1) };
        tb.Inlines.Add(new Run($"{line.OldNo,6} {line.NewNo,6} ") { Foreground = LineNoFg });
        tb.Inlines.Add(new Run($"{mark} {line.Text}"));
        return tb;
    }

    private void BtnSave_Click(object sender, RoutedEventArgs e)
    {
        SaveRequested = true;
        Close();
    }
}
