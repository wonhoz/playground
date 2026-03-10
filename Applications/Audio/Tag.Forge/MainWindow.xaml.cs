using System.Runtime.InteropServices;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using Microsoft.Win32;

namespace Tag.Forge;

public partial class MainWindow : Window
{
    [DllImport("dwmapi.dll")]
    static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int val, int size);

    readonly MainViewModel _vm;

    public MainWindow()
    {
        _vm = new MainViewModel();
        DataContext = _vm;
        InitializeComponent();
        Loaded      += OnLoaded;
        Drop        += OnDrop;
        DragOver    += (_, e) => { e.Effects = DragDropEffects.Copy; e.Handled = true; };
    }

    void OnLoaded(object s, RoutedEventArgs e)
    {
        var h = new WindowInteropHelper(this).Handle;
        int v = 1;
        DwmSetWindowAttribute(h, 20, ref v, sizeof(int));
    }

    // ── 드래그&드롭 ──────────────────────────────────────────────────────

    void OnDrop(object s, DragEventArgs e)
    {
        if (e.Data.GetData(DataFormats.FileDrop) is string[] paths)
            _vm.LoadPaths(paths);
    }

    // ── 툴바 버튼 ─────────────────────────────────────────────────────────

    void BtnOpen_Click(object s, RoutedEventArgs e)
    {
        var dlg = new Microsoft.Win32.OpenFolderDialog
        {
            Title = "음악 파일 폴더 선택",
        };
        if (dlg.ShowDialog() == true)
            _vm.LoadPaths([dlg.FolderName]);
    }

    void BtnSaveAll_Click(object s, RoutedEventArgs e) => _vm.SaveAll();

    void BtnSaveSel_Click(object s, RoutedEventArgs e)
        => _vm.SaveSelected(TrackGrid.SelectedItems.Cast<TrackViewModel>());

    void BtnRemove_Click(object s, RoutedEventArgs e)
        => _vm.RemoveSelected(TrackGrid.SelectedItems.Cast<TrackViewModel>());

    void BtnClear_Click(object s, RoutedEventArgs e)
    {
        if (MessageBox.Show("목록을 모두 지우시겠습니까?\n저장되지 않은 변경사항은 사라집니다.",
            "확인", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
        _vm.ClearAll();
    }

    void BtnAutoNum_Click(object s, RoutedEventArgs e)
    {
        var sel = TrackGrid.SelectedItems.Cast<TrackViewModel>().ToList();
        if (sel.Count == 0) sel = _vm.Tracks.ToList();
        _vm.AutoNumber(sel);
    }

    async void BtnMB_Click(object s, RoutedEventArgs e)
    {
        var sel = TrackGrid.SelectedItems.Cast<TrackViewModel>().ToList();
        if (sel.Count == 0)
        {
            MessageBox.Show("MusicBrainz로 조회할 트랙을 선택하세요.", "알림", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        await _vm.LookupMusicBrainzAsync(sel);
    }

    void BtnCancel_Click(object s, RoutedEventArgs e) => _vm.CancelMusicBrainz();

    // ── 파일명↔태그 패턴 ─────────────────────────────────────────────────

    void BtnFilenameToTag_Click(object s, RoutedEventArgs e)
    {
        var sel = TrackGrid.SelectedItems.Cast<TrackViewModel>().ToList();
        if (sel.Count == 0) sel = _vm.Tracks.ToList();
        _vm.ApplyFilenameToTag(sel, TxtPattern.Text);
    }

    void BtnTagToFilename_Click(object s, RoutedEventArgs e)
    {
        var sel = TrackGrid.SelectedItems.Cast<TrackViewModel>().ToList();
        if (sel.Count == 0) sel = _vm.Tracks.ToList();
        if (MessageBox.Show($"선택된 {sel.Count}개 트랙의 파일명을 패턴에 맞게 변경합니다.\n계속하시겠습니까?",
            "파일명 변경", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
        _vm.ApplyTagToFilename(sel, TxtPattern.Text);
    }

    // ── 앨범아트 ─────────────────────────────────────────────────────────

    void BtnArt_Click(object s, RoutedEventArgs e)
    {
        var sel = TrackGrid.SelectedItems.Cast<TrackViewModel>().FirstOrDefault();
        if (sel == null) return;
        var dlg = new OpenFileDialog
        {
            Filter = "이미지 파일|*.jpg;*.jpeg;*.png;*.bmp|모든 파일|*.*",
            Title  = "앨범 아트 선택"
        };
        if (dlg.ShowDialog() != true) return;
        var bytes = File.ReadAllBytes(dlg.FileName);
        foreach (var t in TrackGrid.SelectedItems.Cast<TrackViewModel>())
        {
            t.Info.AlbumArt = bytes;
            t.Modified = true;
        }
        UpdateArtDisplay(bytes);
    }

    void BtnArtClear_Click(object s, RoutedEventArgs e)
    {
        foreach (var t in TrackGrid.SelectedItems.Cast<TrackViewModel>())
        {
            t.Info.AlbumArt = null;
            t.Modified = true;
        }
        ArtImage.Source = null;
    }

    // ── 컨텍스트 메뉴 ────────────────────────────────────────────────────

    void OpenFileLocation_Click(object s, RoutedEventArgs e)
    {
        var t = TrackGrid.SelectedItem as TrackViewModel;
        if (t == null) return;
        var dir = Path.GetDirectoryName(t.FilePath);
        if (dir != null) System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{t.FilePath}\"");
    }

    // ── 선택 변경 ────────────────────────────────────────────────────────

    void TrackGrid_SelectionChanged(object s, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (!IsLoaded) return;
        var sel = TrackGrid.SelectedItems.Cast<TrackViewModel>().ToList();
        TxtSelCount.Text = sel.Count == 0
            ? "선택 없음"
            : $"{sel.Count}개 선택";
        var first = sel.FirstOrDefault();
        UpdateArtDisplay(first?.Info.AlbumArt);
    }

    void UpdateArtDisplay(byte[]? artBytes)
    {
        if (artBytes == null || artBytes.Length == 0)
        {
            ArtImage.Source = null;
            return;
        }
        try
        {
            using var ms = new System.IO.MemoryStream(artBytes);
            var bi = new BitmapImage();
            bi.BeginInit();
            bi.CacheOption  = BitmapCacheOption.OnLoad;
            bi.StreamSource = ms;
            bi.EndInit();
            ArtImage.Source = bi;
        }
        catch { ArtImage.Source = null; }
    }

    void TrackGrid_CellEditEnding(object s, System.Windows.Controls.DataGridCellEditEndingEventArgs e) { }
}
