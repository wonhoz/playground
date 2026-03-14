using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Data.Sqlite;
using SharpCompress.Archives;
using SharpCompress.Common;

namespace MangaView;

public enum ViewMode { Single, Double, Vertical }
public enum ReadDirection { LTR, RTL }

public partial class MainWindow : Window
{
    [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int val, int sz);

    private string? _currentFile;
    private List<string> _pages = [];          // 아카이브 내 이미지 파일 목록 (정렬됨)
    private int _currentPage = 0;
    private ViewMode _viewMode = ViewMode.Single;
    private ReadDirection _direction = ReadDirection.LTR;
    private double _zoom = 1.0;
    private SqliteConnection? _db;

    // 지연 로딩: 메모리에 페이지 캐시 (현재 페이지 ±2)
    private readonly Dictionary<int, BitmapSource> _cache = [];

    static readonly string[] ImageExts = [".jpg", ".jpeg", ".png", ".webp", ".bmp", ".gif", ".avif"];

    public MainWindow() => InitializeComponent();

    void Window_Loaded(object sender, RoutedEventArgs e)
    {
        var helper = new System.Windows.Interop.WindowInteropHelper(this);
        int dark = 1;
        DwmSetWindowAttribute(helper.Handle, 20, ref dark, sizeof(int));

        ViewModeCombo.ItemsSource = new[] { "단일 페이지", "이중 페이지", "세로 스크롤" };
        ViewModeCombo.SelectedIndex = 0;
        BgCombo.ItemsSource = new[] { "검정", "흰색", "크림", "회색" };
        BgCombo.SelectedIndex = 0;
        DirectionCombo.ItemsSource = new[] { "←→ 좌→우", "←→ 우→좌 (망가)" };
        DirectionCombo.SelectedIndex = 0;

        InitDb();

        // 커맨드라인 파일 열기
        var args = Environment.GetCommandLineArgs();
        if (args.Length > 1 && File.Exists(args[1]))
            OpenFile(args[1]);
    }

    void InitDb()
    {
        string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MangaView");
        Directory.CreateDirectory(dir);
        _db = new SqliteConnection($"Data Source={Path.Combine(dir, "library.db")}");
        _db.Open();
        _db.ExecuteNonQuery(@"CREATE TABLE IF NOT EXISTS library (
            path TEXT PRIMARY KEY, last_page INTEGER DEFAULT 0, added_at TEXT)");
    }

