using Microsoft.Win32;
using System.Windows.Controls.Primitives;
using System.Windows.Threading;

namespace DeepDiff.Views;

/// <summary>인라인 문자 수준 하이라이트가 가능한 TextBlock</summary>
public class HighlightedTextBlock : TextBlock
{
    public static readonly DependencyProperty SegmentsProperty =
        DependencyProperty.Register(nameof(Segments), typeof(List<TextSegment>),
            typeof(HighlightedTextBlock), new PropertyMetadata(null, OnSegmentsChanged));

    public List<TextSegment>? Segments
    {
        get => (List<TextSegment>?)GetValue(SegmentsProperty);
        set => SetValue(SegmentsProperty, value);
    }

    private static readonly SolidColorBrush HighlightBg =
        new(Color.FromArgb(200, 120, 60, 0));  // 변경 강조

    private static readonly SolidColorBrush HighlightBgDel =
        new(Color.FromArgb(200, 120, 30, 30)); // 삭제 강조

    private static void OnSegmentsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not HighlightedTextBlock tb) return;
        tb.Inlines.Clear();
        if (e.NewValue is not List<TextSegment> segs) return;

        foreach (var seg in segs)
        {
            var run = new Run(seg.Text);
            if (seg.IsHighlighted)
                run.Background = HighlightBg;
            tb.Inlines.Add(run);
        }
    }
}

/// <summary>diff 뷰어에서 바인딩할 라인 뷰모델</summary>
public class DiffLineVm
{
    public string? LeftLineNumText  { get; init; }
    public string? RightLineNumText { get; init; }
    public List<TextSegment> LeftSegments  { get; init; } = [];
    public List<TextSegment> RightSegments { get; init; } = [];
    public LineStatus Status  { get; init; }
    public double FontSz      { get; init; } = 11;
    public TextWrapping WrapMode { get; init; } = TextWrapping.NoWrap;

    private static readonly SolidColorBrush TransBrush = new(Colors.Transparent);
    private static readonly SolidColorBrush DelBg   = new(Color.FromRgb(0x2A, 0x18, 0x18));
    private static readonly SolidColorBrush AddBg   = new(Color.FromRgb(0x18, 0x2A, 0x20));
    private static readonly SolidColorBrush ChgBg   = new(Color.FromRgb(0x2A, 0x23, 0x10));
    private static readonly SolidColorBrush DelBar  = new(Color.FromRgb(0xE0, 0x55, 0x55));
    private static readonly SolidColorBrush AddBar  = new(Color.FromRgb(0x3F, 0xC8, 0x78));
    private static readonly SolidColorBrush ChgBar  = new(Color.FromRgb(0xF0, 0xB0, 0x30));

    public SolidColorBrush LeftBg  => Status == LineStatus.LeftOnly  ? DelBg
                                    : Status == LineStatus.Changed    ? ChgBg
                                    : TransBrush;

    public SolidColorBrush RightBg => Status == LineStatus.RightOnly ? AddBg
                                    : Status == LineStatus.Changed    ? ChgBg
                                    : TransBrush;

    public SolidColorBrush StatusBarColor => Status switch
    {
        LineStatus.LeftOnly  => DelBar,
        LineStatus.RightOnly => AddBar,
        LineStatus.Changed   => ChgBar,
        _ => TransBrush
    };
}

public partial class TextCompareView : UserControl, MainWindow.ICloseable
{
    private readonly MainWindow _main;
    private readonly TextDiffService _svc = new();
    private readonly FileOperationService _fops = new();

    private List<AlignedDiffLine> _diffLines = [];
    private List<DiffLineVm> _vmLines = [];
    private int _diffNavIndex = -1;
    private List<int> _diffPositions = [];
    private bool _syncScrolling;
    private double _fontSize = 11;
    private TextWrapping _wrapMode = TextWrapping.NoWrap;

    // 편집 모드
    private bool _editMode;
    private bool _leftModified;
    private bool _rightModified;
    private DispatcherTimer? _debounce;
    private bool _editorUpdating; // 에디터 텍스트 설정 시 이벤트 억제

    public TextCompareView(MainWindow main, string? leftPath = null, string? rightPath = null)
    {
        _main = main;
        InitializeComponent();

        if (leftPath  != null) TxtLeftPath.Text  = leftPath;
        if (rightPath != null) TxtRightPath.Text = rightPath;

        Loaded += (_, _) =>
        {
            if (!string.IsNullOrEmpty(TxtLeftPath.Text) && !string.IsNullOrEmpty(TxtRightPath.Text))
                RunCompare();
        };
    }

    // ─── ICloseable ────────────────────────────────────────────

