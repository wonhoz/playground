namespace DeepDiff.Views;

public partial class ClipboardCompareView : UserControl
{
    private readonly MainWindow _main;
    private readonly TextDiffService _svc = new();

    private List<DiffLineVm> _vmLines = [];
    private List<int> _diffPositions = [];
    private int _diffNavIndex = -1;
    private bool _syncScrolling;
    private System.Windows.Threading.DispatcherTimer? _debounceTimer;

    public ClipboardCompareView(MainWindow main)
    {
        _main = main;
        InitializeComponent();
        Loaded += (_, _) => TxtLeft.Focus();
    }

    // ─── 실시간 업데이트 ───────────────────────────────────────

    private void TxtInput_Changed(object s, TextChangedEventArgs e)
    {
        if (ChkLiveUpdate.IsChecked != true) return;

        // 300ms 디바운스
        _debounceTimer?.Stop();
        _debounceTimer = new()
        {
            Interval = TimeSpan.FromMilliseconds(300)
        };
        _debounceTimer.Tick += (_, _) =>
        {
            _debounceTimer.Stop();
            RunCompare();
        };
        _debounceTimer.Start();
    }

    private void RunCompare()
    {
        bool ignoreWS = ChkIgnoreWS.IsChecked == true;
        var (lines, diffCount) = _svc.CompareText(TxtLeft.Text, TxtRight.Text, ignoreWS);

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
            FontSz           = 11
        }).ToList();

        DiffLeftList.ItemsSource  = _vmLines;
        DiffRightList.ItemsSource = _vmLines;

        TbStatus.Text  = $"총 {lines.Count}줄 비교됨";
        TbDiffInfo.Text = _diffPositions.Count > 0 ? $"차이 {_diffPositions.Count}개 구간" : "차이 없음";
    }

    // ─── 동기화 스크롤 ─────────────────────────────────────────

    private void DiffLeftScroll_ScrollChanged(object s, ScrollChangedEventArgs e)
    {
        if (_syncScrolling) return;
        _syncScrolling = true;
        DiffRightScroll.ScrollToVerticalOffset(e.VerticalOffset);
        _syncScrolling = false;
    }

    private void DiffRightScroll_ScrollChanged(object s, ScrollChangedEventArgs e)
    {
        if (_syncScrolling) return;
        _syncScrolling = true;
        DiffLeftScroll.ScrollToVerticalOffset(e.VerticalOffset);
        _syncScrolling = false;
    }

    // ─── 버튼들 ────────────────────────────────────────────────

    private void BtnPasteLeft_Click(object s, RoutedEventArgs e)
    {
        if (Clipboard.ContainsText()) TxtLeft.Text = Clipboard.GetText();
    }

    private void BtnPasteRight_Click(object s, RoutedEventArgs e)
    {
        if (Clipboard.ContainsText()) TxtRight.Text = Clipboard.GetText();
    }

    private void BtnSwap_Click(object s, RoutedEventArgs e)
    {
        (TxtLeft.Text, TxtRight.Text) = (TxtRight.Text, TxtLeft.Text);
        RunCompare();
    }

    private void BtnClear_Click(object s, RoutedEventArgs e)
    {
        TxtLeft.Clear();
        TxtRight.Clear();
        DiffLeftList.ItemsSource  = null;
        DiffRightList.ItemsSource = null;
        TbStatus.Text = "내용이 지워졌습니다.";
        TbDiffInfo.Text = "";
    }

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
        double offset = Math.Max(0, _diffPositions[navIdx] * 16 - DiffLeftScroll.ViewportHeight / 2);
        DiffLeftScroll.ScrollToVerticalOffset(offset);
        TbDiffInfo.Text = $"[{navIdx + 1}/{_diffPositions.Count}] 차이";
    }

    private void ChkOption_Changed(object s, RoutedEventArgs e)
    { if (IsLoaded) RunCompare(); }
}
