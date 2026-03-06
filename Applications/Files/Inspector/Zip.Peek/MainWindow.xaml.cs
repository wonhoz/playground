using System.Runtime.InteropServices;
using System.Windows.Interop;
using ZipPeek.Views;

namespace ZipPeek;

public partial class MainWindow : Window
{
    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

    private ArchiveView? _archiveView;
    private const string SearchPlaceholder = "파일 내용 또는 이름 검색...";

    public MainWindow()
    {
        InitializeComponent();
        SourceInitialized += (_, _) => ApplyDarkTitle();
        ShowDropHint();
    }

    private void ApplyDarkTitle()
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        int dark = 1;
        DwmSetWindowAttribute(hwnd, 20, ref dark, sizeof(int));
    }

    private void ShowDropHint()
    {
        var hint = new StackPanel { VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center };
        hint.Children.Add(new TextBlock
        {
            Text = "🗜", FontSize = 56,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 0, 0, 12)
        });
        hint.Children.Add(new TextBlock
        {
            Text = "아카이브 파일을 여기로 드래그하거나 열기 버튼을 클릭하세요",
            FontSize = 14, Foreground = new SolidColorBrush(Color.FromRgb(0x88, 0x88, 0x99)),
            HorizontalAlignment = HorizontalAlignment.Center
        });
        hint.Children.Add(new TextBlock
        {
            Text = "ZIP · 7z · RAR · TAR · GZ · BZ2 · XZ 지원",
            FontSize = 12, Foreground = new SolidColorBrush(Color.FromRgb(0x55, 0x55, 0x66)),
            HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 6, 0, 0)
        });
        MainContent.Child = hint;
    }

    public void OpenArchive(string path)
    {
        Title = $"Zip.Peek — {Path.GetFileName(path)}";
        _archiveView = new ArchiveView(path, this);
        MainContent.Child = _archiveView;
        BtnExtractSelected.IsEnabled = true;
        BtnExtractAll.IsEnabled = true;
    }

    public void SetStatus(string text) => TxtStatus.Text = text;
    public void SetStats(string text) => TxtStats.Text = text;
    public void ShowProgress(bool visible, bool indeterminate = false)
    {
        PbMain.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
        PbMain.IsIndeterminate = indeterminate;
    }

    public void SetProgress(double value, double max = 100)
    {
        PbMain.IsIndeterminate = false;
        PbMain.Maximum = max;
        PbMain.Value = value;
    }

    // ── 이벤트 ──────────────────────────────────────────────────
    private void BtnOpen_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "아카이브 파일|*.zip;*.7z;*.rar;*.tar;*.gz;*.bz2;*.xz;*.tar.gz;*.tar.bz2;*.tar.xz|모든 파일|*.*",
            Title = "아카이브 열기"
        };
        if (dlg.ShowDialog() == true) OpenArchive(dlg.FileName);
    }

    private void BtnExtractSelected_Click(object sender, RoutedEventArgs e) =>
        _archiveView?.ExtractSelected();

    private void BtnExtractAll_Click(object sender, RoutedEventArgs e) =>
        _archiveView?.ExtractAll();

    private void BtnSearch_Click(object sender, RoutedEventArgs e) => RunSearch();

    private void TxtSearch_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) RunSearch();
        if (e.Key == Key.Escape)
        {
            TxtSearch.Text = SearchPlaceholder;
            TxtSearch.Foreground = new SolidColorBrush(Color.FromRgb(0x88, 0x88, 0x99));
            _archiveView?.ClearSearch();
        }
    }

    private void TxtSearch_GotFocus(object sender, RoutedEventArgs e)
    {
        if (TxtSearch.Text == SearchPlaceholder)
        {
            TxtSearch.Text = "";
            TxtSearch.Foreground = new SolidColorBrush(Color.FromRgb(0xE8, 0xE8, 0xF0));
        }
    }

    private void TxtSearch_LostFocus(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(TxtSearch.Text))
        {
            TxtSearch.Text = SearchPlaceholder;
            TxtSearch.Foreground = new SolidColorBrush(Color.FromRgb(0x88, 0x88, 0x99));
        }
    }

    private void RunSearch()
    {
        if (_archiveView is null) return;
        var query = TxtSearch.Text == SearchPlaceholder ? "" : TxtSearch.Text.Trim();
        bool contentSearch = ChkContentSearch.IsChecked == true;
        _archiveView.RunSearch(query, contentSearch);
    }

    private void Window_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private void Window_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(DataFormats.FileDrop) is string[] files && files.Length > 0)
            OpenArchive(files[0]);
    }
}