    public bool CanClose()
    {
        if (!_editMode || (!_leftModified && !_rightModified)) return true;

        var ans = MessageBox.Show(
            "편집 내용이 저장되지 않았습니다.\n저장하고 닫으시겠습니까?",
            "저장 확인", MessageBoxButton.YesNoCancel, MessageBoxImage.Question);

        if (ans == MessageBoxResult.Cancel) return false;
        if (ans == MessageBoxResult.Yes)
        {
            if (_leftModified)  SaveEditorContent(TxtLeftPath.Text, true);
            if (_rightModified) SaveEditorContent(TxtRightPath.Text, false);
        }
        return true;
    }

    private void SaveEditorContent(string path, bool isLeft)
    {
        if (string.IsNullOrEmpty(path)) return;
        var text = isLeft ? LeftEditor.Text : RightEditor.Text;
        _fops.SaveText(path, text);
    }

    // ─── 비교 실행 ─────────────────────────────────────────────

    private async void RunCompare()
    {
        string left  = TxtLeftPath.Text.Trim();
        string right = TxtRightPath.Text.Trim();

        TbStatus.Text = "비교 중...";
        bool ignoreWS   = ChkIgnoreWS.IsChecked   == true;
        bool ignoreCase = ChkIgnoreCase.IsChecked == true;

        (List<AlignedDiffLine> lines, int diffCount) = await Task.Run(() =>
        {
            if (File.Exists(left) && File.Exists(right))
                return _svc.Compare(left, right, ignoreWS);
            return (new List<AlignedDiffLine>(), 0);
        });

        _diffLines = lines;
        _diffPositions = lines.Select((l, i) => (l, i))
                              .Where(x => x.l.Status != LineStatus.Same)
                              .Select(x => x.i).ToList();
        _diffNavIndex = _diffPositions.Count > 0 ? 0 : -1;

        _vmLines = lines.Select(l => new DiffLineVm
        {
            LeftLineNumText  = l.LeftLineNumText,
            RightLineNumText = l.RightLineNumText,
            LeftSegments     = l.LeftSegments,
            RightSegments    = l.RightSegments,
            Status           = l.Status,
            FontSz           = _fontSize,
            WrapMode         = _wrapMode
        }).ToList();

        LeftList.ItemsSource  = _vmLines;
        RightList.ItemsSource = _vmLines;

        // 파일 정보 표시
        if (File.Exists(left))
        {
            var fi = new FileInfo(left);
            TbLeftInfo.Text = $"{fi.Name}  |  {fi.Length:N0} bytes  |  {fi.LastWriteTime:yyyy-MM-dd HH:mm}";
        }
        if (File.Exists(right))
        {
            var fi = new FileInfo(right);
            TbRightInfo.Text = $"{fi.Name}  |  {fi.Length:N0} bytes  |  {fi.LastWriteTime:yyyy-MM-dd HH:mm}";
        }

        int total = lines.Count;
        TbStatus.Text = $"총 {total}줄 · 차이 {diffCount}개 구간";
        TbDiffNav.Text = _diffPositions.Count > 0 ? $"[1/{_diffPositions.Count}]" : "[차이 없음]";

        if (_diffPositions.Count > 0) ScrollToDiff(0);
    }

    // ─── 동기화 스크롤 ─────────────────────────────────────────

    private void LeftScroll_ScrollChanged(object s, ScrollChangedEventArgs e)
    {
        if (_syncScrolling) return;
        _syncScrolling = true;
        RightScroll.ScrollToVerticalOffset(e.VerticalOffset);
        RightScroll.ScrollToHorizontalOffset(e.HorizontalOffset);
        _syncScrolling = false;
    }

    private void RightScroll_ScrollChanged(object s, ScrollChangedEventArgs e)
    {
        if (_syncScrolling) return;
        _syncScrolling = true;
        LeftScroll.ScrollToVerticalOffset(e.VerticalOffset);
        LeftScroll.ScrollToHorizontalOffset(e.HorizontalOffset);
        _syncScrolling = false;
    }

    // ─── diff 네비게이션 ───────────────────────────────────────

    private void BtnNextDiff_Click(object s, RoutedEventArgs e)
    {
        if (_diffPositions.Count == 0) return;
        _diffNavIndex = (_diffNavIndex + 1) % _diffPositions.Count;
        ScrollToDiff(_diffNavIndex);
    }

    private void BtnPrevDiff_Click(object s, RoutedEventArgs e)
    {
        if (_diffPositions.Count == 0) return;
        _diffNavIndex = (_diffNavIndex - 1 + _diffPositions.Count) % _diffPositions.Count;
        ScrollToDiff(_diffNavIndex);
    }