    // ─── 파일 열기 ────────────────────────────────────────────────────
    void BtnOpen_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "만화 파일|*.cbz;*.cbr;*.zip;*.7z;*.rar|모든 파일|*.*",
            Title = "만화 파일 열기"
        };
        if (dlg.ShowDialog() == true)
            OpenFile(dlg.FileName);
    }

    void OpenFile(string path)
    {
        try
        {
            _currentFile = path;
            _pages.Clear();
            _cache.Clear();

            using var archive = ArchiveFactory.Open(path);
            _pages = archive.Entries
                .Where(e => !e.IsDirectory && ImageExts.Contains(Path.GetExtension(e.Key ?? "").ToLower()))
                .Select(e => e.Key!)
                .OrderBy(k => k, StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (_pages.Count == 0)
            {
                PageText.Text = "이미지 없음";
                return;
            }

            // 북마크에서 마지막 페이지 복원
            _currentPage = GetLastPage(path);
            PageSlider.Maximum = _pages.Count - 1;
            PageSlider.Value = _currentPage;

            Title = $"Manga.View — {Path.GetFileName(path)}";
            SaveToLibrary(path);
            ShowCurrentPage();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"파일 열기 오류: {ex.Message}", "Manga.View", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    // ─── 페이지 표시 ──────────────────────────────────────────────────
    void ShowCurrentPage()
    {
        if (_pages.Count == 0 || _currentFile == null) return;
        PagePanel.Children.Clear();

        _currentPage = Math.Clamp(_currentPage, 0, _pages.Count - 1);

        switch (_viewMode)
        {
            case ViewMode.Single:
                AddPageImage(_currentPage);
                PageText.Text = $"{_currentPage + 1} / {_pages.Count}";
                break;

            case ViewMode.Double:
                // 방향에 따라 페이지 순서 결정
                int left = _direction == ReadDirection.LTR ? _currentPage : _currentPage + 1;
                int right = _direction == ReadDirection.LTR ? _currentPage + 1 : _currentPage;
                var sp = new StackPanel { Orientation = Orientation.Horizontal };
                if (left >= 0 && left < _pages.Count) sp.Children.Add(CreatePageImage(left));
                if (right >= 0 && right < _pages.Count) sp.Children.Add(CreatePageImage(right));
                PagePanel.Children.Add(sp);
                PageText.Text = $"{_currentPage + 1}-{Math.Min(_currentPage + 2, _pages.Count)} / {_pages.Count}";
                break;

            case ViewMode.Vertical:
                for (int i = 0; i < _pages.Count; i++)
                    AddPageImage(i);
                PageText.Text = $"전체 {_pages.Count}페이지 (세로 스크롤)";
                // 현재 페이지로 스크롤은 지연
                Dispatcher.InvokeAsync(() =>
                {
                    if (PagePanel.Children.Count > _currentPage && PagePanel.Children[_currentPage] is FrameworkElement el)
                        el.BringIntoView();
                }, System.Windows.Threading.DispatcherPriority.Loaded);
                break;
        }

        PageSlider.Value = _currentPage;
        SaveLastPage(_currentFile!, _currentPage);
        PreloadCache();
    }

    void AddPageImage(int pageIdx) => PagePanel.Children.Add(CreatePageImage(pageIdx));

    UIElement CreatePageImage(int pageIdx)
    {
        var img = new Image
        {
            Margin = new Thickness(2),
            Stretch = Stretch.Uniform,
            StretchDirection = StretchDirection.Both
        };

        var bitmap = GetCachedBitmap(pageIdx);
        img.Source = bitmap;

        // zoom 적용
        if (bitmap != null)
        {
            double naturalW = bitmap.PixelWidth;
            double naturalH = bitmap.PixelHeight;

            if (_viewMode == ViewMode.Single || _viewMode == ViewMode.Vertical)
            {
                double viewW = MainScroll.ViewportWidth > 0 ? MainScroll.ViewportWidth - 20 : 800;
                img.Width = _zoom == 1.0 ? viewW : naturalW * _zoom;
                img.Height = img.Width * (naturalH / naturalW);
            }
            else
            {
                double viewW = (MainScroll.ViewportWidth > 0 ? MainScroll.ViewportWidth - 20 : 800) / 2;
                img.Width = viewW;
                img.Height = img.Width * (naturalH / naturalW);
            }
        }
        else
        {
            img.Width = 400; img.Height = 600;
            img.Source = null;
        }

        return img;
    }

    BitmapSource? GetCachedBitmap(int idx)
    {
        if (_cache.TryGetValue(idx, out var cached)) return cached;
        var bmp = LoadPage(idx);
        if (bmp != null) _cache[idx] = bmp;
        return bmp;
    }

    BitmapSource? LoadPage(int idx)
    {
        if (_currentFile == null || idx < 0 || idx >= _pages.Count) return null;
        try
        {
            using var archive = ArchiveFactory.Open(_currentFile);
            var entry = archive.Entries.First(e => e.Key == _pages[idx]);
            using var stream = entry.OpenEntryStream();
            using var ms = new MemoryStream();
            stream.CopyTo(ms);
            ms.Position = 0;
            var bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.CacheOption = BitmapCacheOption.OnLoad;
            bmp.StreamSource = ms;
            bmp.EndInit();
            bmp.Freeze();
            return bmp;
        }
        catch { return null; }
    }

    void PreloadCache()
    {
        // 현재 페이지 ±2 미리 로딩
        for (int i = _currentPage - 2; i <= _currentPage + 2; i++)
        {
            if (i < 0 || i >= _pages.Count || _cache.ContainsKey(i)) continue;
            int idx = i;
            Task.Run(() =>
            {
                var bmp = LoadPage(idx);
                if (bmp != null) Dispatcher.Invoke(() => _cache[idx] = bmp);
            });
        }
        // 오래된 캐시 제거
        var toRemove = _cache.Keys.Where(k => Math.Abs(k - _currentPage) > 5).ToList();
        foreach (var k in toRemove) _cache.Remove(k);
    }

    // ─── 탐색 ─────────────────────────────────────────────────────────
    void BtnFirst_Click(object sender, RoutedEventArgs e) { _currentPage = 0; ShowCurrentPage(); }
    void BtnLast_Click(object sender, RoutedEventArgs e) { _currentPage = _pages.Count - 1; ShowCurrentPage(); }

    void BtnPrev_Click(object sender, RoutedEventArgs e)
    {
        if (_pages.Count == 0) return;
        int step = _viewMode == ViewMode.Double ? 2 : 1;
        _currentPage = Math.Max(0, _currentPage - step);
        ShowCurrentPage();
    }

    void BtnNext_Click(object sender, RoutedEventArgs e)
    {
        if (_pages.Count == 0) return;
        int step = _viewMode == ViewMode.Double ? 2 : 1;
        _currentPage = Math.Min(_pages.Count - 1, _currentPage + step);
        ShowCurrentPage();
    }

    void PageSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!IsLoaded) return;
        int newPage = (int)e.NewValue;
        if (newPage != _currentPage) { _currentPage = newPage; ShowCurrentPage(); }
    }

    void Window_KeyDown(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Right or Key.PageDown or Key.Space:
                if (_direction == ReadDirection.LTR) BtnNext_Click(this, null!);
                else BtnPrev_Click(this, null!);
                break;
            case Key.Left or Key.PageUp:
                if (_direction == ReadDirection.LTR) BtnPrev_Click(this, null!);
                else BtnNext_Click(this, null!);
                break;
            case Key.Home: BtnFirst_Click(this, null!); break;
            case Key.End: BtnLast_Click(this, null!); break;
        }
        e.Handled = true;
    }

    void MainScroll_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl))
        {
            _zoom = e.Delta > 0 ? Math.Min(_zoom * 1.1, 4.0) : Math.Max(_zoom / 1.1, 0.25);
            ShowCurrentPage();
            e.Handled = true;
        }
    }

    // ─── 설정 ─────────────────────────────────────────────────────────
    void ViewModeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded) return;
        _viewMode = ViewModeCombo.SelectedIndex switch { 1 => ViewMode.Double, 2 => ViewMode.Vertical, _ => ViewMode.Single };
        ShowCurrentPage();
    }

    void BgCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded) return;
        MainScroll.Background = BgCombo.SelectedIndex switch
        {
            1 => Brushes.White,
            2 => new SolidColorBrush(Color.FromRgb(0xF5, 0xF0, 0xE8)),
            3 => new SolidColorBrush(Color.FromRgb(0x44, 0x44, 0x44)),
            _ => new SolidColorBrush(Color.FromRgb(0x0A, 0x0A, 0x0A))
        };
    }

    void DirectionCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded) return;
        _direction = DirectionCombo.SelectedIndex == 1 ? ReadDirection.RTL : ReadDirection.LTR;
    }

    void BtnZoomIn_Click(object sender, RoutedEventArgs e) { _zoom = Math.Min(_zoom * 1.2, 4.0); ShowCurrentPage(); }
    void BtnZoomOut_Click(object sender, RoutedEventArgs e) { _zoom = Math.Max(_zoom / 1.2, 0.25); ShowCurrentPage(); }
    void BtnFitWidth_Click(object sender, RoutedEventArgs e) { _zoom = 1.0; ShowCurrentPage(); }

    // ─── 북마크 ───────────────────────────────────────────────────────
    void BtnBookmark_Click(object sender, RoutedEventArgs e)
    {
        if (_currentFile != null)
        {
            SaveLastPage(_currentFile, _currentPage);
            MessageBox.Show($"북마크 저장: {_currentPage + 1}페이지", "Manga.View", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    void BtnLibrary_Click(object sender, RoutedEventArgs e)
    {
        var win = new LibraryWindow(_db!, path => OpenFile(path)) { Owner = this };
        win.ShowDialog();
    }

    // ─── DB 헬퍼 ──────────────────────────────────────────────────────
    void SaveToLibrary(string path)
    {
        _db!.ExecuteNonQuery(
            "INSERT OR IGNORE INTO library(path, last_page, added_at) VALUES($p, 0, $d)",
            ("$p", path), ("$d", DateTime.Now.ToString("s")));
    }

    void SaveLastPage(string path, int page)
    {
        _db!.ExecuteNonQuery("UPDATE library SET last_page=$pg WHERE path=$p", ("$pg", page), ("$p", path));
    }

    int GetLastPage(string path)
    {
        using var cmd = _db!.CreateCommand();
        cmd.CommandText = "SELECT last_page FROM library WHERE path=$p";
        cmd.Parameters.AddWithValue("$p", path);
        var result = cmd.ExecuteScalar();
        return result is long l ? (int)l : 0;
    }
}

// ─── DB 확장 ──────────────────────────────────────────────────────────
static class DbExt
{
    public static void ExecuteNonQuery(this SqliteConnection db, string sql, params (string, object)[] parms)
    {
        using var cmd = db.CreateCommand();
        cmd.CommandText = sql;
        foreach (var (k, v) in parms) cmd.Parameters.AddWithValue(k, v);
        cmd.ExecuteNonQuery();
    }
}

// ─── 라이브러리 창 ────────────────────────────────────────────────────
public class LibraryWindow : Window
{
    [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int val, int sz);
    private readonly SqliteConnection _db;
    private readonly Action<string> _openFile;

    public LibraryWindow(SqliteConnection db, Action<string> openFile)
    {
        _db = db; _openFile = openFile;
        Title = "📚 라이브러리"; Width = 600; Height = 400;
        Background = new SolidColorBrush(Color.FromRgb(0x11, 0x11, 0x11));
        Foreground = new SolidColorBrush(Color.FromRgb(0xE0, 0xE0, 0xE0));
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        var grid = new Grid();
        grid.RowDefinitions.Add(new RowDefinition());
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(44) });

        var lb = new ListBox
        {
            Background = new SolidColorBrush(Color.FromRgb(0x11, 0x11, 0x11)),
            Foreground = new SolidColorBrush(Color.FromRgb(0xE0, 0xE0, 0xE0)),
            BorderThickness = new Thickness(0)
        };

        using var cmd = db.CreateCommand();
        cmd.CommandText = "SELECT path, last_page FROM library ORDER BY added_at DESC LIMIT 100";
        using var reader = cmd.ExecuteReader();
        var items = new List<(string Path, int LastPage)>();
        while (reader.Read())
            items.Add((reader.GetString(0), reader.GetInt32(1)));

        lb.ItemsSource = items.Select(i => $"{Path.GetFileName(i.Path)} (p.{i.LastPage + 1}) — {i.Path}").ToList();
        lb.MouseDoubleClick += (_, _) =>
        {
            if (lb.SelectedIndex >= 0 && lb.SelectedIndex < items.Count)
            {
                _openFile(items[lb.SelectedIndex].Path);
                Close();
            }
        };
        grid.Children.Add(lb);

        var btn = new Button
        {
            Content = "열기", Margin = new Thickness(12, 6, 12, 6),
            Background = new SolidColorBrush(Color.FromRgb(0x5B, 0x8A, 0xF0)),
            Foreground = Brushes.White,
            Cursor = Cursors.Hand
        };
        btn.Click += (_, _) =>
        {
            if (lb.SelectedIndex >= 0 && lb.SelectedIndex < items.Count)
            {
                _openFile(items[lb.SelectedIndex].Path);
                Close();
            }
        };
        Grid.SetRow(btn, 1);
        grid.Children.Add(btn);
        Content = grid;

        Loaded += (_, _) =>
        {
            var h = new System.Windows.Interop.WindowInteropHelper(this);
            int dark = 1;
            DwmSetWindowAttribute(h.Handle, 20, ref dark, sizeof(int));
        };
    }
}
