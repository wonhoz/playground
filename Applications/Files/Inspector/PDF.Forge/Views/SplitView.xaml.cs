using Microsoft.Win32;
using PdfForge.Services;
using SWF = System.Windows.Forms;

namespace PdfForge.Views;

public partial class SplitView : UserControl
{
    private readonly PdfSplitService _svc = new();
    private readonly PdfPageService _pageSvc = new();
    private readonly ObservableCollection<RangeItem> _ranges = [];
    private string? _inputPath;

    public SplitView()
    {
        InitializeComponent();
        RangeListBox.ItemsSource = _ranges;
    }

    private void SelectFile_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog { Filter = "PDF 파일|*.pdf" };
        if (dlg.ShowDialog() != true) return;
        _inputPath = dlg.FileName;
        TxtInput.Text = _inputPath;
        TxtInput.Foreground = new SolidColorBrush(Color.FromRgb(0xE8, 0xE8, 0xF0));
        int count = _pageSvc.GetPageCount(_inputPath);
        TxtPageCount.Text = $"총 {count}페이지";
    }

    private void Mode_Changed(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded) return;
        RangePanel.Visibility = RbRanges.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
    }

    private void AddRange_Click(object sender, RoutedEventArgs e)
    {
        var raw = TxtRanges.Text.Trim();
        if (raw is "예: 1-3, 5-7, 9" or "") return;
        foreach (var part in raw.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            var seg = part.Split('-');
            if (seg.Length == 2 && int.TryParse(seg[0], out int f) && int.TryParse(seg[1], out int t) && f <= t)
                _ranges.Add(new RangeItem(f, t));
        }
        TxtRanges.Text = "";
    }

    private void RemoveRange_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: RangeItem item })
            _ranges.Remove(item);
    }

    private async void BtnSplit_Click(object sender, RoutedEventArgs e)
    {
        if (_inputPath is null)
        {
            MessageBox.Show("PDF 파일을 선택하세요.", "PDF Forge", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var dialog = new SWF.FolderBrowserDialog { Description = "저장 폴더를 선택하세요" };
        if (dialog.ShowDialog() != SWF.DialogResult.OK) return;
        var outDir = dialog.SelectedPath;

        BtnSplit.IsEnabled = false;
        PbSplit.Visibility = Visibility.Visible;
        PbSplit.IsIndeterminate = true;
        try
        {
            List<string> result;
            if (RbEachPage.IsChecked == true)
                result = await Task.Run(() => _svc.SplitEachPage(_inputPath, outDir));
            else
            {
                if (_ranges.Count == 0)
                {
                    MessageBox.Show("범위를 1개 이상 추가하세요.", "PDF Forge", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }
                var ranges = _ranges.Select(r => (r.From, r.To)).ToList();
                result = await Task.Run(() => _svc.SplitByRanges(_inputPath, ranges, outDir));
            }
            MessageBox.Show($"분리 완료! {result.Count}개 파일 저장됨.\n{outDir}", "PDF Forge",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"분리 실패: {ex.Message}", "PDF Forge", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            BtnSplit.IsEnabled = true;
            PbSplit.Visibility = Visibility.Collapsed;
            PbSplit.IsIndeterminate = false;
        }
    }
}

public record RangeItem(int From, int To)
{
    public string Label => $"페이지 {From} ~ {To}";
}