    private void ScrollToDiff(int navIdx)
    {
        if (navIdx < 0 || navIdx >= _diffPositions.Count) return;
        int lineIdx = _diffPositions[navIdx];

        // 아이템 높이 추정(18px)으로 스크롤
        double offset = Math.Max(0, lineIdx * 18 - LeftScroll.ViewportHeight / 2);
        LeftScroll.ScrollToVerticalOffset(offset);
        TbDiffNav.Text = $"[{navIdx + 1}/{_diffPositions.Count}]";
    }

    // ─── 경로 선택 ─────────────────────────────────────────────

    private void BtnLeftBrowse_Click(object s, RoutedEventArgs e)
    {
        var path = PickFile(TxtLeftPath.Text);
        if (path != null) { TxtLeftPath.Text = path; RunCompare(); }
    }

    private void BtnRightBrowse_Click(object s, RoutedEventArgs e)
    {
        var path = PickFile(TxtRightPath.Text);
        if (path != null) { TxtRightPath.Text = path; RunCompare(); }
    }

    private void BtnSwap_Click(object s, RoutedEventArgs e)
    {
        (TxtLeftPath.Text, TxtRightPath.Text) = (TxtRightPath.Text, TxtLeftPath.Text);
        if (!string.IsNullOrEmpty(TxtLeftPath.Text)) RunCompare();
    }

    private void BtnCompare_Click(object s, RoutedEventArgs e) => RunCompare();
    private void TxtPath_KeyDown(object s, KeyEventArgs e) { if (e.Key == Key.Enter) RunCompare(); }

    private static string? PickFile(string? initial)
    {
        var dlg = new OpenFileDialog { Title = "파일 선택", Filter = "모든 파일|*.*|텍스트|*.txt;*.cs;*.json;*.xml;*.md" };
        if (!string.IsNullOrEmpty(initial) && File.Exists(initial))
            dlg.InitialDirectory = Path.GetDirectoryName(initial);
        return dlg.ShowDialog() == true ? dlg.FileName : null;
    }

    // ─── 복사 작업 ─────────────────────────────────────────────

    private void BtnCopyLR_Click(object s, RoutedEventArgs e) => CopyFile(TxtLeftPath.Text, TxtRightPath.Text);
    private void BtnCopyRL_Click(object s, RoutedEventArgs e) => CopyFile(TxtRightPath.Text, TxtLeftPath.Text);

    private void CopyFile(string src, string dst)
    {
        if (!File.Exists(src)) { MessageBox.Show("원본 파일이 없습니다.", "오류"); return; }
        if (string.IsNullOrEmpty(dst)) { MessageBox.Show("대상 경로를 입력하세요.", "오류"); return; }
        var r = _fops.CopyFile(src, dst);
        if (r.Success) RunCompare();
        else MessageBox.Show(r.Error, "복사 오류", MessageBoxButton.OK, MessageBoxImage.Error);
    }

    private void BtnSaveLeft_Click(object s, RoutedEventArgs e)
    {
        if (_editMode) { SaveEditorContent(TxtLeftPath.Text, true); _leftModified = false; TbStatus.Text = $"저장 완료: {Path.GetFileName(TxtLeftPath.Text)}"; }
        else SaveEdited(TxtLeftPath.Text, true);
    }
    private void BtnSaveRight_Click(object s, RoutedEventArgs e)
    {
        if (_editMode) { SaveEditorContent(TxtRightPath.Text, false); _rightModified = false; TbStatus.Text = $"저장 완료: {Path.GetFileName(TxtRightPath.Text)}"; }
        else SaveEdited(TxtRightPath.Text, false);
    }

    private void SaveEdited(string path, bool isLeft)
    {
        if (string.IsNullOrEmpty(path)) return;
        var lines = _diffLines
            .Where(l => isLeft ? l.LeftLineNum.HasValue : l.RightLineNum.HasValue)
            .OrderBy(l => isLeft ? l.LeftLineNum : l.RightLineNum)
            .Select(l => isLeft ? l.LeftText : l.RightText);
        var r = _fops.SaveText(path, string.Join(Environment.NewLine, lines));
        if (r.Success) { RunCompare(); TbStatus.Text = $"저장 완료: {Path.GetFileName(path)}"; }
        else MessageBox.Show(r.Error, "저장 오류", MessageBoxButton.OK, MessageBoxImage.Error);
    }

    // ─── 편집 모드 ────────────────────────────────────────────

