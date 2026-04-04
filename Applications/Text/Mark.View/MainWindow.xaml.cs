using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using MarkView.Models;
using MarkView.Services;
using System.Linq;
using Microsoft.Win32;
using Microsoft.Web.WebView2.Core;

namespace MarkView;

public partial class MainWindow : Window
{
    private readonly MarkdownRenderer _renderer = new();
    private readonly List<MarkDocument> _docs = [];
    private readonly List<Border> _tabs = [];
    private readonly AppSettings _settings;
    private int _activeIndex = -1;
    private bool _isEditMode;
    private bool _isTocVisible;
    private bool _isFocusMode;
    private bool _suppressEditorChange;
    private bool _suppressTocSelection;
    private HelpWindow? _helpWindow;
    private DispatcherTimer? _previewTimer;
    private ScrollViewer? _editorScrollViewer;
    private DispatcherTimer? _autoSaveTimer;
    private string _currentTheme = "dark";
    private double _pendingScrollY = 0;
    private bool _webViewReady = false;
    private readonly TaskCompletionSource _webViewReadyTcs = new();
    private readonly Dictionary<string, FileSystemWatcher> _fileWatchers = new(StringComparer.OrdinalIgnoreCase);
    private double _editorFontSize = 13;
    private double _previewFontSize = 15;

    public MainWindow()
    {
        _settings = AppSettings.Load();
        _currentTheme = _settings.Theme;
        _editorFontSize = _settings.EditorFontSize;
        _previewFontSize = _settings.PreviewFontSize;
        InitializeComponent();
        RestoreWindowBounds();
        ApplySavedTheme();
        ApplyEditorFontSize();
        Loaded += OnMainWindowLoaded;
        InitWebView();
        SetupPreviewTimer();
        SetupAutoSaveTimer();
        RefreshRecentList();
        RestoreUiState();
        RestoreSessionAsync();
    }

    private async void RestoreSessionAsync()
    {
        var files = _settings.OpenFiles.Where(File.Exists).ToList();
        if (files.Count == 0) return;
        // WebView2 초기화 완료 이벤트 대기 (최대 3초)
        await Task.WhenAny(_webViewReadyTcs.Task, Task.Delay(3000));
        foreach (var f in files)
            await OpenFileAsync(f);
        // 마지막 활성 탭 복원
        var savedIdx = _settings.ActiveTabIndex;
        if (savedIdx > 0 && savedIdx < _docs.Count)
            SwitchTo(savedIdx);
    }

    private void RestoreWindowBounds()
    {
        Width = _settings.WindowWidth;
        Height = _settings.WindowHeight;
        if (!double.IsNaN(_settings.WindowLeft) && !double.IsNaN(_settings.WindowTop))
        {
            // 저장된 위치가 현재 연결된 화면 내에 있는지 확인
            var virtualScreen = new System.Windows.Rect(
                SystemParameters.VirtualScreenLeft,
                SystemParameters.VirtualScreenTop,
                SystemParameters.VirtualScreenWidth,
                SystemParameters.VirtualScreenHeight);
            var titleBarArea = new System.Windows.Rect(
                _settings.WindowLeft, _settings.WindowTop, Width, 40);
            if (virtualScreen.IntersectsWith(titleBarArea))
            {
                Left = _settings.WindowLeft;
                Top = _settings.WindowTop;
            }
        }
    }

    private void SaveWindowBounds()
    {
        if (WindowState == WindowState.Normal)
        {
            _settings.WindowLeft = Left;
            _settings.WindowTop = Top;
            _settings.WindowWidth = Width;
            _settings.WindowHeight = Height;
        }
    }

    private void ApplyEditorFontSize()
    {
        Editor.FontSize = _editorFontSize;
    }

    private DispatcherTimer? _fontSizeHintTimer;

    private void AdjustEditorFontSize(double delta)
    {
        _editorFontSize = Math.Clamp(_editorFontSize + delta, 8, 32);
        Editor.FontSize = _editorFontSize;
        _settings.EditorFontSize = _editorFontSize;
        _settings.Save();
        ShowFontSizeHint();
    }

    private void AdjustPreviewFontSize(double delta)
    {
        _previewFontSize = Math.Clamp(_previewFontSize + delta, 8, 32);
        _settings.PreviewFontSize = _previewFontSize;
        _settings.Save();
        _ = ApplyPreviewFontSizeAsync();
        var prev = StatusPath.Text;
        StatusPath.Text = $"프리뷰 폰트: {(int)_previewFontSize}px";
        _fontSizeHintTimer?.Stop();
        _fontSizeHintTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        _fontSizeHintTimer.Tick += (_, _) => { _fontSizeHintTimer?.Stop(); StatusPath.Text = prev; };
        _fontSizeHintTimer.Start();
    }

    private async Task ApplyPreviewFontSizeAsync()
    {
        if (!_webViewReady) return;
        try
        {
            await Viewer.ExecuteScriptAsync(
                $"document.body.style.fontSize = '{_previewFontSize.ToString(System.Globalization.CultureInfo.InvariantCulture)}px'");
        }
        catch { }
    }

    private void ShowFontSizeHint()
    {
        var prev = _activeIndex >= 0 && _activeIndex < _docs.Count
            ? (_docs[_activeIndex].IsNew ? "새 문서 (저장되지 않음)" : _docs[_activeIndex].FilePath)
            : "파일을 열어주세요";
        StatusPath.Text = $"에디터 폰트: {(int)_editorFontSize}px";
        _fontSizeHintTimer?.Stop();
        _fontSizeHintTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        _fontSizeHintTimer.Tick += (_, _) =>
        {
            _fontSizeHintTimer?.Stop();
            StatusPath.Text = prev;
        };
        _fontSizeHintTimer.Start();
    }

    private void ApplySavedTheme()
    {
        var item = CmbTheme.Items.OfType<ComboBoxItem>()
            .FirstOrDefault(i => i.Tag as string == _currentTheme);
        if (item != null) CmbTheme.SelectedItem = item;
    }

    // ── 로딩 UI ─────────────────────────────────────────────────────────

    private DoubleAnimation? _loadingAnim;
    private long _loadingStartTick;

    private void ShowLoading(string message = "로딩 중...")
    {
        _loadingStartTick = Environment.TickCount64;
        StatusPath.Text = message;
        LoadingBarTrack.Visibility = Visibility.Visible;   // solid 바 즉시 표시
        _loadingAnim = new DoubleAnimation(-120, 1500,
            new Duration(TimeSpan.FromSeconds(1.2)))
        {
            RepeatBehavior = RepeatBehavior.Forever,
            EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
        };
        LoadingBarTranslate.BeginAnimation(TranslateTransform.XProperty, _loadingAnim);
    }

    // WPF가 최소 1프레임 렌더링 후 무거운 작업 시작
    private async Task ShowLoadingAsync(string message = "로딩 중...")
    {
        ShowLoading(message);
        await Task.Delay(16);
    }

