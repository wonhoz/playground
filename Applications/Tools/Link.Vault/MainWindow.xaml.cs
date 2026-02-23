using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using LinkVault.Models;
using LinkVault.Services;

namespace LinkVault;

public partial class MainWindow : Window
{
    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

    private readonly DatabaseService _db;
    private readonly SnapshotService _snapshot;
    private readonly ApiServerService _api;

    private List<Bookmark> _bookmarks = [];
    private Bookmark? _selected;
    private string _activeTag = "";
    private bool _webViewReady = false;

    public MainWindow()
    {
        InitializeComponent();
        _db       = new DatabaseService();
        _snapshot = new SnapshotService();
        _api      = new ApiServerService();

        _api.BookmarkAddRequested += OnApiBookmarkAdd;
        _api.Start();

        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        // 다크 타이틀바
        var dark = 1;
        DwmSetWindowAttribute(new System.Windows.Interop.WindowInteropHelper(this).Handle, 20, ref dark, sizeof(int));

        // WebView2 초기화
        try
        {
            await WebPreview.EnsureCoreWebView2Async();
            WebPreview.CoreWebView2.Settings.IsStatusBarEnabled = false;
            WebPreview.CoreWebView2.Settings.AreDefaultContextMenusEnabled = true;
            _webViewReady = true;
        }
        catch { /* WebView2 런타임 미설치 시 무시 */ }

        TxtApiStatus.Text = $"브라우저 확장: localhost:{ApiServerService.Port}";
        Refresh();
    }

    // ── 데이터 로드 ───────────────────────────────────────────────────

    private void Refresh()
    {
        var query     = TxtSearch.Text.Trim();
        var unread    = ChkUnread.IsChecked == true;
        var minStars  = CmbStars.SelectedIndex > 0 ? (int?)CmbStars.SelectedIndex : null;
        var tag       = string.IsNullOrEmpty(_activeTag) ? null : _activeTag;

        _bookmarks = string.IsNullOrEmpty(query)
            ? _db.GetAll(unread, minStars, tag)
            : _db.Search(query, unread, minStars, tag);

        LstBookmarks.ItemsSource = _bookmarks;
        TxtListHeader.Text = $"{_bookmarks.Count}개";

        var total = _db.CountAll();
        TxtCount.Text = $"  총 {total}개";

        RebuildTagPanel();
        SetStatus("새로고침 완료");
    }

    private void RebuildTagPanel()
    {
        TagPanel.Children.Clear();

        // "전체" 버튼
        var allBtn = new Button
        {
            Content = "전체",
            Style = FindResource(string.IsNullOrEmpty(_activeTag) ? "TagBtnActive" : "TagBtn") as Style
        };
        allBtn.Click += (_, _) => { _activeTag = ""; Refresh(); };
        TagPanel.Children.Add(allBtn);

        foreach (var tag in _db.GetAllTags())
        {
            var t = tag;
            var btn = new Button
            {
                Content = t,
                Style = FindResource(t == _activeTag ? "TagBtnActive" : "TagBtn") as Style
            };
            btn.Click += (_, _) => { _activeTag = t; Refresh(); };
            TagPanel.Children.Add(btn);
        }
    }

    // ── 선택 처리 ─────────────────────────────────────────────────────

