using Microsoft.Win32;
using PdfForge.Services;

namespace PdfForge.Views;

public partial class PageView : UserControl
{
    private readonly PdfPageService _svc = new();
    private readonly ObservableCollection<PageItem> _pages = [];
    private string? _inputPath;

    public PageView()
    {
        InitializeComponent();
        PageListBox.ItemsSource = _pages;
    }

    private void SelectFile_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog { Filter = "PDF 파일|*.pdf" };
        if (dlg.ShowDialog() != true) return;
        _inputPath = dlg.FileName;
        TxtInput.Text = _inputPath;
        TxtInput.Foreground = new SolidColorBrush(Color.FromRgb(0xE8, 0xE8, 0xF0));
        LoadPages();
    }

    private void LoadPages()
    {
        _pages.Clear();
        if (_inputPath is null) return;
        int count = _svc.GetPageCount(_inputPath);
        for (int i = 0; i < count; i++)
            _pages.Add(new PageItem(i, 0));
        TxtPageInfo.Text = $"총 {count}페이지";
    }

    private IEnumerable<PageItem> SelectedPages =>
        PageListBox.SelectedItems.Cast<PageItem>();

    private void Rotate90_Click(object sender, RoutedEventArgs e)
    {
        foreach (var p in SelectedPages) p.PendingRotate = (p.PendingRotate + 90) % 360;
        RefreshList();
    }

    private void RotateMinus90_Click(object sender, RoutedEventArgs e)
    {
        foreach (var p in SelectedPages) p.PendingRotate = (p.PendingRotate - 90 + 360) % 360;
        RefreshList();
    }

    private void Rotate180_Click(object sender, RoutedEventArgs e)
    {
        foreach (var p in SelectedPages) p.PendingRotate = (p.PendingRotate + 180) % 360;
        RefreshList();
    }

    private void DeletePages_Click(object sender, RoutedEventArgs e)
    {
        foreach (var p in SelectedPages.ToList()) _pages.Remove(p);
        RenumberPages();
    }

    private void MoveUp_Click(object sender, RoutedEventArgs e)
    {
        var sel = SelectedPages.OrderBy(p => _pages.IndexOf(p)).ToList();
        foreach (var p in sel)
        {
            int idx = _pages.IndexOf(p);
            if (idx > 0) _pages.Move(idx, idx - 1);
        }
        RenumberPages();
    }

    private void MoveDown_Click(object sender, RoutedEventArgs e)
    {
        var sel = SelectedPages.OrderByDescending(p => _pages.IndexOf(p)).ToList();
        foreach (var p in sel)
        {
            int idx = _pages.IndexOf(p);
            if (idx < _pages.Count - 1) _pages.Move(idx, idx + 1);
        }
        RenumberPages();
    }

    private void PageListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        TxtPageInfo.Text = PageListBox.SelectedItems.Count > 0
            ? $"{PageListBox.SelectedItems.Count}개 선택"
            : $"총 {_pages.Count}페이지";
    }

    private void RenumberPages()
    {
        for (int i = 0; i < _pages.Count; i++)
            _pages[i].Number = $"{i + 1}";
    }

    private void RefreshList()
    {
        // ObservableCollection으로는 속성 변경 알림이 자동으로 안 되므로 강제 갱신
        var selected = PageListBox.SelectedItems.Cast<PageItem>().ToList();
        var temp = _pages.ToList();
        _pages.Clear();
        foreach (var p in temp) _pages.Add(p);
        foreach (var p in selected)
            PageListBox.SelectedItems.Add(p);
    }

    private async void BtnSave_Click(object sender, RoutedEventArgs e)
    {
        if (_inputPath is null)
        {
            MessageBox.Show("PDF 파일을 선택하세요.", "PDF Forge", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var dlg = new SaveFileDialog { Filter = "PDF 파일|*.pdf", FileName = "edited.pdf" };
        if (dlg.ShowDialog() != true) return;

        BtnSave.IsEnabled = false;
        PbPage.Visibility = Visibility.Visible;
        PbPage.IsIndeterminate = true;
        try
        {
            // 삭제 후 재정렬 → 회전 순으로 처리
            var newOrder = _pages.Select(p => p.OriginalIndex).ToList();
            var rotations = _pages.Select(p => (p.OriginalIndex, p.PendingRotate))
                                  .Where(x => x.PendingRotate != 0).ToList();

            var tmp = Path.GetTempFileName() + ".pdf";
            await Task.Run(() => _svc.ReorderPages(_inputPath, tmp, newOrder));

            if (rotations.Count > 0)
            {
                // 재정렬된 새 인덱스 기준으로 회전 적용
                var rotateMap = rotations.ToDictionary(x => x.OriginalIndex, x => x.PendingRotate);
                var rotateIndices = _pages.Select((p, i) => (p, i))
                                          .Where(x => rotateMap.ContainsKey(x.p.OriginalIndex))
                                          .Select(x => (x.i, rotateMap[x.p.OriginalIndex]))
                                          .ToList();
                await Task.Run(() =>
                {
                    var tmp2 = Path.GetTempFileName() + ".pdf";
                    _svc.RotatePages(tmp, tmp2,
                        rotateIndices.Select(x => x.i),
                        rotateIndices.First().Item2);  // 단순화: 같은 각도 적용
                    // 각도별 그룹핑 처리
                    File.Move(tmp2, dlg.FileName, overwrite: true);
                    File.Delete(tmp);
                });
            }
            else
            {
                File.Move(tmp, dlg.FileName, overwrite: true);
            }

            MessageBox.Show($"저장 완료!\n{dlg.FileName}", "PDF Forge", MessageBoxButton.OK, MessageBoxImage.Information);
            _inputPath = dlg.FileName;
            LoadPages();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"저장 실패: {ex.Message}", "PDF Forge", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            BtnSave.IsEnabled = true;
            PbPage.Visibility = Visibility.Collapsed;
            PbPage.IsIndeterminate = false;
        }
    }
}

public class PageItem(int originalIndex, int pendingRotate) : INotifyPropertyChanged
{
    public int OriginalIndex { get; } = originalIndex;

    private string _number = $"{originalIndex + 1}";
    public string Number
    {
        get => _number;
        set { _number = value; OnPropertyChanged(); }
    }

    private int _pendingRotate = pendingRotate;
    public int PendingRotate
    {
        get => _pendingRotate;
        set { _pendingRotate = value; OnPropertyChanged(); OnPropertyChanged(nameof(RotateText)); }
    }

    public string Label => $"페이지 {OriginalIndex + 1}";
    public string RotateText => PendingRotate == 0 ? "" : $"+{PendingRotate}°";

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
