using Microsoft.Win32;

namespace DeepDiff.Views;

public class HexRowVm
{
    private static readonly SolidColorBrush TransBrush = new(Colors.Transparent);
    private static readonly SolidColorBrush DiffBg     = new(Color.FromRgb(0x2A, 0x18, 0x18));
    private static readonly SolidColorBrush FgNormal   = new(Color.FromRgb(0xE8, 0xE8, 0xF0));
    private static readonly SolidColorBrush FgDiff     = new(Color.FromRgb(0xF0, 0xB0, 0x30));
    private static readonly SolidColorBrush FgAddress  = new(Color.FromRgb(0x55, 0x55, 0x6A));

    public required HexDiffRow Data { get; init; }

    private bool AnyDiff => Data.BytesDiffer.Any(d => d);

    public SolidColorBrush RowBg  => AnyDiff ? DiffBg : TransBrush;
    public SolidColorBrush LeftFg  => AnyDiff ? FgDiff : FgNormal;
    public SolidColorBrush RightFg => AnyDiff ? FgDiff : FgNormal;

    public string LeftDisplay  => FormatRow(Data.LeftBytes,  Data.Address);
    public string RightDisplay => FormatRow(Data.RightBytes, Data.Address);

    private string FormatRow(byte[] bytes, long addr)
    {
        string hex   = HexDiffService.FormatHex(bytes, long.MaxValue, 0);
        string ascii = HexDiffService.FormatAscii(bytes, long.MaxValue, 0);
        return $"  {addr:X8}  {hex}   {ascii}  ";
    }
}

public partial class HexCompareView : UserControl
{
    private readonly MainWindow _main;
    private readonly HexDiffService _svc = new();
    private bool _syncScrolling;

    public HexCompareView(MainWindow main, string? leftPath = null, string? rightPath = null)
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

    private async void RunCompare()
    {
        string left  = TxtLeftPath.Text.Trim();
        string right = TxtRightPath.Text.Trim();
        TbStatus.Text = "로드 중...";

        var result = await Task.Run(() => _svc.Compare(left, right));

        var vms = result.Rows.Select(r => new HexRowVm { Data = r }).ToList();
        LeftHex.ItemsSource  = vms;
        RightHex.ItemsSource = vms;

        TbLeftSize.Text  = $"왼쪽: {result.LeftSize:N0} bytes";
        TbRightSize.Text = $"오른쪽: {result.RightSize:N0} bytes";
        TbDiffRows.Text  = $"차이 행: {result.DiffRows:N0}";
        TbStatus.Text    = $"{result.Rows.Count:N0}행 · {HexDiffService.BytesPerRow}바이트/행";

        if (result.LeftSize > HexDiffService.MaxRows * HexDiffService.BytesPerRow)
            TbStatus.Text += $" (최대 {HexDiffService.MaxRows * HexDiffService.BytesPerRow / 1024:N0}KB까지 표시)";
    }

    private void LeftScroll_ScrollChanged(object s, ScrollChangedEventArgs e)
    {
        if (_syncScrolling) return;
        _syncScrolling = true;
        RightScroll.ScrollToVerticalOffset(e.VerticalOffset);
        _syncScrolling = false;
    }

    private void RightScroll_ScrollChanged(object s, ScrollChangedEventArgs e)
    {
        if (_syncScrolling) return;
        _syncScrolling = true;
        LeftScroll.ScrollToVerticalOffset(e.VerticalOffset);
        _syncScrolling = false;
    }

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
        RunCompare();
    }

    private void BtnCompare_Click(object s, RoutedEventArgs e) => RunCompare();
    private void TxtPath_KeyDown(object s, KeyEventArgs e) { if (e.Key == Key.Enter) RunCompare(); }

    private static string? PickFile(string? initial)
    {
        var dlg = new OpenFileDialog { Title = "파일 선택", Filter = "모든 파일|*.*" };
        if (!string.IsNullOrEmpty(initial) && File.Exists(initial))
            dlg.InitialDirectory = Path.GetDirectoryName(initial);
        return dlg.ShowDialog() == true ? dlg.FileName : null;
    }
}