    private void BtnEditMode_Changed(object s, RoutedEventArgs e)
    {
        if (!IsLoaded) return;
        _editMode = BtnEditMode.IsChecked == true;

        if (_editMode)
        {
            // diff → edit: 에디터에 현재 파일 내용 로드
            EditPanel.Visibility = Visibility.Visible;
            DiffPanel.Visibility = Visibility.Collapsed;
            LoadEditors();
        }
        else
        {
            // edit → diff: 에디터 내용으로 diff 업데이트
            EditPanel.Visibility = Visibility.Collapsed;
            DiffPanel.Visibility = Visibility.Visible;
            RunCompareFromEditors();
        }
    }

    private void LoadEditors()
    {
        _editorUpdating = true;
        try
        {
            LeftEditor.Text  = File.Exists(TxtLeftPath.Text.Trim())
                ? File.ReadAllText(TxtLeftPath.Text.Trim()) : "";
            RightEditor.Text = File.Exists(TxtRightPath.Text.Trim())
                ? File.ReadAllText(TxtRightPath.Text.Trim()) : "";
        }
        finally { _editorUpdating = false; }
        _leftModified = false;
        _rightModified = false;
    }

    private void Editor_TextChanged(object s, TextChangedEventArgs e)
    {
        if (_editorUpdating) return;
        if (s == LeftEditor)  _leftModified  = true;
        if (s == RightEditor) _rightModified = true;

        // 300ms 디바운스
        if (_debounce == null)
        {
            _debounce = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
            _debounce.Tick += (_, _) => { _debounce.Stop(); RunCompareFromEditors(); };
        }
        _debounce.Stop();
        _debounce.Start();
    }

    private void RunCompareFromEditors()
    {
        bool ignoreWS   = ChkIgnoreWS.IsChecked   == true;
        var (lines, diffCount) = _svc.CompareText(LeftEditor.Text, RightEditor.Text, ignoreWS);
        _diffLines = lines;
        _diffPositions = lines.Select((l, i) => (l, i))
                              .Where(x => x.l.Status != LineStatus.Same)
                              .Select(x => x.i).ToList();
        _diffNavIndex = _diffPositions.Count > 0 ? 0 : -1;
        _vmLines = lines.Select(l => new DiffLineVm
        {
            LeftLineNumText  = l.LeftLineNumText,
            RightLineNumText = l.RightLineNumText,
            LeftSegments     = l.LeftSegments,
            RightSegments    = l.RightSegments,
            Status           = l.Status,
            FontSz           = _fontSize,
            WrapMode         = _wrapMode
        }).ToList();
        LeftList.ItemsSource  = _vmLines;
        RightList.ItemsSource = _vmLines;
        int total = lines.Count;
        TbStatus.Text = $"총 {total}줄 · 차이 {diffCount}개 구간  [편집 모드]";
        TbDiffNav.Text = _diffPositions.Count > 0 ? $"[1/{_diffPositions.Count}]" : "[차이 없음]";
    }

    // ─── 옵션 ─────────────────────────────────────────────────

    private void ChkOption_Changed(object s, RoutedEventArgs e) { if (IsLoaded) RunCompare(); }
    private void ChkWrap_Changed(object s, RoutedEventArgs e)
    {
        _wrapMode = ChkWrap.IsChecked == true ? TextWrapping.Wrap : TextWrapping.NoWrap;
        if (IsLoaded) RunCompare();
    }

    private void CbFontSize_Changed(object s, SelectionChangedEventArgs e)
    {
        if (!IsLoaded) return;
        if (CbFontSize.SelectedItem is ComboBoxItem ci && ci.Tag is string tag
            && double.TryParse(tag, out double sz))
        {
            _fontSize = sz;
            RunCompare();
        }
    }

    // ─── 키 단축키 ────────────────────────────────────────────

    private void UserControl_KeyDown(object s, KeyEventArgs e)
    {
        if (e.Key == Key.F3 && e.KeyboardDevice.Modifiers == ModifierKeys.None)
        { BtnNextDiff_Click(s, e); e.Handled = true; }
        else if (e.Key == Key.F3 && e.KeyboardDevice.Modifiers == ModifierKeys.Shift)
        { BtnPrevDiff_Click(s, e); e.Handled = true; }
        else if (e.Key == Key.S && e.KeyboardDevice.Modifiers == ModifierKeys.Control)
        { BtnSaveLeft_Click(s, e); e.Handled = true; }
        else if (e.Key == Key.W && e.KeyboardDevice.Modifiers == ModifierKeys.Control)
        {
            ChkIgnoreWS.IsChecked = !(ChkIgnoreWS.IsChecked == true);
            e.Handled = true;
        }
    }
}
