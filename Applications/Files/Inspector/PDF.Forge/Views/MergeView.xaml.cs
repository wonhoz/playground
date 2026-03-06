using Microsoft.Win32;
using PdfForge.Services;

namespace PdfForge.Views;

public partial class MergeView : UserControl
{
    private readonly PdfMergeService _svc = new();
    private readonly ObservableCollection<PdfFileItem> _items = [];

    public MergeView()
    {
        InitializeComponent();
        FileListBox.ItemsSource = _items;
        _items.CollectionChanged += (_, _) =>
            EmptyHint.Visibility = _items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void AddFiles_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog { Filter = "PDF 파일|*.pdf", Multiselect = true };
        if (dlg.ShowDialog() != true) return;
        foreach (var f in dlg.FileNames)
            _items.Add(new PdfFileItem(f));
    }

    private void RemoveSelected_Click(object sender, RoutedEventArgs e)
    {
        foreach (var item in FileListBox.SelectedItems.Cast<PdfFileItem>().ToList())
            _items.Remove(item);
    }

    private void ClearAll_Click(object sender, RoutedEventArgs e) => _items.Clear();

    private void MoveUp_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: PdfFileItem item })
        {
            int idx = _items.IndexOf(item);
            if (idx > 0) _items.Move(idx, idx - 1);
        }
    }

    private void MoveDown_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: PdfFileItem item })
        {
            int idx = _items.IndexOf(item);
            if (idx < _items.Count - 1) _items.Move(idx, idx + 1);
        }
    }

    private void FileList_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private void FileList_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(DataFormats.FileDrop) is not string[] files) return;
        foreach (var f in files.Where(f => f.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase)))
            _items.Add(new PdfFileItem(f));
    }

    private async void BtnMerge_Click(object sender, RoutedEventArgs e)
    {
        if (_items.Count < 2)
        {
            MessageBox.Show("PDF 파일을 2개 이상 추가하세요.", "PDF Forge", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        var dlg = new SaveFileDialog { Filter = "PDF 파일|*.pdf", FileName = "merged.pdf" };
        if (dlg.ShowDialog() != true) return;

        BtnMerge.IsEnabled = false;
        PbMerge.Visibility = Visibility.Visible;
        PbMerge.IsIndeterminate = true;
        try
        {
            var paths = _items.Select(i => i.Path).ToList();
            await Task.Run(() => _svc.Merge(paths, dlg.FileName));
            MessageBox.Show($"병합 완료!\n{dlg.FileName}", "PDF Forge", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"병합 실패: {ex.Message}", "PDF Forge", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            BtnMerge.IsEnabled = true;
            PbMerge.Visibility = Visibility.Collapsed;
            PbMerge.IsIndeterminate = false;
        }
    }
}

public class PdfFileItem(string path)
{
    public string Path { get; } = path;
    public string Name { get; } = System.IO.Path.GetFileName(path);
    public string SizeText { get; } = FormatSize(new FileInfo(path).Length);

    private static string FormatSize(long bytes) => bytes switch
    {
        >= 1_048_576 => $"{bytes / 1_048_576.0:F1} MB",
        >= 1_024 => $"{bytes / 1024.0:F0} KB",
        _ => $"{bytes} B"
    };
}