    private void HideLoading()
    {
        LoadingBarTranslate.BeginAnimation(TranslateTransform.XProperty, null);
        LoadingBarTrack.Visibility = Visibility.Collapsed;
        // 상태바 경로 텍스트 복원
        if (_activeIndex >= 0 && _activeIndex < _docs.Count)
            StatusPath.Text = _docs[_activeIndex].IsNew ? "새 문서 (저장되지 않음)" : _docs[_activeIndex].FilePath;
        else
            StatusPath.Text = "파일을 열어주세요";
    }

    // 최소 표시 시간 보장 (기본 300ms)
    private async Task HideLoadingAsync(int minMs = 300)
    {
        var elapsed = (int)(Environment.TickCount64 - _loadingStartTick);
        if (elapsed < minMs) await Task.Delay(minMs - elapsed);
        HideLoading();
    }

    // ── WebView2 초기화 ──────────────────────────────────────────────────

    private async void InitWebView()
    {
        ShowLoading("WebView2 초기화 중...");
        try
        {
            var env = await CoreWebView2Environment.CreateAsync();
            await Viewer.EnsureCoreWebView2Async(env);
            Viewer.CoreWebView2.Settings.IsStatusBarEnabled = false;
            Viewer.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
            Viewer.CoreWebView2.NavigationCompleted += OnNavigationCompleted;
            Viewer.CoreWebView2.WebMessageReceived += OnWebViewMessageReceived;
            Viewer.CoreWebView2.NavigationStarting += OnNavigationStarting;
            _webViewReady = true;
            _webViewReadyTcs.TrySetResult();
            HideLoading();
        }
        catch (Exception ex)
        {
            HideLoading();
            MessageBox.Show($"WebView2 초기화 실패:\n{ex.Message}\n\nMicrosoft Edge WebView2 런타임이 설치되어 있는지 확인하세요.",
                "초기화 오류", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void OnNavigationStarting(object? sender, CoreWebView2NavigationStartingEventArgs e)
    {
        var uri = e.Uri;
        if (uri.StartsWith("about:") || uri.StartsWith("blob:")) return;
        if (uri.StartsWith("http://") || uri.StartsWith("https://"))
        {
            e.Cancel = true;
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(uri) { UseShellExecute = true });
        }
        else if (uri.StartsWith("file://"))
        {
            // 파일 URL의 fragment(#) 포함 여부 확인 — 앵커 이동은 허용, 다른 파일은 차단
            var hashIdx = uri.IndexOf('#');
            if (hashIdx > 0)
            {
                e.Cancel = true;
                var id = Uri.UnescapeDataString(uri[(hashIdx + 1)..]);
                _ = Viewer.ExecuteScriptAsync(
                    $"document.getElementById({System.Text.Json.JsonSerializer.Serialize(id)})?.scrollIntoView({{behavior:'smooth'}})");
            }
        }
    }

    private void OnNavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
    {
        _ = HideLoadingAsync(300);
        _ = ExtractTocAsync();
        if (_pendingScrollY > 0)
            _ = RestoreScrollAsync();
        if (_isTocVisible)
            _ = InjectTocScrollMonitorAsync();
        if (_previewFontSize != 15)
            _ = ApplyPreviewFontSizeAsync();
    }

    private async Task InjectTocScrollMonitorAsync()
    {
        await Task.Delay(200); // 렌더링 안정화 대기
        try
        {
            await Viewer.ExecuteScriptAsync(@"
(function() {
    if (window.__tocMonitorInstalled) return;
    window.__tocMonitorInstalled = true;
    var timer = null;
    window.addEventListener('scroll', function() {
        clearTimeout(timer);
        timer = setTimeout(function() {
            var headings = document.querySelectorAll('h1,h2,h3,h4,h5,h6');
            var current = '';
            headings.forEach(function(h) {
                if (h.getBoundingClientRect().top <= 80) current = h.id;
            });
            if (window.chrome && window.chrome.webview)
                window.chrome.webview.postMessage('toc:' + current);
        }, 100);
    }, {passive: true});
})()");
        }
        catch { }
    }

    private void OnWebViewMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        var msg = e.TryGetWebMessageAsString();
        if (msg?.StartsWith("toc:") == true)
        {
            var id = msg[4..];
            HighlightTocEntry(id);
        }
        else if (msg?.StartsWith("open:") == true)
        {
            var url = msg[5..];
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(url) { UseShellExecute = true });
        }
    }

    private void HighlightTocEntry(string id)
    {
        _suppressTocSelection = true;
        for (int i = 0; i < TocList.Items.Count; i++)
        {
            if (TocList.Items[i] is ListBoxItem item && item.Tag as string == id)
            {
                TocList.SelectedIndex = i;
                TocList.ScrollIntoView(item);
                break;
            }
        }
        _suppressTocSelection = false;
    }

    private async Task<double> GetScrollYAsync()
    {
        try
        {
            var result = await Viewer.ExecuteScriptAsync("window.scrollY.toString()");
            if (double.TryParse(result.Trim('"'), System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out var y)) return y;
        }
        catch { }
        return 0;
    }

    private async Task RestoreScrollAsync()
    {
        await Task.Delay(50);
        try
        {
            await Viewer.ExecuteScriptAsync($"window.scrollTo(0, {(int)_pendingScrollY})");
        }
        catch { }
        _pendingScrollY = 0;
    }

    private async Task ExtractTocAsync()
    {
        if (!IsLoaded) return;
        try
        {
            var json = await Viewer.ExecuteScriptAsync(@"
                (function() {
                    var els = document.querySelectorAll('h1,h2,h3,h4,h5,h6');
                    var result = [];
                    els.forEach(function(el) {
                        result.push({level: parseInt(el.tagName[1]), text: el.textContent, id: el.id});
                    });
                    return JSON.stringify(result);
                })()
            ");
            PopulateToc(json);
        }
        catch { }
    }

    private record TocEntry(int Level, string Text, string Id);

    private static readonly System.Text.Json.JsonSerializerOptions _jsonOpts =
        new() { PropertyNameCaseInsensitive = true };

    private void PopulateToc(string json)
    {
        TocList.Items.Clear();
        try
        {
            // ExecuteScriptAsync가 JS 반환값을 JSON 문자열로 래핑 — 먼저 언래핑
            var inner = System.Text.Json.JsonSerializer.Deserialize<string>(json);
            if (string.IsNullOrEmpty(inner)) return;
            var entries = System.Text.Json.JsonSerializer.Deserialize<List<TocEntry>>(inner, _jsonOpts);
            if (entries == null) return;
            foreach (var entry in entries)
            {
                var item = new ListBoxItem
                {
                    Content = entry.Text,
                    Tag = entry.Id,
                    Padding = new Thickness(12 + (entry.Level - 1) * 8, 4, 8, 4),
                    FontSize = entry.Level == 1 ? 13 : entry.Level == 2 ? 12 : 11,
                    FontWeight = entry.Level <= 2 ? FontWeights.SemiBold : FontWeights.Normal,
                };
                TocList.Items.Add(item);
            }
        }
        catch { }
    }

    // ── Loaded 초기화 ────────────────────────────────────────────────────

    private void OnMainWindowLoaded(object sender, RoutedEventArgs e)
    {
        // 에디터 내부 ScrollViewer 탐색 후 스크롤 동기화 구독
        _editorScrollViewer = FindVisualChild<ScrollViewer>(Editor);
        if (_editorScrollViewer != null)
            _editorScrollViewer.ScrollChanged += EditorScrollViewer_ScrollChanged;
        // 커서 위치 추적
        Editor.SelectionChanged += Editor_SelectionChanged;
    }

    private static T? FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
    {
        for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = System.Windows.Media.VisualTreeHelper.GetChild(parent, i);
            if (child is T t) return t;
            var result = FindVisualChild<T>(child);
            if (result != null) return result;
        }
        return null;
    }

    private void EditorScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        if (!_isEditMode || !_webViewReady || _suppressEditorChange) return;
        if (_editorScrollViewer == null || _editorScrollViewer.ScrollableHeight <= 0) return;
        var ratio = _editorScrollViewer.VerticalOffset / _editorScrollViewer.ScrollableHeight;
        _ = Viewer.ExecuteScriptAsync(
            $"(function(){{var h=document.documentElement.scrollHeight-window.innerHeight;" +
            $"if(h>0)window.scrollTo(0,h*{ratio.ToString(System.Globalization.CultureInfo.InvariantCulture)});}})()");
    }