    private void LstBookmarks_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _selected = LstBookmarks.SelectedItem as Bookmark;
        ShowDetail(_selected);
    }

    private void ShowDetail(Bookmark? b)
    {
        if (b == null)
        {
            DetailPanel.Visibility = Visibility.Collapsed;
            PreviewToolbar.Visibility = Visibility.Collapsed;
            WebPreview.Visibility = Visibility.Collapsed;
            TxtEmptyHint.Visibility = Visibility.Visible;
            return;
        }

        DetailPanel.Visibility = Visibility.Visible;
        PreviewToolbar.Visibility = Visibility.Visible;
        TxtEmptyHint.Visibility = Visibility.Collapsed;

        TxtDetailTitle.Text = b.Title;
        TxtDetailUrl.Text   = b.Url;
        TxtDetailDesc.Text  = b.Description;
        TxtDetailTags.Text  = string.IsNullOrEmpty(b.Tags) ? "" : $"🏷 {b.Tags}";
        BtnToggleRead.Content = b.IsRead ? "미읽음으로" : "읽음 표시";

        UpdateStarButtons(b.Stars);

        // 스냅샷 미리보기
        if (_webViewReady && SnapshotService.SnapshotExists(b.Id))
        {
            var path = SnapshotService.GetSnapshotPath(b.Id);
            WebPreview.Source = new Uri(path);
            WebPreview.Visibility = Visibility.Visible;
            TxtPreviewStatus.Text = "📄 로컬 스냅샷";
        }
        else if (_webViewReady)
        {
            WebPreview.NavigateToString(BuildPlaceholderHtml(b));
            WebPreview.Visibility = Visibility.Visible;
            TxtPreviewStatus.Text = "🌐 스냅샷 없음 (📥 클릭하여 저장)";
        }
        else
        {
            WebPreview.Visibility = Visibility.Collapsed;
            TxtPreviewStatus.Text = "⚠ WebView2 런타임 미설치";
        }
    }

    private static string BuildPlaceholderHtml(Bookmark b) => $$"""
        <!DOCTYPE html><html>
        <head><meta charset="utf-8"><style>
        body{background:#0f0f1a;color:#888;font-family:Segoe UI,sans-serif;display:flex;
              align-items:center;justify-content:center;height:100vh;margin:0}
        .box{text-align:center;max-width:500px;padding:40px}
        h2{color:#4a8fff;font-size:18px}a{color:#4a8fff}
        </style></head>
        <body><div class="box">
        <h2>🔗 {{System.Security.SecurityElement.Escape(b.Title)}}</h2>
        <p style="font-size:12px">{{System.Security.SecurityElement.Escape(b.Host)}}</p>
        <p style="font-size:11px;color:#505060">{{System.Security.SecurityElement.Escape(b.Description)}}</p>
        <p style="font-size:11px;margin-top:20px">📥 스냅샷 버튼을 클릭하면 오프라인에 저장됩니다</p>
        </div></body></html>
        """;

    private void UpdateStarButtons(int stars)
    {
        var btns = new[] { BtnStar1, BtnStar2, BtnStar3, BtnStar4, BtnStar5 };
        for (var i = 0; i < btns.Length; i++)
            btns[i].Foreground = i < stars
                ? System.Windows.Media.Brushes.Gold
                : new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(60, 60, 80));
    }

    // ── 버튼 핸들러 ───────────────────────────────────────────────────

    private void BtnAdd_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new AddBookmarkDialog(_db, _snapshot) { Owner = this };
        if (dlg.ShowDialog() == true) Refresh();
    }

    private void BtnRefresh_Click(object sender, RoutedEventArgs e) => Refresh();

    private void BtnToggleRead_Click(object sender, RoutedEventArgs e)
    {
        if (_selected == null) return;
        _selected.IsRead = !_selected.IsRead;
        _db.SetRead(_selected.Id, _selected.IsRead);
        BtnToggleRead.Content = _selected.IsRead ? "미읽음으로" : "읽음 표시";
        Refresh();
    }

    private async void BtnSnapshot_Click(object sender, RoutedEventArgs e)
    {
        if (_selected == null) return;
        SetStatus("스냅샷 저장 중...");
        BtnSnapshot.IsEnabled = false;
        var path = await _snapshot.SaveSnapshotAsync(_selected.Id, _selected.Url);
        if (path != null)
        {
            _db.UpdateSnapshotPath(_selected.Id, path);
            _selected.SnapshotPath = path;
            ShowDetail(_selected);
            SetStatus("스냅샷 저장 완료");
        }
        else
        {
            SetStatus("스냅샷 저장 실패 (네트워크 확인)");
        }
        BtnSnapshot.IsEnabled = true;
    }

    private void BtnEdit_Click(object sender, RoutedEventArgs e)
    {
        if (_selected == null) return;
        var dlg = new AddBookmarkDialog(_db, _snapshot, _selected) { Owner = this };
        if (dlg.ShowDialog() == true) { Refresh(); ShowDetail(_selected); }
    }

    private void BtnDelete_Click(object sender, RoutedEventArgs e)
    {
        if (_selected == null) return;
        var r = MessageBox.Show($"'{_selected.Title}'\n삭제하시겠습니까?",
            "삭제 확인", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (r != MessageBoxResult.Yes) return;
        _db.Delete(_selected.Id);
        _selected = null;
        ShowDetail(null);
        Refresh();
    }

    private void BtnStar_Click(object sender, RoutedEventArgs e)
    {
        if (_selected == null) return;
        var stars = int.Parse(((Button)sender).Tag.ToString()!);
        _selected.Stars = _selected.Stars == stars ? 0 : stars; // 같은 별점 클릭 시 초기화
        _db.SetStars(_selected.Id, _selected.Stars);
        UpdateStarButtons(_selected.Stars);
        Refresh();
    }

    private void BtnOpenBrowser_Click(object sender, RoutedEventArgs e)
    {
        if (_selected == null) return;
        Process.Start(new ProcessStartInfo(_selected.Url) { UseShellExecute = true });
    }

    private void TxtDetailUrl_Click(object sender, MouseButtonEventArgs e)
    {
        if (_selected == null) return;
        Process.Start(new ProcessStartInfo(_selected.Url) { UseShellExecute = true });
    }

    // ── 검색/필터 ─────────────────────────────────────────────────────

    private System.Windows.Threading.DispatcherTimer? _searchTimer;

    private void TxtSearch_TextChanged(object sender, TextChangedEventArgs e)
    {
        _searchTimer?.Stop();
        _searchTimer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(300)
        };
        _searchTimer.Tick += (_, _) => { _searchTimer.Stop(); Refresh(); };
        _searchTimer.Start();
    }

    private void Filter_Changed(object sender, RoutedEventArgs e) => Refresh();

    // ── API 서버 콜백 ─────────────────────────────────────────────────

    private void OnApiBookmarkAdd(string url, string title)
    {
        Dispatcher.Invoke(async () =>
        {
            SetStatus($"확장에서 추가 중: {url}");
            var meta = await _snapshot.FetchMetaAsync(url);
            var b = new Bookmark
            {
                Url   = url,
                Title = string.IsNullOrEmpty(title) ? meta.Title : title,
                Description = meta.Description
            };
            _db.Insert(b);
            Refresh();
            SetStatus($"북마크 추가: {b.Title}");
        });
    }

    // ── 공통 헬퍼 ────────────────────────────────────────────────────

    private void SetStatus(string msg) => TxtStatus.Text = msg;

    private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
    {
        _api.Dispose();
        _db.Dispose();
    }
}