    // ── 프리뷰 타이머 ───────────────────────────────────────────────────

    private void SetupPreviewTimer()
    {
        _previewTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
        _previewTimer.Tick += (_, _) =>
        {
            _previewTimer.Stop();
            RenderPreview(saveScroll: true);
        };
    }

    // ── 자동 저장 ────────────────────────────────────────────────────────

    private void SetupAutoSaveTimer()
    {
        _autoSaveTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(30) };
        _autoSaveTimer.Tick += AutoSave_Tick;
    }

    private async void AutoSave_Tick(object? sender, EventArgs e)
    {
        _autoSaveTimer?.Stop();
        var modified = _docs.Where(d => d.IsModified && !string.IsNullOrEmpty(d.Content)).ToList();
        if (modified.Count == 0) return;

        var autoSaveDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "MarkView", "autosave");
        Directory.CreateDirectory(autoSaveDir);

        await Task.Run(() =>
        {
            foreach (var doc in modified)
            {
                var baseName = doc.IsNew
                    ? "unsaved"
                    : Path.GetFileNameWithoutExtension(doc.FilePath);
                var path = Path.Combine(autoSaveDir, baseName + ".autosave.md");
                File.WriteAllText(path, doc.Content, new UTF8Encoding(true));
            }
        });

        var savedPath = StatusPath.Text;
        var savedTip = StatusPath.ToolTip;
        var autoMsg = $"자동 저장 완료 ({modified.Count}개)";
        StatusPath.Text = autoMsg;
        StatusPath.ToolTip = autoSaveDir;
        await Task.Delay(2000);
        // 다른 핸들러가 텍스트를 변경하지 않은 경우에만 복원
        if (StatusPath.Text == autoMsg)
        {
            StatusPath.Text = savedPath;
            StatusPath.ToolTip = savedTip;
        }
    }

    // ── 탭 관리 ─────────────────────────────────────────────────────────

    private void OpenDocument(MarkDocument doc)
    {
        // 이미 열려있는지 확인
        if (!doc.IsNew)
        {
            var existing = _docs.FindIndex(d => !d.IsNew &&
                string.Equals(d.FilePath, doc.FilePath, StringComparison.OrdinalIgnoreCase));
            if (existing >= 0) { SwitchTo(existing); return; }
        }

        _docs.Add(doc);
        var tab = CreateTabItem(doc, _docs.Count - 1);
        _tabs.Add(tab);
        TabBar.Children.Add(tab);

        SwitchTo(_docs.Count - 1);
        UpdateEmptyState();
    }

    private Border CreateTabItem(MarkDocument doc, int index)
    {
        var tab = new Border
        {
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0, 0, 0, 2),
            BorderBrush = Brushes.Transparent,
            Padding = new Thickness(14, 8, 8, 8),
            Cursor = Cursors.Hand,
            Tag = index,
        };

        var inner = new StackPanel { Orientation = Orientation.Horizontal };

        var title = new TextBlock
        {
            Text = doc.TabTitle,
            FontSize = 12,
            Foreground = (SolidColorBrush)FindResource("TextDimBrush"),
            VerticalAlignment = VerticalAlignment.Center,
            Tag = "title",
        };

        var closeBtn = new TextBlock
        {
            Text = " ×",
            FontSize = 14,
            Foreground = (SolidColorBrush)FindResource("TextDimBrush"),
            VerticalAlignment = VerticalAlignment.Center,
            Cursor = Cursors.Hand,
            Tag = index,
        };
        closeBtn.MouseLeftButtonUp += CloseTab_Click;
        closeBtn.MouseEnter += (s, _) => ((TextBlock)s).Foreground =
            (SolidColorBrush)FindResource("TextBrush");
        closeBtn.MouseLeave += (s, _) => ((TextBlock)s).Foreground =
            (SolidColorBrush)FindResource("TextDimBrush");

        inner.Children.Add(title);
        inner.Children.Add(closeBtn);
        tab.Child = inner;

        tab.MouseLeftButtonDown += Tab_Click;
        tab.MouseDown += Tab_MouseDown;
        tab.MouseEnter += (s, _) =>
        {
            if ((int)((Border)s).Tag != _activeIndex)
                ((Border)s).Background = (SolidColorBrush)FindResource("HoverBrush");
        };
        tab.MouseLeave += (s, _) =>
        {
            if ((int)((Border)s).Tag != _activeIndex)
                ((Border)s).Background = Brushes.Transparent;
        };

        return tab;
    }

    private void Tab_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender is Border b)
            SwitchTo((int)b.Tag);
    }

    private void Tab_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Middle && sender is Border b)
        {
            e.Handled = true;
            CloseTab((int)b.Tag);
        }
    }

    private void CloseTab_Click(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
        if (sender is TextBlock tb)
            CloseTab((int)tb.Tag);
    }

    private async void SwitchTo(int index)
    {
        if (index < 0 || index >= _docs.Count) return;

        // 현재 탭 스크롤 저장
        if (_activeIndex >= 0 && _activeIndex < _docs.Count)
            _docs[_activeIndex].ScrollY = await GetScrollYAsync();

        // 이전 탭 스타일 복원
        if (_activeIndex >= 0 && _activeIndex < _tabs.Count)
        {
            _tabs[_activeIndex].BorderBrush = Brushes.Transparent;
            _tabs[_activeIndex].Background = Brushes.Transparent;
            UpdateTabTitle(_activeIndex);
        }

        _activeIndex = index;
        _pendingScrollY = _docs[index].ScrollY;
        var tab = _tabs[index];
        tab.BorderBrush = (SolidColorBrush)FindResource("AccentBrush");
        if (tab.Child is StackPanel sp && sp.Children[0] is TextBlock t)
            t.Foreground = (SolidColorBrush)FindResource("TextBrush");

        LoadDocumentToUI(_docs[index]);
    }

    private void LoadDocumentToUI(MarkDocument doc)
    {
        _suppressEditorChange = true;
        Editor.Text = doc.Content;
        _suppressEditorChange = false;

        RenderPreview();
        UpdateStatusBar(doc);
        Title = $"Mark.View — {doc.TabTitle}";
    }

    private void UpdateTabTitle(int index)
    {
        if (index < 0 || index >= _tabs.Count) return;
        if (_tabs[index].Child is StackPanel sp && sp.Children[0] is TextBlock t)
        {
            t.Text = _docs[index].TabTitle;
            t.Foreground = index == _activeIndex
                ? (SolidColorBrush)FindResource("TextBrush")
                : (SolidColorBrush)FindResource("TextDimBrush");
        }
    }

    private void CloseTab(int index)
    {
        if (index < 0 || index >= _docs.Count) return;
        var doc = _docs[index];

        if (doc.IsModified)
        {
            var result = MessageBox.Show(
                $"'{doc.FileName}'의 변경 사항을 저장하시겠습니까?",
                "저장 확인",
                MessageBoxButton.YesNoCancel,
                MessageBoxImage.Question);
            if (result == MessageBoxResult.Cancel) return;
            if (result == MessageBoxResult.Yes) SaveDocument(doc);
        }

        if (!doc.IsNew) RemoveFileWatcher(doc.FilePath);
        TabBar.Children.RemoveAt(index);
        _docs.RemoveAt(index);
        _tabs.RemoveAt(index);

        // 인덱스 재매핑
        for (int i = 0; i < _tabs.Count; i++)
        {
            _tabs[i].Tag = i;
            if (_tabs[i].Child is StackPanel sp)
                foreach (var c in sp.Children.OfType<TextBlock>())
                    if (c.Tag is int) c.Tag = i;
        }

        _activeIndex = -1;
        if (_docs.Count > 0)
            SwitchTo(Math.Min(index, _docs.Count - 1));
        else
            UpdateEmptyState();
    }

    private void UpdateEmptyState()
    {
        bool hasDoc = _docs.Count > 0;
        EmptyState.Visibility = hasDoc ? Visibility.Collapsed : Visibility.Visible;
        ContentArea.Visibility = hasDoc ? Visibility.Visible : Visibility.Collapsed;
    }

    // ── 렌더링 ──────────────────────────────────────────────────────────

    private async void RenderPreview(bool saveScroll = false)
    {
        if (!_webViewReady) return;
        if (_activeIndex < 0 || _activeIndex >= _docs.Count) return;
        if (saveScroll)
            _pendingScrollY = await GetScrollYAsync();
        var doc = _docs[_activeIndex];
        var content = doc.Content;
        var filePath = doc.IsNew ? null : doc.FilePath;
        var theme = _currentTheme;

        await ShowLoadingAsync("렌더링 중...");
        var html = await Task.Run(() => _renderer.RenderToHtml(content, filePath, theme));
        Viewer.NavigateToString(html);
        // HideLoading은 OnNavigationCompleted에서 호출됨
    }

    // ── UI 상태 복원 ────────────────────────────────────────────────────

    private void RestoreUiState()
    {
        if (_settings.IsEditMode) SetEditMode(true);
        if (_settings.IsTocVisible) SetTocVisible(true);
    }

    // ── 편집 모드 ────────────────────────────────────────────────────────

    private void SetEditMode(bool editMode)
    {
        _isEditMode = editMode;
        if (editMode)
        {
            var ratio = Math.Clamp(_settings.EditorSplitRatio, 0.2, 0.8);
            EditorColumn.Width = new GridLength(ratio, GridUnitType.Star);
            ViewerColumn.Width = new GridLength(1 - ratio, GridUnitType.Star);
            SplitterColumn.Width = new GridLength(4);
            TxtEditIcon.Foreground = (SolidColorBrush)FindResource("AccentBrush");
            StatusMode.Text = "편집";
            StatusCursor.Visibility = Visibility.Visible;
        }
        else
        {
            EditorColumn.Width = new GridLength(0);
            SplitterColumn.Width = new GridLength(0);
            TxtEditIcon.Foreground = (SolidColorBrush)FindResource("TextDimBrush");
            StatusMode.Text = "뷰";
            StatusCursor.Visibility = Visibility.Collapsed;
        }
        _settings.IsEditMode = editMode;
        _settings.Save();
    }

    // ── TOC ─────────────────────────────────────────────────────────────

    private void SetTocVisible(bool visible)
    {
        _isTocVisible = visible;
        TocPanel.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
        TocSplitter.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
        if (!visible)
        {
            var w = TocColumn.ActualWidth > 0 ? TocColumn.ActualWidth : TocColumn.Width.Value;
            if (w > 0) _settings.TocWidth = w;
            TocColumn.Width = new GridLength(0);
        }
        else
        {
            TocColumn.Width = new GridLength(_settings.TocWidth);
            if (_webViewReady) _ = InjectTocScrollMonitorAsync();
        }
        _settings.IsTocVisible = visible;
        _settings.Save();
    }

    // ── 집중 모드 ────────────────────────────────────────────────────────

    private void SetFocusMode(bool focus)
    {
        _isFocusMode = focus;
        ToolbarBorder.Visibility = focus ? Visibility.Collapsed : Visibility.Visible;
        StatusBarBorder.Visibility = focus ? Visibility.Collapsed : Visibility.Visible;
    }

    // ── 파일 작업 ────────────────────────────────────────────────────────

    private bool SaveDocument(MarkDocument doc)
    {
        if (doc.IsNew) return SaveDocumentAs(doc);
        try
        {
            File.WriteAllText(doc.FilePath, doc.Content, new UTF8Encoding(true));
            doc.IsModified = false;
            int i = _docs.IndexOf(doc);
            UpdateTabTitle(i);
            if (i == _activeIndex)
            {
                Title = $"Mark.View — {doc.TabTitle}";
                UpdateStatusBar(doc);
            }
            return true;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"저장 실패:\n{ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            return false;
        }
    }

    private bool SaveDocumentAs(MarkDocument doc)
    {
        var dlg = new SaveFileDialog
        {
            Filter = "Markdown 파일 (*.md;*.markdown)|*.md;*.markdown|모든 파일 (*.*)|*.*",
            DefaultExt = ".md",
            FileName = doc.FileName,
        };
        if (doc.Directory != null) dlg.InitialDirectory = doc.Directory;
        if (dlg.ShowDialog() != true) return false;

        doc.FilePath = dlg.FileName;
        return SaveDocument(doc);
    }

    // ── 상태바 ──────────────────────────────────────────────────────────

    private void StatusPath_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (_activeIndex < 0 || _activeIndex >= _docs.Count) return;
        var doc = _docs[_activeIndex];
        if (doc.IsNew || !File.Exists(doc.FilePath)) return;
        try
        {
            System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{doc.FilePath}\"");
        }
        catch { }
    }

    private void UpdateStatusBar(MarkDocument doc)
    {
        StatusPath.Text = doc.IsNew ? "새 문서 (저장되지 않음)" : doc.FilePath;
        var lines = doc.Content.Split('\n').Length;
        var words = string.IsNullOrWhiteSpace(doc.Content)
            ? 0
            : Regex.Matches(doc.Content, @"\S+").Count;
        StatusLines.Text = $"{lines}줄";
        StatusWords.Text = $"{words}단어";
        StatusMode.Text = _isEditMode ? "편집" : "뷰";
    }

    // ── 찾기 ────────────────────────────────────────────────────────────

    private void FindInPreview(string keyword)
    {
        if (!_webViewReady) return;
        if (string.IsNullOrEmpty(keyword))
        {
            StatusFind.Visibility = Visibility.Collapsed;
            _ = Viewer.ExecuteScriptAsync("window.getSelection()?.removeAllRanges()");
            return;
        }
        _ = FindInPreviewAsync(keyword, reverse: false);
    }

    private async Task FindInPreviewAsync(string keyword, bool reverse)
    {
        try
        {
            var kw = System.Text.Json.JsonSerializer.Serialize(keyword);
            // 전체 매칭 개수 계산
            var countJson = await Viewer.ExecuteScriptAsync($@"
(function() {{
    var text = document.body.innerText || '';
    var lower = text.toLowerCase();
    var target = {kw}.toLowerCase();
    if (!target) return '0';
    var count = 0, idx = 0;
    while ((idx = lower.indexOf(target, idx)) !== -1) {{ count++; idx += target.length; }}
    return count.toString();
}})()");
            if (int.TryParse(countJson.Trim('"'), out var count))
            {
                StatusFind.Text = count > 0 ? $"{count}개 일치" : "없음";
                StatusFind.Visibility = Visibility.Visible;
            }
            // 방향에 따라 이동 (reverse=true: 역방향)
            var rev = reverse ? "true" : "false";
            await Viewer.ExecuteScriptAsync($"window.find({kw}, false, {rev}, true)");
        }
        catch { }
    }

    // ── 찾기/바꾸기 ─────────────────────────────────────────────────────

    private void ToggleReplaceBar()
    {
        var visible = ReplaceBar.Visibility == Visibility.Visible;
        ReplaceBar.Visibility = visible ? Visibility.Collapsed : Visibility.Visible;
        if (!visible) TxtReplace.Focus();
    }

    private void TxtReplace_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) BtnReplace_Click(sender, new RoutedEventArgs());
        if (e.Key == Key.Escape) ReplaceBar.Visibility = Visibility.Collapsed;
    }

    private void BtnReplace_Click(object sender, RoutedEventArgs e)
    {
        if (_activeIndex < 0 || !_isEditMode) return;
        var find = TxtFind.Text;
        var replace = TxtReplace.Text;
        if (string.IsNullOrEmpty(find)) return;

        var text = Editor.Text;
        var idx = text.IndexOf(find, StringComparison.OrdinalIgnoreCase);
        if (idx < 0)
        {
            ShowReplaceStatus("없음");
            return;
        }
        Editor.Select(idx, find.Length);
        Editor.SelectedText = replace;
        Editor.CaretIndex = idx + replace.Length;
        ShowReplaceStatus("1개 변경");
    }

    private void BtnReplaceAll_Click(object sender, RoutedEventArgs e)
    {
        if (_activeIndex < 0 || !_isEditMode) return;
        var find = TxtFind.Text;
        var replace = TxtReplace.Text;
        if (string.IsNullOrEmpty(find)) return;

        var text = Editor.Text;
        int count = 0;
        var sb = new StringBuilder();
        int pos = 0;
        while (pos <= text.Length)
        {
            var idx = text.IndexOf(find, pos, StringComparison.OrdinalIgnoreCase);
            if (idx < 0) { sb.Append(text.AsSpan(pos)); break; }
            sb.Append(text.AsSpan(pos, idx - pos));
            sb.Append(replace);
            pos = idx + find.Length;
            count++;
        }
        if (count > 0)
        {
            Editor.Text = sb.ToString();
            Editor.CaretIndex = Editor.Text.Length;
        }
        ShowReplaceStatus(count > 0 ? $"{count}개 변경" : "없음");
    }

    private void ShowReplaceStatus(string msg)
    {
        StatusReplace.Text = msg;
        StatusReplace.Visibility = Visibility.Visible;
        var t = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        t.Tick += (_, _) => { t.Stop(); StatusReplace.Visibility = Visibility.Collapsed; };
        t.Start();
    }

    private void BtnFindPrev_Click(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrEmpty(TxtFind.Text))
            _ = FindInPreviewAsync(TxtFind.Text, reverse: true);
    }

    private void BtnFindNext_Click(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrEmpty(TxtFind.Text))
            _ = FindInPreviewAsync(TxtFind.Text, reverse: false);
    }

    // ── 이벤트 핸들러 ────────────────────────────────────────────────────

    private void BtnNew_Click(object sender, RoutedEventArgs e)
    {
        OpenDocument(new MarkDocument());
        if (!_isEditMode) SetEditMode(true);
    }

    private void BtnOpen_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Filter = "Markdown 파일 (*.md;*.markdown)|*.md;*.markdown|텍스트 파일 (*.txt)|*.txt|모든 파일 (*.*)|*.*",
            Multiselect = true,
        };
        if (dlg.ShowDialog() != true) return;
        foreach (var f in dlg.FileNames) OpenFile(f);
    }

    private void BtnSave_Click(object sender, RoutedEventArgs e)
    {
        if (_activeIndex < 0) return;
        SaveDocument(_docs[_activeIndex]);
    }

    private void BtnToggleEdit_Click(object sender, RoutedEventArgs e)
    {
        SetEditMode(!_isEditMode);
    }

    private void BtnToc_Click(object sender, RoutedEventArgs e)
    {
        SetTocVisible(!_isTocVisible);
    }

    private void BtnFocus_Click(object sender, RoutedEventArgs e)
    {
        SetFocusMode(!_isFocusMode);
    }

    private void BtnHelp_Click(object sender, RoutedEventArgs e)
    {
        ShowHelp();
    }

    private void ShowHelp()
    {
        if (_helpWindow != null)
        {
            _helpWindow.Activate();
            return;
        }
        _helpWindow = new HelpWindow { Owner = this };
        _helpWindow.Closed += (_, _) => _helpWindow = null;
        _helpWindow.Show();
    }

    private void EditorSplitter_DragCompleted(object sender, System.Windows.Controls.Primitives.DragCompletedEventArgs e)
    {
        var total = EditorColumn.ActualWidth + ViewerColumn.ActualWidth;
        if (total > 0)
        {
            _settings.EditorSplitRatio = EditorColumn.ActualWidth / total;
            _settings.Save();
        }
    }

    private void TocSplitter_DragCompleted(object sender, System.Windows.Controls.Primitives.DragCompletedEventArgs e)
    {
        if (TocColumn.ActualWidth > 0)
        {
            _settings.TocWidth = TocColumn.ActualWidth;
            _settings.Save();
        }
    }

    private void CmbTheme_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded) return;
        if (CmbTheme.SelectedItem is ComboBoxItem item && item.Tag is string tag)
        {
            _currentTheme = tag;
            _settings.Theme = tag;
            _settings.Save();
            RenderPreview(saveScroll: true);
        }
    }

    private async void BtnReload_Click(object sender, RoutedEventArgs e)
    {
        if (_activeIndex < 0 || _activeIndex >= _docs.Count) return;
        _pendingScrollY = await GetScrollYAsync();
        var doc = _docs[_activeIndex];
        if (!doc.IsNew && File.Exists(doc.FilePath))
        {
            await ShowLoadingAsync("파일 다시 읽기 중...");
            doc.Content = await Task.Run(() => File.ReadAllText(doc.FilePath));
            doc.IsModified = false;
            LoadDocumentToUI(doc);
            UpdateTabTitle(_activeIndex);
        }
        else RenderPreview();
    }

    private async void BtnExportPdf_Click(object sender, RoutedEventArgs e)
    {
        if (_activeIndex < 0 || !_webViewReady) return;
        var doc = _docs[_activeIndex];
        var dlg = new SaveFileDialog
        {
            Filter = "PDF 파일 (*.pdf)|*.pdf",
            DefaultExt = ".pdf",
            FileName = System.IO.Path.GetFileNameWithoutExtension(doc.FileName) + ".pdf",
        };
        if (doc.Directory != null) dlg.InitialDirectory = doc.Directory;
        if (dlg.ShowDialog() != true) return;

        await ShowLoadingAsync("PDF 내보내기 중...");
        try
        {
            await Viewer.CoreWebView2.PrintToPdfAsync(dlg.FileName);
            HideLoading();
            MessageBox.Show($"PDF 내보내기 완료:\n{dlg.FileName}", "완료",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            HideLoading();
            MessageBox.Show($"PDF 내보내기 실패:\n{ex.Message}", "오류",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void BtnExportHtml_Click(object sender, RoutedEventArgs e)
    {
        if (_activeIndex < 0) return;
        var doc = _docs[_activeIndex];
        var dlg = new SaveFileDialog
        {
            Filter = "HTML 파일 (*.html)|*.html",
            DefaultExt = ".html",
            FileName = System.IO.Path.GetFileNameWithoutExtension(doc.FileName) + ".html",
        };
        if (doc.Directory != null) dlg.InitialDirectory = doc.Directory;
        if (dlg.ShowDialog() != true) return;

        await ShowLoadingAsync("HTML 내보내기 중...");
        var content = doc.Content;
        var filePath = doc.IsNew ? null : doc.FilePath;
        var theme = _currentTheme;
        await Task.Run(() =>
        {
            var html = _renderer.RenderToHtml(content, filePath, theme);
            File.WriteAllText(dlg.FileName, html, new UTF8Encoding(true));
        });
        HideLoading();
        MessageBox.Show($"HTML 내보내기 완료:\n{dlg.FileName}", "완료",
            MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void Editor_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_suppressEditorChange || _activeIndex < 0) return;
        var doc = _docs[_activeIndex];
        doc.Content = Editor.Text;
        if (!doc.IsModified)
        {
            doc.IsModified = true;
            UpdateTabTitle(_activeIndex);
            Title = $"Mark.View — {doc.TabTitle}";
        }
        UpdateStatusBar(doc);
        _previewTimer?.Stop();
        _previewTimer?.Start();
        // 자동 저장 타이머 재시작 (30초 비활동 후 저장)
        _autoSaveTimer?.Stop();
        _autoSaveTimer?.Start();
    }

    private void WrapSelection(string before, string after)
    {
        var start = Editor.SelectionStart;
        var sel = Editor.SelectedText;
        var wrapped = before + sel + after;
        Editor.SelectedText = wrapped;
        if (sel.Length == 0)
            Editor.CaretIndex = start + before.Length;
        else
            Editor.CaretIndex = start + wrapped.Length;
    }

    private void WrapAsLink()
    {
        var start = Editor.SelectionStart;
        var sel = Editor.SelectedText;
        var inserted = $"[{sel}]()";
        Editor.SelectedText = inserted;
        // 커서를 () 안쪽에 위치
        Editor.CaretIndex = start + sel.Length + 3;
    }

    private void Editor_SelectionChanged(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded || _activeIndex < 0) return;
        var text = Editor.Text;
        var caret = Editor.CaretIndex;
        if (caret < 0 || caret > text.Length) return;
        var line = text.Take(caret).Count(c => c == '\n') + 1;
        var lastNl = text.LastIndexOf('\n', Math.Max(0, caret - 1));
        var col = caret - (lastNl + 1) + 1;
        StatusCursor.Text = $"{line}:{col}";
        StatusCursor.Visibility = Visibility.Visible;
    }

    private void Editor_KeyDown(object sender, KeyEventArgs e)
    {
        // 서식 단축키 — 에디터 포커스 시에만 적용
        if (e.Key == Key.B && Keyboard.Modifiers == ModifierKeys.Control)
        { WrapSelection("**", "**"); e.Handled = true; return; }
        if (e.Key == Key.I && Keyboard.Modifiers == ModifierKeys.Control)
        { WrapSelection("*", "*"); e.Handled = true; return; }
        if (e.Key == Key.K && Keyboard.Modifiers == ModifierKeys.Control)
        { WrapAsLink(); e.Handled = true; return; }

        if (e.Key != Key.Tab) return;
        e.Handled = true;

        bool shift = Keyboard.Modifiers == ModifierKeys.Shift;
        if (Editor.SelectionLength > 0)
        {
            IndentSelection(shift);
            return;
        }

        int caret = Editor.CaretIndex;
        if (shift)
        {
            // 역들여쓰기: 캐럿 앞 공백 최대 4개 제거
            if (caret > 0 && Editor.Text[caret - 1] == ' ')
            {
                int start = caret - 1;
                int count = 1;
                while (start > 0 && count < 4 && Editor.Text[start - 1] == ' ')
                { start--; count++; }
                Editor.Select(start, count);
                Editor.SelectedText = "";
            }
        }
        else
        {
            Editor.Select(caret, 0);
            Editor.SelectedText = "    ";
            Editor.CaretIndex = caret + 4;
        }
    }

    private void IndentSelection(bool dedent)
    {
        string text = Editor.Text;
        int selStart = Editor.SelectionStart;
        int selEnd = selStart + Editor.SelectionLength;

        // 선택 범위를 줄 경계로 확장
        int lineStart = selStart > 0 ? text.LastIndexOf('\n', selStart - 1) + 1 : 0;

        // selEnd가 줄 시작(\n 직후)이면 그 줄은 제외
        int lastPos = (selEnd > lineStart && selEnd > 0 && selEnd <= text.Length
                       && text[selEnd - 1] == '\n') ? selEnd - 1 : selEnd;
        int lineEnd = lastPos >= text.Length ? text.Length : text.IndexOf('\n', lastPos);
        if (lineEnd < 0) lineEnd = text.Length;
        if (lineEnd < lineStart) lineEnd = lineStart;

        string[] lines = text[lineStart..lineEnd].Split('\n');
        var sb = new StringBuilder();
        for (int i = 0; i < lines.Length; i++)
        {
            if (i > 0) sb.Append('\n');
            if (dedent)
            {
                int spaces = 0;
                while (spaces < 4 && spaces < lines[i].Length && lines[i][spaces] == ' ')
                    spaces++;
                sb.Append(lines[i].AsSpan(spaces));
            }
            else
            {
                sb.Append("    ").Append(lines[i]);
            }
        }

        Editor.Select(lineStart, lineEnd - lineStart);
        Editor.SelectedText = sb.ToString();
        Editor.Select(lineStart, sb.Length);
    }

    private void TxtFind_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (!IsLoaded) return;
        FindInPreview(TxtFind.Text);
    }

    private void TxtFind_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            if (!string.IsNullOrEmpty(TxtFind.Text))
            {
                bool reverse = Keyboard.Modifiers == ModifierKeys.Shift;
                _ = FindInPreviewAsync(TxtFind.Text, reverse);
            }
        }
        if (e.Key == Key.Escape) TxtFind.Text = "";
    }

    private void TocList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded || _suppressTocSelection) return;
        if (TocList.SelectedItem is ListBoxItem item && item.Tag is string id && !string.IsNullOrEmpty(id))
        {
            _ = Viewer.ExecuteScriptAsync(
                $"document.getElementById({System.Text.Json.JsonSerializer.Serialize(id)})?.scrollIntoView({{behavior:'smooth'}})");
        }
    }

    // ── Keyboard Shortcuts (Window level) ───────────────────────────────

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Key == Key.O && Keyboard.Modifiers == ModifierKeys.Control)
        { BtnOpen_Click(this, new RoutedEventArgs()); e.Handled = true; }
        else if (e.Key == Key.S && Keyboard.Modifiers == ModifierKeys.Control)
        { BtnSave_Click(this, new RoutedEventArgs()); e.Handled = true; }
        else if (e.Key == Key.S && Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift))
        { if (_activeIndex >= 0) SaveDocumentAs(_docs[_activeIndex]); e.Handled = true; }
        else if (e.Key == Key.N && Keyboard.Modifiers == ModifierKeys.Control)
        { BtnNew_Click(this, new RoutedEventArgs()); e.Handled = true; }
        else if (e.Key == Key.W && Keyboard.Modifiers == ModifierKeys.Control)
        { if (_activeIndex >= 0) CloseTab(_activeIndex); e.Handled = true; }
        else if (e.Key == Key.E && Keyboard.Modifiers == ModifierKeys.Control)
        { SetEditMode(!_isEditMode); e.Handled = true; }
        else if (e.Key == Key.F5)
        { BtnReload_Click(this, new RoutedEventArgs()); e.Handled = true; }
        else if (e.Key == Key.F && Keyboard.Modifiers == ModifierKeys.Control)
        { TxtFind.Focus(); e.Handled = true; }
        else if (e.Key == Key.B && Keyboard.Modifiers == ModifierKeys.Control)
        { BtnExportHtml_Click(this, new RoutedEventArgs()); e.Handled = true; }
        else if (e.Key == Key.T && Keyboard.Modifiers == ModifierKeys.Control)
        { SetTocVisible(!_isTocVisible); e.Handled = true; }
        else if (e.Key == Key.Tab && Keyboard.Modifiers == ModifierKeys.Control && _docs.Count > 1)
        { SwitchTo((_activeIndex + 1) % _docs.Count); e.Handled = true; }
        else if (e.Key == Key.Tab && Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift) && _docs.Count > 1)
        { SwitchTo((_activeIndex - 1 + _docs.Count) % _docs.Count); e.Handled = true; }
        else if (e.Key == Key.P && Keyboard.Modifiers == ModifierKeys.Control)
        { BtnExportPdf_Click(this, new RoutedEventArgs()); e.Handled = true; }
        else if (e.Key == Key.OemPlus && Keyboard.Modifiers == ModifierKeys.Control)
        { AdjustEditorFontSize(1); e.Handled = true; }
        else if (e.Key == Key.OemMinus && Keyboard.Modifiers == ModifierKeys.Control)
        { AdjustEditorFontSize(-1); e.Handled = true; }
        else if (e.Key == Key.F11)
        { SetFocusMode(!_isFocusMode); e.Handled = true; }
        else if (e.Key == Key.F1)
        { ShowHelp(); e.Handled = true; }
        else if (e.Key == Key.H && Keyboard.Modifiers == ModifierKeys.Control)
        { ToggleReplaceBar(); e.Handled = true; }
        else if (e.Key == Key.V && Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift))
        { PasteClipboardAsNewTab(); e.Handled = true; }
        else if (e.Key == Key.Escape && !string.IsNullOrEmpty(TxtFind.Text))
        { TxtFind.Text = ""; e.Handled = true; }
    }

    protected override void OnPreviewMouseWheel(MouseWheelEventArgs e)
    {
        base.OnPreviewMouseWheel(e);
        // Ctrl+Wheel: 에디터 영역 위에서만 폰트 크기 조절
        // WebView2(뷰어) 위에서는 가로채지 않음
        if (Keyboard.Modifiers == ModifierKeys.Control && IsMouseOverEditor())
        {
            AdjustEditorFontSize(e.Delta > 0 ? 1 : -1);
            e.Handled = true;
        }
        else if (Keyboard.Modifiers == ModifierKeys.Control && IsMouseOverViewer())
        {
            AdjustPreviewFontSize(e.Delta > 0 ? 1 : -1);
            e.Handled = true;
        }
    }

    private void PasteClipboardAsNewTab()
    {
        var text = Clipboard.GetText();
        if (string.IsNullOrEmpty(text)) return;
        var doc = new MarkDocument { Content = text };
        OpenDocument(doc);
        if (!_isEditMode) SetEditMode(true);
    }

    private bool IsMouseOverEditor()
    {
        var pos = Mouse.GetPosition(Editor);
        return pos.X >= 0 && pos.Y >= 0 && pos.X <= Editor.ActualWidth && pos.Y <= Editor.ActualHeight;
    }

    private bool IsMouseOverViewer()
    {
        var pos = Mouse.GetPosition(Viewer);
        return pos.X >= 0 && pos.Y >= 0 && pos.X <= Viewer.ActualWidth && pos.Y <= Viewer.ActualHeight;
    }

    // ── 드래그 앤 드롭 ───────────────────────────────────────────────────

    private void Window_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop)
            ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private void Window_Drop(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return;
        var files = (string[])e.Data.GetData(DataFormats.FileDrop);
        foreach (var f in files)
            if (File.Exists(f)) OpenFile(f);
    }

    private void OpenFile(string path) => _ = OpenFileAsync(path);

    private async Task OpenFileAsync(string path)
    {
        await ShowLoadingAsync("파일 열기 중...");
        try
        {
            var content = await Task.Run(() => File.ReadAllText(path));
            OpenDocument(new MarkDocument { FilePath = path, Content = content });
            _settings.AddRecentFile(path);
            _settings.Save();
            RefreshRecentList();
            AddFileWatcher(path);
        }
        catch (Exception ex)
        {
            HideLoading();
            MessageBox.Show($"파일 열기 실패:\n{ex.Message}", "오류",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
        // 정상 경로는 RenderPreview → NavigationCompleted 에서 HideLoading 호출
    }

    // ── 파일 변경 감지 ───────────────────────────────────────────────────

    private void AddFileWatcher(string filePath)
    {
        if (_fileWatchers.ContainsKey(filePath)) return;
        var dir = Path.GetDirectoryName(filePath);
        if (dir == null) return;
        var watcher = new FileSystemWatcher(dir, Path.GetFileName(filePath))
        {
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size,
            EnableRaisingEvents = true,
        };
        watcher.Changed += OnWatchedFileChanged;
        _fileWatchers[filePath] = watcher;
    }

    private void RemoveFileWatcher(string filePath)
    {
        if (_fileWatchers.TryGetValue(filePath, out var watcher))
        {
            watcher.Dispose();
            _fileWatchers.Remove(filePath);
        }
    }

    private DispatcherTimer? _fileChangedDebounce;

    private void OnWatchedFileChanged(object sender, FileSystemEventArgs e)
    {
        Dispatcher.Invoke(() =>
        {
            _fileChangedDebounce?.Stop();
            _fileChangedDebounce = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
            _fileChangedDebounce.Tick += (_, _) =>
            {
                _fileChangedDebounce?.Stop();
                NotifyFileChanged(e.FullPath);
            };
            _fileChangedDebounce.Start();
        });
    }

    private void NotifyFileChanged(string path)
    {
        var idx = _docs.FindIndex(d => string.Equals(d.FilePath, path, StringComparison.OrdinalIgnoreCase));
        if (idx < 0) return;

        var doc = _docs[idx];
        if (doc.IsModified)
        {
            var result = MessageBox.Show(
                $"'{doc.FileName}' 파일이 외부에서 변경되었습니다.\n수정 중인 내용을 버리고 다시 불러오시겠습니까?",
                "파일 변경 감지",
                MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result != MessageBoxResult.Yes) return;
        }

        _ = ReloadDocumentAsync(idx);
    }

    private async Task ReloadDocumentAsync(int index)
    {
        if (index < 0 || index >= _docs.Count) return;
        var doc = _docs[index];
        if (doc.IsNew || !File.Exists(doc.FilePath)) return;

        await Task.Delay(100); // 파일 락 해제 대기
        try
        {
            doc.Content = await Task.Run(() => File.ReadAllText(doc.FilePath));
            doc.IsModified = false;
            if (index == _activeIndex)
                LoadDocumentToUI(doc);
            UpdateTabTitle(index);
        }
        catch (Exception ex)
        {
            if (index == _activeIndex)
                StatusPath.Text = $"파일 읽기 실패: {ex.Message}";
        }
    }

    // ── 최근 파일 ────────────────────────────────────────────────────────

    private record RecentFileItem(string FileName, string Directory, string FullPath, bool IsPinned)
    {
        public string PinIcon => IsPinned ? "📌" : "○";
    }

    private void RefreshRecentList()
    {
        var validRecent = _settings.RecentFiles.Where(File.Exists).ToList();
        if (validRecent.Count != _settings.RecentFiles.Count)
        {
            _settings.RecentFiles = validRecent;
            _settings.Save();
        }
        var validPinned = _settings.PinnedFiles.Where(File.Exists).ToList();
        if (validPinned.Count != _settings.PinnedFiles.Count)
        {
            _settings.PinnedFiles = validPinned;
            _settings.Save();
        }

        // 핀된 파일을 상단에, 나머지 최근 파일 하단에 표시
        var pinned = validPinned
            .Select(p => new RecentFileItem(
                System.IO.Path.GetFileName(p),
                System.IO.Path.GetDirectoryName(p) ?? "",
                p, true));
        var rest = validRecent
            .Where(p => !_settings.IsPinned(p))
            .Select(p => new RecentFileItem(
                System.IO.Path.GetFileName(p),
                System.IO.Path.GetDirectoryName(p) ?? "",
                p, false));

        var all = pinned.Concat(rest).ToList();
        if (all.Count == 0)
        {
            RecentFilesPanel.Visibility = Visibility.Collapsed;
            return;
        }
        RecentFilesList.ItemsSource = all;
        RecentFilesPanel.Visibility = Visibility.Visible;
    }

    private void PinFile_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string path)
        {
            _settings.TogglePin(path);
            _settings.Save();
            RefreshRecentList();
        }
    }

    private void RecentFile_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (sender is Border b && b.DataContext is RecentFileItem item)
            OpenFile(item.FullPath);
    }

    private void RecentFile_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (sender is Border b)
            b.Background = (System.Windows.Media.SolidColorBrush)FindResource("HoverBrush");
    }

    private void RecentFile_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (sender is Border b)
            b.Background = System.Windows.Media.Brushes.Transparent;
    }

    // ── 종료 ────────────────────────────────────────────────────────────

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        base.OnClosing(e);
        var modified = _docs.Where(d => d.IsModified).ToList();
        if (modified.Count == 0)
        {
            SaveWindowBounds();
            SaveSession();
            _settings.Save();
            foreach (var w in _fileWatchers.Values) w.Dispose();
            _fileWatchers.Clear();
            return;
        }

        var names = string.Join("\n  • ", modified.Select(d => d.FileName));
        var result = MessageBox.Show(
            $"저장되지 않은 파일이 있습니다:\n  • {names}\n\n저장 후 종료하시겠습니까?",
            "종료 확인",
            MessageBoxButton.YesNoCancel,
            MessageBoxImage.Question);
        if (result == MessageBoxResult.Cancel)
        {
            e.Cancel = true;
            return;
        }
        if (result == MessageBoxResult.Yes)
        {
            foreach (var doc in modified)
                SaveDocument(doc);
        }
        SaveWindowBounds();
        SaveSession();
        _settings.Save();
        foreach (var w in _fileWatchers.Values) w.Dispose();
        _fileWatchers.Clear();
    }

    private void SaveSession()
    {
        _settings.OpenFiles = _docs
            .Where(d => !d.IsNew && File.Exists(d.FilePath))
            .Select(d => d.FilePath)
            .ToList();
        _settings.ActiveTabIndex = _activeIndex >= 0 ? _activeIndex : 0;
    }
}
