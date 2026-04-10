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
    private bool _caseSensitive;
    private HelpWindow? _helpWindow;
    private DispatcherTimer? _previewTimer;
    private Point _tabDragStart;

    private DispatcherTimer? _autoSaveTimer;
    private string _currentTheme = "dark";
    private double _pendingScrollY = 0;
    private bool _webViewReady = false;
    private readonly TaskCompletionSource _webViewReadyTcs = new();
    private readonly Dictionary<string, FileSystemWatcher> _fileWatchers = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _recentlySavedPaths = new(StringComparer.OrdinalIgnoreCase);
    private double _editorFontSize = 13;
    private double _previewFontSize = 15;
    private int _findMatchCount = 0;
    private int _findCurrentIndex = 0;
    private List<RecentFileItem> _allRecentItems = [];

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
        // WebView2 초기화 완료 이벤트 대기 (최대 3초)
        await Task.WhenAny(_webViewReadyTcs.Task, Task.Delay(3000));
        foreach (var f in files)
            await OpenFileAsync(f);
        // 마지막 활성 탭 복원
        var savedIdx = _settings.ActiveTabIndex;
        if (savedIdx >= 0 && savedIdx < _docs.Count)
            SwitchTo(savedIdx);
        // 자동 저장 파일 감지 → 상태바 복구 알림
        CheckAutosaveFiles();
    }

    private void CheckAutosaveFiles()
    {
        var autoSaveDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "MarkView", "autosave");
        if (!Directory.Exists(autoSaveDir)) return;
        var autosaves = Directory.GetFiles(autoSaveDir, "*.autosave.md");
        if (autosaves.Length == 0) return;

        // 차단 메시지박스 대신 상태바에 클릭 가능한 알림 표시
        var prevTip = StatusPath.ToolTip;
        StatusPath.Text = $"⚠ 자동 저장 파일 {autosaves.Length}개 — 클릭하여 폴더 열기";
        StatusPath.ToolTip = $"자동 저장 위치: {autoSaveDir}\n(클릭하면 탐색기에서 엽니다)";
        StatusPath.Tag = autoSaveDir;
        StatusPath.MouseLeftButtonDown -= StatusPath_MouseLeftButtonDown;
        StatusPath.MouseLeftButtonDown += AutosaveStatus_Click;

        var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(10) };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            if (StatusPath.Tag as string == autoSaveDir)
            {
                StatusPath.Tag = null;
                StatusPath.MouseLeftButtonDown -= AutosaveStatus_Click;
                StatusPath.MouseLeftButtonDown += StatusPath_MouseLeftButtonDown;
                StatusPath.ToolTip = prevTip;
                if (_activeIndex >= 0 && _activeIndex < _docs.Count)
                    StatusPath.Text = _docs[_activeIndex].IsNew ? "새 문서 (저장되지 않음)" : _docs[_activeIndex].FilePath;
                else
                    StatusPath.Text = "파일을 열어주세요";
            }
        };
        timer.Start();
    }

    private void AutosaveStatus_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (StatusPath.Tag is string dir && Directory.Exists(dir))
        {
            try { System.Diagnostics.Process.Start("explorer.exe", dir); }
            catch { }
        }
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
        if (_settings.IsMaximized)
            WindowState = WindowState.Maximized;
    }

    private void SaveWindowBounds()
    {
        _settings.IsMaximized = WindowState == WindowState.Maximized;
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


    private void AdjustEditorFontSize(double delta)
    {
        _editorFontSize = Math.Clamp(_editorFontSize + delta, 8, 32);
        Editor.FontSize = _editorFontSize;
        _settings.EditorFontSize = _editorFontSize;
        _settings.Save();
        ShowFontSizeHint();
    }

    private void ResetFontSize()
    {
        _editorFontSize = 13;
        _previewFontSize = 15;
        Editor.FontSize = _editorFontSize;
        _settings.EditorFontSize = _editorFontSize;
        _settings.PreviewFontSize = _previewFontSize;
        _settings.Save();
        _ = ApplyPreviewFontSizeAsync();
        ShowStatusHint("폰트 크기 초기화 (에디터 13px · 프리뷰 15px)", 2);
    }

    private void AdjustPreviewFontSize(double delta)
    {
        _previewFontSize = Math.Clamp(_previewFontSize + delta, 8, 32);
        _settings.PreviewFontSize = _previewFontSize;
        _settings.Save();
        _ = ApplyPreviewFontSizeAsync();
        ShowStatusHint($"프리뷰 폰트: {(int)_previewFontSize}px", 2);
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
        ShowStatusHint($"에디터 폰트: {(int)_editorFontSize}px", 2);
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
            Viewer.CoreWebView2.ContextMenuRequested += OnWebViewContextMenuRequested;
            // 로컬 CDN 캐시 폴더를 가상 호스트로 서빙 (오프라인 지원)
            Directory.CreateDirectory(CdnCache.CacheDir);
            Viewer.CoreWebView2.SetVirtualHostNameToFolderMapping(
                CdnCache.VirtualHost, CdnCache.CacheDir,
                Microsoft.Web.WebView2.Core.CoreWebView2HostResourceAccessKind.Allow);
            _ = CdnCache.WarmupAsync();
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
            // 외부 링크는 시스템 브라우저로 열기
            e.Cancel = true;
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(uri) { UseShellExecute = true });
        }
        else if (uri.StartsWith("file://"))
        {
            e.Cancel = true;
            // .md/.markdown 파일 링크는 앱 내 새 탭으로 열기
            try
            {
                var localPath = Uri.UnescapeDataString(new Uri(uri).LocalPath);
                var ext = System.IO.Path.GetExtension(localPath).ToLowerInvariant();
                if ((ext == ".md" || ext == ".markdown") && File.Exists(localPath))
                    OpenFile(localPath);
            }
            catch { }
        }
    }

    private void OnNavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
    {
        _ = HideLoadingAsync(300);
        _ = ExtractTocAsync();
        _ = InjectCopyButtonsAsync();
        _ = InjectImageLightboxAsync();
        _ = InjectSearchHighlightFunctionAsync();
        if (_pendingScrollY > 0)
            _ = RestoreScrollAsync();
        if (_isTocVisible)
            _ = InjectTocScrollMonitorAsync();
        if (_previewFontSize != 15)
            _ = ApplyPreviewFontSizeAsync();
        // 현재 검색어가 있으면 하이라이트 복원
        if (!string.IsNullOrEmpty(TxtFind.Text))
            _ = ApplySearchHighlightAsync(TxtFind.Text);
    }

    private async Task InjectCopyButtonsAsync()
    {
        if (!_webViewReady) return;
        try
        {
            await Viewer.ExecuteScriptAsync(@"
(function() {
    if (window.__copyBtnsInstalled) return;
    window.__copyBtnsInstalled = true;
    document.querySelectorAll('pre').forEach(function(pre) {
        var btn = document.createElement('button');
        btn.textContent = 'Copy';
        btn.style.cssText = [
            'position:absolute','top:8px','right:8px',
            'padding:2px 8px','font-size:11px','border-radius:4px',
            'border:1px solid var(--border,#555)',
            'background:var(--surface,#2a2a3a)','color:var(--text-dim,#ccc)',
            'cursor:pointer','opacity:0','transition:opacity 0.15s',
            'font-family:inherit','line-height:1.5'
        ].join(';');
        pre.style.position = 'relative';
        pre.addEventListener('mouseenter', function() { btn.style.opacity = '1'; });
        pre.addEventListener('mouseleave', function() { btn.style.opacity = '0'; });
        btn.addEventListener('click', function(e) {
            e.stopPropagation();
            var code = pre.querySelector('code');
            var text = code ? code.innerText : pre.innerText;
            navigator.clipboard.writeText(text).then(function() {
                btn.textContent = 'Copied!';
                btn.style.color = 'var(--h1,#a78bfa)';
                setTimeout(function() { btn.textContent = 'Copy'; btn.style.color = 'var(--text-dim,#ccc)'; }, 1500);
            }).catch(function() {
                btn.textContent = 'Error';
                setTimeout(function() { btn.textContent = 'Copy'; }, 1500);
            });
        });
        pre.appendChild(btn);
    });
})()");
        }
        catch { }
    }

    private async Task InjectImageLightboxAsync()
    {
        if (!_webViewReady) return;
        try
        {
            await Viewer.ExecuteScriptAsync(@"
(function() {
    if (window.__lightboxInstalled) return;
    window.__lightboxInstalled = true;
    var style = document.createElement('style');
    style.textContent = [
        '#__lb{display:none;position:fixed;inset:0;background:rgba(0,0,0,0.82);z-index:99999;',
        'align-items:center;justify-content:center;cursor:zoom-out;}',
        '#__lb.open{display:flex;}',
        '#__lb img{max-width:92vw;max-height:92vh;border-radius:8px;',
        'box-shadow:0 8px 40px rgba(0,0,0,0.6);pointer-events:none;transition:opacity 0.15s;}',
        '#__lb-prev,#__lb-next{position:absolute;top:50%;transform:translateY(-50%);',
        'background:rgba(255,255,255,0.12);border:none;color:#fff;font-size:24px;',
        'width:44px;height:64px;border-radius:6px;cursor:pointer;display:none;',
        'align-items:center;justify-content:center;z-index:100000;',
        'transition:background 0.15s;}',
        '#__lb-prev{left:16px;}#__lb-next{right:16px;}',
        '#__lb-prev:hover,#__lb-next:hover{background:rgba(255,255,255,0.28);}',
        '#__lb-counter{position:absolute;bottom:16px;left:50%;transform:translateX(-50%);',
        'color:rgba(255,255,255,0.7);font-size:13px;pointer-events:none;display:none;}',
        'article img,body>*:not(#__lb) img{cursor:zoom-in;}'
    ].join('');
    document.head.appendChild(style);
    var overlay = document.createElement('div');
    overlay.id = '__lb';
    var img = document.createElement('img');
    var btnPrev = document.createElement('button');
    btnPrev.id = '__lb-prev'; btnPrev.textContent = '❮';
    var btnNext = document.createElement('button');
    btnNext.id = '__lb-next'; btnNext.textContent = '❯';
    var counter = document.createElement('div');
    counter.id = '__lb-counter';
    overlay.appendChild(btnPrev); overlay.appendChild(img);
    overlay.appendChild(btnNext); overlay.appendChild(counter);
    document.body.appendChild(overlay);
    var imgs = [], cur = 0;
    function open(idx) {
        cur = idx;
        img.src = imgs[cur].src; img.alt = imgs[cur].alt;
        overlay.classList.add('open'); document.body.style.overflow = 'hidden';
        var show = imgs.length > 1;
        btnPrev.style.display = show ? 'flex' : 'none';
        btnNext.style.display = show ? 'flex' : 'none';
        counter.style.display = show ? 'block' : 'none';
        if (show) counter.textContent = (cur+1) + ' / ' + imgs.length;
    }
    function close() { overlay.classList.remove('open'); document.body.style.overflow = ''; }
    overlay.addEventListener('click', function(e) {
        if (e.target === overlay || e.target === img) close();
    });
    btnPrev.addEventListener('click', function(e) {
        e.stopPropagation(); open((cur - 1 + imgs.length) % imgs.length);
    });
    btnNext.addEventListener('click', function(e) {
        e.stopPropagation(); open((cur + 1) % imgs.length);
    });
    document.addEventListener('keydown', function(e) {
        if (!overlay.classList.contains('open')) return;
        if (e.key === 'Escape') close();
        else if (e.key === 'ArrowLeft') { e.preventDefault(); open((cur-1+imgs.length)%imgs.length); }
        else if (e.key === 'ArrowRight') { e.preventDefault(); open((cur+1)%imgs.length); }
    });
    imgs = Array.from(document.querySelectorAll('article img, body>*:not(#__lb) img'));
    imgs.forEach(function(i, idx) {
        i.addEventListener('click', function(e) { e.stopPropagation(); open(idx); });
    });
})()");
        }
        catch { }
    }

    private async Task InjectSearchHighlightFunctionAsync()
    {
        if (!_webViewReady) return;
        try
        {
            await Viewer.ExecuteScriptAsync(@"
(function() {
    window.__searchHighlight = function(keyword, caseSensitive) {
        // 기존 하이라이트 제거
        document.querySelectorAll('.__shl').forEach(function(el) {
            var parent = el.parentNode;
            parent.replaceChild(document.createTextNode(el.textContent), el);
            parent.normalize();
        });
        if (!keyword) { window.__shlCount = 0; return 0; }
        var target = caseSensitive ? keyword : keyword.toLowerCase();
        var walk = document.createTreeWalker(document.body, NodeFilter.SHOW_TEXT, {
            acceptNode: function(node) {
                var p = node.parentNode;
                if (!p) return NodeFilter.FILTER_REJECT;
                var tag = p.tagName ? p.tagName.toLowerCase() : '';
                if (tag === 'script' || tag === 'style' || tag === 'textarea') return NodeFilter.FILTER_REJECT;
                return NodeFilter.FILTER_ACCEPT;
            }
        });
        var nodes = [];
        while (walk.nextNode()) nodes.push(walk.currentNode);
        var count = 0;
        nodes.forEach(function(node) {
            var text = node.nodeValue;
            if (!text) return;
            var searchText = caseSensitive ? text : text.toLowerCase();
            var idx = searchText.indexOf(target);
            if (idx < 0) return;
            var frag = document.createDocumentFragment();
            var pos = 0;
            while (idx >= 0) {
                if (idx > pos) frag.appendChild(document.createTextNode(text.slice(pos, idx)));
                var span = document.createElement('span');
                span.className = '__shl';
                span.dataset.idx = count;
                span.style.cssText = 'background:#FFD700;color:#1a1a1a;border-radius:2px;padding:0 1px;transition:background 0.15s;';
                span.textContent = text.slice(idx, idx + keyword.length);
                frag.appendChild(span);
                count++;
                pos = idx + keyword.length;
                idx = searchText.indexOf(target, pos);
            }
            if (pos < text.length) frag.appendChild(document.createTextNode(text.slice(pos)));
            node.parentNode.replaceChild(frag, node);
        });
        window.__shlCount = count;
        return count;
    };
    window.__searchFocus = function(idx) {
        var els = document.querySelectorAll('.__shl');
        els.forEach(function(el) { el.style.background = '#FFD700'; el.style.color = '#1a1a1a'; });
        if (idx >= 0 && idx < els.length) {
            els[idx].style.background = '#FF6B35';
            els[idx].style.color = '#FFFFFF';
            els[idx].scrollIntoView({behavior:'smooth',block:'center'});
        }
    };
})()");
        }
        catch { }
    }

    private async Task ApplySearchHighlightAsync(string keyword)
    {
        if (!_webViewReady) return;
        try
        {
            var kw = System.Text.Json.JsonSerializer.Serialize(keyword);
            var cs = _caseSensitive ? "true" : "false";
            var countJson = await Viewer.ExecuteScriptAsync($"window.__searchHighlight ? window.__searchHighlight({kw}, {cs}) : 0");
            if (int.TryParse(countJson.Trim('"'), out var count))
            {
                _findMatchCount = count;
                if (count > 0)
                {
                    _findCurrentIndex = 1;
                    StatusFind.Text = $"1 / {count}";
                    StatusFind.Visibility = Visibility.Visible;
                    await Viewer.ExecuteScriptAsync("window.__searchFocus && window.__searchFocus(0)");
                }
                else
                {
                    _findCurrentIndex = 0;
                    StatusFind.Text = "없음";
                    StatusFind.Visibility = Visibility.Visible;
                }
            }
        }
        catch { }
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

    private void OnWebViewContextMenuRequested(object? sender, Microsoft.Web.WebView2.Core.CoreWebView2ContextMenuRequestedEventArgs e)
    {
        e.Handled = true; // 기본 메뉴 억제
        var hasSelection = e.ContextMenuTarget.HasSelection;
        var linkUri = e.ContextMenuTarget.HasLinkUri ? e.ContextMenuTarget.LinkUri : null;
        var srcUri  = e.ContextMenuTarget.HasSourceUri ? e.ContextMenuTarget.SourceUri : null;

        var menu = new ContextMenu();

        if (hasSelection)
        {
            var copyItem = new MenuItem { Header = "텍스트 복사" };
            copyItem.Click += async (_, _) =>
            {
                try
                {
                    var txt = await Viewer.ExecuteScriptAsync("window.getSelection().toString()");
                    var clean = System.Text.Json.JsonSerializer.Deserialize<string>(txt) ?? "";
                    if (!string.IsNullOrEmpty(clean)) Clipboard.SetText(clean);
                }
                catch { }
            };
            menu.Items.Add(copyItem);
        }

        if (!string.IsNullOrEmpty(linkUri))
        {
            var openItem = new MenuItem { Header = "링크 열기" };
            openItem.Click += (_, _) =>
            {
                try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(linkUri) { UseShellExecute = true }); }
                catch { }
            };
            menu.Items.Add(openItem);

            var copyLinkItem = new MenuItem { Header = "링크 주소 복사" };
            copyLinkItem.Click += (_, _) => { try { Clipboard.SetText(linkUri); } catch { } };
            menu.Items.Add(copyLinkItem);
        }

        if (!string.IsNullOrEmpty(srcUri))
        {
            var saveImgItem = new MenuItem { Header = "이미지 저장..." };
            saveImgItem.Click += async (_, _) =>
            {
                try
                {
                    var dlg = new Microsoft.Win32.SaveFileDialog
                    {
                        FileName = System.IO.Path.GetFileName(new Uri(srcUri).LocalPath),
                        Filter = "이미지 파일|*.png;*.jpg;*.jpeg;*.gif;*.webp;*.svg;*.bmp|모든 파일|*.*",
                    };
                    if (dlg.ShowDialog() != true) return;
                    if (srcUri.StartsWith("file://", StringComparison.OrdinalIgnoreCase))
                    {
                        var localPath = new Uri(srcUri).LocalPath;
                        if (File.Exists(localPath))
                        {
                            File.Copy(localPath, dlg.FileName, overwrite: true);
                            return;
                        }
                    }
                    using var client = new System.Net.Http.HttpClient();
                    var data = await client.GetByteArrayAsync(srcUri);
                    await System.IO.File.WriteAllBytesAsync(dlg.FileName, data);
                }
                catch { }
            };
            menu.Items.Add(saveImgItem);
        }

        if (menu.Items.Count == 0)
        {
            var selectAllItem = new MenuItem { Header = "모두 선택" };
            selectAllItem.Click += async (_, _) =>
            {
                try { await Viewer.ExecuteScriptAsync("document.execCommand('selectAll')"); }
                catch { }
            };
            menu.Items.Add(selectAllItem);
        }

        menu.PlacementTarget = Viewer;
        menu.Placement = System.Windows.Controls.Primitives.PlacementMode.MousePoint;
        menu.IsOpen = true;
    }

    private void OnWebViewMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        var msg = e.TryGetWebMessageAsString();
        if (msg?.StartsWith("toc:") == true)
        {
            var id = msg[4..];
            HighlightTocEntry(id);
        }
        else if (msg?.StartsWith("anchor:") == true)
        {
            var id = msg[7..];
            var jsId = System.Text.Json.JsonSerializer.Serialize(id);
            // ID 불일치 대비: 정확 매칭 → 연속 하이픈 정규화 매칭 → 헤딩 텍스트 기반 매칭 순으로 시도
            // norm: Unicode letter/number만 남기고 나머지(이모지·+·.·-·공백 등) 모두 제거 → 범용 비교
            var script = $@"
(function() {{
    window.__sp && window.__sp();
    var id = {jsId};
    var el = document.getElementById(id);
    if (el) {{ el.scrollIntoView({{behavior:'smooth'}}); return; }}
    var normalized = id.replace(/-{{2,}}/g, '-').replace(/^-|-$/g, '');
    el = document.getElementById(normalized);
    if (el) {{ el.scrollIntoView({{behavior:'smooth'}}); return; }}
    var norm = function(s) {{ return s.replace(/[^\p{{L}}\p{{N}}]/gu, '').toLowerCase(); }};
    var idNorm = norm(id);
    var headings = document.querySelectorAll('h1,h2,h3,h4,h5,h6');
    for (var h of headings) {{
        if (norm(h.textContent) === idNorm) {{
            h.scrollIntoView({{behavior:'smooth'}}); return;
        }}
    }}
}})()";
            _ = Viewer.ExecuteScriptAsync(script);
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
                    Padding = new Thickness(12 + (entry.Level - 1) * 10, 4, 8, 4),
                    FontSize = entry.Level == 1 ? 13 : entry.Level == 2 ? 12 : 11,
                    FontWeight = entry.Level == 1 ? FontWeights.Bold : entry.Level == 2 ? FontWeights.SemiBold : FontWeights.Normal,
                    Foreground = entry.Level == 1
                        ? (SolidColorBrush)FindResource("AccentBrush")
                        : entry.Level == 2
                            ? (SolidColorBrush)FindResource("TextBrush")
                            : (SolidColorBrush)FindResource("TextDimBrush"),
                    Opacity = entry.Level >= 4 ? 0.8 : 1.0,
                };
                TocList.Items.Add(item);
            }
        }
        catch { }
        // 검색어가 있으면 새로 로드된 TOC에도 필터 적용
        if (!string.IsNullOrEmpty(TxtTocSearch?.Text))
            FilterToc(TxtTocSearch.Text);
    }

    private void FilterToc(string keyword)
    {
        foreach (var item in TocList.Items.OfType<ListBoxItem>())
        {
            item.Visibility = string.IsNullOrEmpty(keyword) ||
                              item.Content?.ToString()?.Contains(keyword, StringComparison.OrdinalIgnoreCase) == true
                ? Visibility.Visible
                : Visibility.Collapsed;
        }
    }

    private void TxtTocSearch_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (!IsLoaded) return;
        FilterToc(TxtTocSearch.Text);
    }

    // ── Loaded 초기화 ────────────────────────────────────────────────────

    private void OnMainWindowLoaded(object sender, RoutedEventArgs e)
    {
        // ScrollChangedEvent를 Editor 요소에 직접 구독 — ContentArea.Visibility=Collapsed 상태에서도 작동
        // (내부 ScrollViewer의 ScrollChanged가 Editor 방향으로 버블링됨)
        Editor.AddHandler(ScrollViewer.ScrollChangedEvent,
            new ScrollChangedEventHandler(EditorScrollViewer_ScrollChanged));
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
        // 줄 번호 스크롤 동기화
        if (LineNumColumn.Width.Value > 0)
            SyncLineNumberOffset();

        // 뷰어 스크롤 동기화 (편집 모드에서만)
        if (!_isEditMode || !_webViewReady || _suppressEditorChange) return;
        if (e.ExtentHeight <= e.ViewportHeight) return;
        _ = SyncScrollToViewerAsync(e);
    }

    private async Task SyncScrollToViewerAsync(ScrollChangedEventArgs e)
    {
        // 에디터 상단에 보이는 문자 인덱스 → 줄 번호 계산
        var text = Editor.Text;
        if (string.IsNullOrEmpty(text)) return;
        int topCharIdx = Editor.GetCharacterIndexFromPoint(new Point(Editor.Padding.Left + 1, 1), snapToText: true);
        int topLine = CountNewlinesBefore(text, topCharIdx);

        // 에디터 텍스트에서 헤딩 줄 목록 추출 (0-based)
        var headingLines = new List<int>();
        var lines = text.Split('\n');
        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i].AsSpan().TrimStart();
            if (line.StartsWith("#") && line.Length > 1)
            {
                int level = 0;
                while (level < line.Length && line[level] == '#') level++;
                if (level <= 6 && level < line.Length && line[level] == ' ')
                    headingLines.Add(i);
            }
        }

        // 현재 뷰포트 상단에 가장 가까운 이전 헤딩 찾기
        int prevHeadingIdx = -1;
        for (int i = headingLines.Count - 1; i >= 0; i--)
        {
            if (headingLines[i] <= topLine)
            {
                prevHeadingIdx = i;
                break;
            }
        }

        if (prevHeadingIdx < 0 || headingLines.Count < 2)
        {
            // 헤딩이 없거나 최소 2개 미만이면 기본 비율 동기화
            var ratio = e.VerticalOffset / (e.ExtentHeight - e.ViewportHeight);
            try
            {
                await Viewer.ExecuteScriptAsync(
                    $"(function(){{var h=document.documentElement.scrollHeight-window.innerHeight;" +
                    $"if(h>0)window.scrollTo(0,h*{ratio.ToString(System.Globalization.CultureInfo.InvariantCulture)});}})()");
            }
            catch { }
            return;
        }

        // 현재 헤딩과 다음 헤딩 사이의 비율 계산
        int curHeadingLine = headingLines[prevHeadingIdx];
        int nextHeadingLine = prevHeadingIdx + 1 < headingLines.Count
            ? headingLines[prevHeadingIdx + 1]
            : lines.Length;
        double interRatio = nextHeadingLine > curHeadingLine
            ? Math.Clamp((double)(topLine - curHeadingLine) / (nextHeadingLine - curHeadingLine), 0, 1)
            : 0;

        try
        {
            var inv = System.Globalization.CultureInfo.InvariantCulture;
            await Viewer.ExecuteScriptAsync($@"
(function(){{
    var headings = document.querySelectorAll('h1,h2,h3,h4,h5,h6');
    var idx = {prevHeadingIdx};
    if (idx >= headings.length) {{
        var h = document.documentElement.scrollHeight - window.innerHeight;
        if (h > 0) window.scrollTo(0, h * {(e.VerticalOffset / (e.ExtentHeight - e.ViewportHeight)).ToString(inv)});
        return;
    }}
    var cur = headings[idx];
    var curTop = cur.getBoundingClientRect().top + window.scrollY;
    var nextTop = idx + 1 < headings.length
        ? headings[idx+1].getBoundingClientRect().top + window.scrollY
        : document.documentElement.scrollHeight;
    var target = curTop + (nextTop - curTop) * {interRatio.ToString(inv)};
    window.scrollTo(0, Math.max(0, target - 20));
}})()");
        }
        catch { }
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
        var interval = Math.Clamp(_settings.AutoSaveIntervalSec, 10, 300);
        _autoSaveTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(interval) };
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
                    ? "unsaved_" + doc.Id.ToString("N")[..8]
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
        tab.MouseMove += Tab_MouseMove;
        tab.MouseDown += Tab_MouseDown;
        tab.MouseRightButtonUp += Tab_RightClick;
        tab.AllowDrop = true;
        tab.DragOver += Tab_DragOver;
        tab.Drop += Tab_Drop;
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
        {
            if (e.ClickCount == 2)
            {
                e.Handled = true;
                StartTabRename(b);
                return;
            }
            _tabDragStart = e.GetPosition(b);
            SwitchTo((int)b.Tag);
        }
    }

    private void StartTabRename(Border tab)
    {
        if (tab.Child is not StackPanel sp) return;
        var titleBlock = sp.Children.OfType<TextBlock>().FirstOrDefault(t => t.Tag as string == "title");
        if (titleBlock == null) return;

        var idx = (int)tab.Tag;
        var doc = _docs[idx];
        titleBlock.Visibility = Visibility.Collapsed;

        var tb = new TextBox
        {
            Text = doc.CustomTitle ?? doc.FileName,
            FontSize = 12,
            MinWidth = 60,
            MaxWidth = 200,
            Height = 22,
            Padding = new Thickness(3, 1, 3, 1),
            VerticalAlignment = VerticalAlignment.Center,
            BorderThickness = new Thickness(1),
            BorderBrush = (SolidColorBrush)FindResource("AccentBrush"),
            Background = (SolidColorBrush)FindResource("Surface2Brush"),
            Foreground = (SolidColorBrush)FindResource("TextBrush"),
            CaretBrush = (SolidColorBrush)FindResource("AccentBrush"),
        };

        var titleIdx = sp.Children.IndexOf(titleBlock);
        sp.Children.Insert(titleIdx, tb);

        void Confirm()
        {
            if (!sp.Children.Contains(tb)) return;
            var newName = tb.Text.Trim();
            doc.CustomTitle = string.IsNullOrEmpty(newName) ? null : newName;
            sp.Children.Remove(tb);
            titleBlock.Visibility = Visibility.Visible;
            UpdateTabTitle(idx);
            if (idx == _activeIndex)
                Title = $"Mark.View — {doc.TabTitle}";
        }

        void Cancel()
        {
            if (!sp.Children.Contains(tb)) return;
            sp.Children.Remove(tb);
            titleBlock.Visibility = Visibility.Visible;
        }

        tb.KeyDown += (_, ke) =>
        {
            if (ke.Key == Key.Enter) { Confirm(); ke.Handled = true; }
            else if (ke.Key == Key.Escape) { Cancel(); ke.Handled = true; }
        };
        tb.LostFocus += (_, _) => Confirm();
        Dispatcher.InvokeAsync(() => { tb.Focus(); tb.SelectAll(); },
            System.Windows.Threading.DispatcherPriority.Input);
    }

    private void Tab_MouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed || sender is not Border b) return;
        var pos = e.GetPosition(b);
        if (Math.Abs(pos.X - _tabDragStart.X) < 10 && Math.Abs(pos.Y - _tabDragStart.Y) < 10) return;
        DragDrop.DoDragDrop(b, new DataObject("TabIndex", (int)b.Tag), DragDropEffects.Move);
    }

    private void Tab_DragOver(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent("TabIndex"))
            e.Effects = DragDropEffects.Move;
        else
            e.Effects = DragDropEffects.None;
        e.Handled = true;
    }

    private void Tab_Drop(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent("TabIndex") || sender is not Border target) return;
        var fromIdx = (int)e.Data.GetData("TabIndex");
        var toIdx = (int)target.Tag;
        if (fromIdx == toIdx) return;
        MoveTab(fromIdx, toIdx);
    }

    private void MoveTab(int from, int to)
    {
        var doc = _docs[from];
        var tab = _tabs[from];
        _docs.RemoveAt(from);
        _tabs.RemoveAt(from);
        TabBar.Children.RemoveAt(from);
        _docs.Insert(to, doc);
        _tabs.Insert(to, tab);
        TabBar.Children.Insert(to, tab);
        // 인덱스 재매핑
        for (int i = 0; i < _tabs.Count; i++)
        {
            _tabs[i].Tag = i;
            if (_tabs[i].Child is StackPanel sp)
                foreach (var c in sp.Children.OfType<TextBlock>())
                    if (c.Tag is int) c.Tag = i;
        }
        _activeIndex = to;
        // 탭 스타일 갱신
        for (int i = 0; i < _tabs.Count; i++)
        {
            _tabs[i].BorderBrush = i == _activeIndex
                ? (SolidColorBrush)FindResource("AccentBrush") : Brushes.Transparent;
            _tabs[i].Background = Brushes.Transparent;
            UpdateTabTitle(i);
        }
    }

    private void Tab_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Middle && sender is Border b)
        {
            e.Handled = true;
            CloseTab((int)b.Tag);
        }
    }

    private void Tab_RightClick(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
        if (sender is not Border b) return;
        var tabIdx = (int)b.Tag;
        SwitchTo(tabIdx);
        var doc = _docs[tabIdx];

        var menu = new ContextMenu { IsOpen = false };

        var copyPathItem = new MenuItem { Header = "경로 복사" };
        copyPathItem.IsEnabled = !doc.IsNew;
        copyPathItem.Click += (_, _) => { try { Clipboard.SetText(doc.FilePath); } catch { } };
        menu.Items.Add(copyPathItem);

        var openExplorerItem = new MenuItem { Header = "탐색기에서 열기" };
        openExplorerItem.IsEnabled = !doc.IsNew && File.Exists(doc.FilePath);
        openExplorerItem.Click += (_, _) =>
        {
            try { System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{doc.FilePath}\""); }
            catch { }
        };
        menu.Items.Add(openExplorerItem);

        menu.Items.Add(new Separator());

        var duplicateItem = new MenuItem { Header = "탭 복제" };
        duplicateItem.Click += (_, _) =>
        {
            var clone = new MarkDocument { Content = doc.Content };
            if (!doc.IsNew) clone.FilePath = doc.FilePath;
            OpenDocument(clone);
        };
        menu.Items.Add(duplicateItem);

        var closeRightItem = new MenuItem { Header = "오른쪽 탭 모두 닫기" };
        closeRightItem.IsEnabled = tabIdx < _docs.Count - 1;
        closeRightItem.Click += (_, _) =>
        {
            for (int i = _docs.Count - 1; i > _activeIndex; i--)
                CloseTab(i);
        };
        menu.Items.Add(closeRightItem);

        var closeOthersItem = new MenuItem { Header = "다른 탭 모두 닫기" };
        closeOthersItem.IsEnabled = _docs.Count > 1;
        closeOthersItem.Click += (_, _) =>
        {
            for (int i = _docs.Count - 1; i >= 0; i--)
                if (i != _activeIndex) CloseTab(i);
        };
        menu.Items.Add(closeOthersItem);

        menu.PlacementTarget = b;
        menu.Placement = System.Windows.Controls.Primitives.PlacementMode.MousePoint;
        menu.IsOpen = true;
    }

    private void Editor_ContextMenuOpening(object sender, ContextMenuEventArgs e)
    {
        e.Handled = true; // 기본 WPF 컨텍스트 메뉴 억제
        var hasSel = Editor.SelectionLength > 0;
        var menu = new ContextMenu();

        var cutItem = new MenuItem { Header = "잘라내기\tCtrl+X", IsEnabled = hasSel };
        cutItem.Click += (_, _) => Editor.Cut();
        menu.Items.Add(cutItem);

        var copyItem = new MenuItem { Header = "복사\tCtrl+C", IsEnabled = hasSel };
        copyItem.Click += (_, _) => Editor.Copy();
        menu.Items.Add(copyItem);

        var pasteItem = new MenuItem { Header = "붙여넣기\tCtrl+V" };
        pasteItem.Click += (_, _) => Editor.Paste();
        menu.Items.Add(pasteItem);

        menu.Items.Add(new Separator());

        var selectAllItem = new MenuItem { Header = "모두 선택\tCtrl+A" };
        selectAllItem.Click += (_, _) => Editor.SelectAll();
        menu.Items.Add(selectAllItem);

        if (_isEditMode)
        {
            menu.Items.Add(new Separator());

            var boldItem = new MenuItem { Header = "굵게\tCtrl+B", IsEnabled = hasSel };
            boldItem.Click += (_, _) => WrapSelection("**", "**");
            menu.Items.Add(boldItem);

            var italicItem = new MenuItem { Header = "기울임\tCtrl+I", IsEnabled = hasSel };
            italicItem.Click += (_, _) => WrapSelection("*", "*");
            menu.Items.Add(italicItem);

            var codeItem = new MenuItem { Header = "인라인 코드", IsEnabled = hasSel };
            codeItem.Click += (_, _) => WrapSelection("`", "`");
            menu.Items.Add(codeItem);

            var linkItem = new MenuItem { Header = "링크 삽입\tCtrl+K" };
            linkItem.Click += (_, _) => WrapAsLink();
            menu.Items.Add(linkItem);
        }

        menu.PlacementTarget = Editor;
        menu.Placement = System.Windows.Controls.Primitives.PlacementMode.MousePoint;
        menu.IsOpen = true;
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

        // 현재 탭 스크롤·커서 저장
        if (_activeIndex >= 0 && _activeIndex < _docs.Count)
        {
            _docs[_activeIndex].ScrollY = await GetScrollYAsync();
            var prevDoc = _docs[_activeIndex];
            if (!prevDoc.IsNew)
            {
                _settings.FileCursorPositions[prevDoc.FilePath] = Editor.CaretIndex;
                // 최대 100개 유지
                if (_settings.FileCursorPositions.Count > 100)
                {
                    var oldest = _settings.FileCursorPositions.Keys
                        .Where(k => !_docs.Any(d => d.FilePath == k))
                        .FirstOrDefault();
                    if (oldest != null) _settings.FileCursorPositions.Remove(oldest);
                }
                // 에디터 스크롤 위치 저장 (첫 번째 표시 줄 번호)
                try
                {
                    var text = Editor.Text;
                    if (!string.IsNullOrEmpty(text))
                    {
                        int topCharIdx = Editor.GetCharacterIndexFromPoint(
                            new Point(Editor.Padding.Left + 1, 1), snapToText: true);
                        int firstLine = CountNewlinesBefore(text, topCharIdx);
                        _settings.FileScrollPositions[prevDoc.FilePath] = firstLine;
                        if (_settings.FileScrollPositions.Count > 100)
                        {
                            var oldestScroll = _settings.FileScrollPositions.Keys
                                .Where(k => !_docs.Any(d => d.FilePath == k))
                                .FirstOrDefault();
                            if (oldestScroll != null) _settings.FileScrollPositions.Remove(oldestScroll);
                        }
                    }
                }
                catch { }
            }
        }

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
        // 저장된 커서 위치 복원
        if (!doc.IsNew && _settings.FileCursorPositions.TryGetValue(doc.FilePath, out var savedCaret))
        {
            var caret = Math.Clamp(savedCaret, 0, Editor.Text.Length);
            Editor.CaretIndex = caret;
            // 에디터 스크롤 위치 복원 (저장된 첫 번째 줄 기준)
            if (_settings.FileScrollPositions.TryGetValue(doc.FilePath, out var savedScrollLine))
            {
                try { Editor.ScrollToLine((int)Math.Clamp(savedScrollLine, 0, Editor.LineCount - 1)); } catch { }
            }
            else
            {
                try { Editor.ScrollToLine(Editor.GetLineIndexFromCharacterIndex(caret)); } catch { }
            }
        }
        _suppressEditorChange = false;

        if (_isEditMode) UpdateLineNumbers();
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
            if (result == MessageBoxResult.Yes && !SaveDocument(doc)) return;
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
        // 탭 닫기 후 세션 즉시 저장 (강제종료 대비)
        SaveSession();
        _settings.Save();
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
        if (_settings.IsFocusMode) SetFocusMode(true);
        if (!_settings.IsWordWrap) SetWordWrap(false);
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
            FormatBar.Visibility = Visibility.Visible;
            LineNumColumn.Width = new GridLength(52);
            UpdateLineNumbers();
        }
        else
        {
            EditorColumn.Width = new GridLength(0);
            SplitterColumn.Width = new GridLength(0);
            TxtEditIcon.Foreground = (SolidColorBrush)FindResource("TextDimBrush");
            StatusMode.Text = "뷰";
            StatusCursor.Visibility = Visibility.Collapsed;
            StatusUndo.Visibility = Visibility.Collapsed;
            FormatBar.Visibility = Visibility.Collapsed;
            LineNumColumn.Width = new GridLength(0);
            EditorLineHighlight.Visibility = Visibility.Collapsed;
        }
        _settings.IsEditMode = editMode;
        _settings.Save();
    }

    private void FmtBtn_Click(object sender, RoutedEventArgs e)
    {
        if (_activeIndex < 0 || !_isEditMode) return;
        if (sender is not Button btn || btn.Tag is not string tag) return;
        Editor.Focus();
        switch (tag)
        {
            case "h1":       InsertLinePrefix("# "); break;
            case "h2":       InsertLinePrefix("## "); break;
            case "h3":       InsertLinePrefix("### "); break;
            case "bold":     WrapSelection("**", "**"); break;
            case "italic":   WrapSelection("*", "*"); break;
            case "code":     WrapSelection("`", "`"); break;
            case "codeblock":WrapSelection("\n```\n", "\n```\n"); break;
            case "link":     WrapAsLink(); break;
            case "quote":    InsertLinePrefix("> "); break;
            case "ul":       InsertLinePrefix("- "); break;
            case "ol":       InsertLinePrefix("1. "); break;
            case "hr":       InsertAtNewLine("---"); break;
            case "table":    InsertAtNewLine("| 제목1 | 제목2 | 제목3 |\n|-------|-------|-------|\n|       |       |       |"); break;
            case "image":    InsertImage(); break;
        }
    }

    private void InsertLinePrefix(string prefix)
    {
        var caret = Editor.CaretIndex;
        var text = Editor.Text;
        var lineStart = caret > 0 ? text.LastIndexOf('\n', caret - 1) + 1 : 0;
        Editor.Select(lineStart, 0);
        Editor.SelectedText = prefix;
        Editor.CaretIndex = lineStart + prefix.Length + (caret - lineStart);
    }

    private void InsertAtNewLine(string content)
    {
        var caret = Editor.CaretIndex;
        var text = Editor.Text;
        var needNewlineBefore = caret > 0 && text[caret - 1] != '\n';
        var insert = (needNewlineBefore ? "\n" : "") + content + "\n";
        Editor.Select(caret, 0);
        Editor.SelectedText = insert;
        Editor.CaretIndex = caret + insert.Length;
    }

    private void InsertImage()
    {
        var dlg = new OpenFileDialog
        {
            Filter = "이미지 파일 (*.png;*.jpg;*.jpeg;*.gif;*.webp;*.svg;*.bmp)|*.png;*.jpg;*.jpeg;*.gif;*.webp;*.svg;*.bmp|모든 파일 (*.*)|*.*",
            Title = "이미지 선택",
        };
        if (_activeIndex >= 0 && !_docs[_activeIndex].IsNew)
            dlg.InitialDirectory = _docs[_activeIndex].Directory;
        if (dlg.ShowDialog() != true) return;

        var doc = _docs[_activeIndex];
        var docDir = doc.IsNew ? null : Path.GetDirectoryName(doc.FilePath);
        var imgPath = docDir != null
            ? Path.GetRelativePath(docDir, dlg.FileName).Replace('\\', '/')
            : dlg.FileName.Replace('\\', '/');
        var altText = Path.GetFileNameWithoutExtension(dlg.FileName);
        InsertAtNewLine($"![{altText}]({imgPath})");
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
            TocColumn.MinWidth = 0;
            TocColumn.Width = new GridLength(0);
        }
        else
        {
            var tocWidth = Math.Clamp(_settings.TocWidth, 140, 500);
            TocColumn.Width = new GridLength(tocWidth);
            TocColumn.MinWidth = 140;
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
        _settings.IsFocusMode = focus;
        _settings.Save();
    }

    // ── Word Wrap ────────────────────────────────────────────────────────

    private bool _isWordWrap = true;

    private void SetWordWrap(bool wrap)
    {
        _isWordWrap = wrap;
        Editor.TextWrapping = wrap ? TextWrapping.Wrap : TextWrapping.NoWrap;
        Editor.HorizontalScrollBarVisibility = wrap ? ScrollBarVisibility.Disabled : ScrollBarVisibility.Auto;
        TxtWordWrapIcon.Foreground = wrap
            ? (SolidColorBrush)FindResource("TextDimBrush")
            : (SolidColorBrush)FindResource("AccentBrush");
        _settings.IsWordWrap = wrap;
        _settings.Save();
    }

    // ── 파일 작업 ────────────────────────────────────────────────────────

    private bool SaveDocument(MarkDocument doc)
    {
        if (doc.IsNew) return SaveDocumentAs(doc);
        try
        {
            // 자체 저장임을 FileSystemWatcher에 알림 (1초 유예)
            _recentlySavedPaths.Add(doc.FilePath);
            var savedPath = doc.FilePath;
            var t = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            t.Tick += (_, _) => { t.Stop(); _recentlySavedPaths.Remove(savedPath); };
            t.Start();

            File.WriteAllText(doc.FilePath, doc.Content, new UTF8Encoding(true));
            doc.IsModified = false;
            int i = _docs.IndexOf(doc);
            UpdateTabTitle(i);
            if (i == _activeIndex)
            {
                Title = $"Mark.View — {doc.TabTitle}";
                UpdateStatusBar(doc);
                ShowStatusHint($"저장 완료 — {doc.FileName}", 2);
            }
            // 대응하는 autosave 파일 삭제
            DeleteAutosaveFile(doc);
            return true;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"저장 실패:\n{ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            return false;
        }
    }

    private static void DeleteAutosaveFile(MarkDocument doc)
    {
        var autoSaveDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "MarkView", "autosave");
        var baseName = doc.IsNew
            ? "unsaved_" + doc.Id.ToString("N")[..8]
            : Path.GetFileNameWithoutExtension(doc.FilePath);
        var path = Path.Combine(autoSaveDir, baseName + ".autosave.md");
        if (File.Exists(path)) try { File.Delete(path); } catch { }
    }

    private static void CleanupAutosaveDir()
    {
        var autoSaveDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "MarkView", "autosave");
        if (!Directory.Exists(autoSaveDir)) return;
        try { foreach (var f in Directory.GetFiles(autoSaveDir, "*.autosave.md")) File.Delete(f); }
        catch { }
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
        // 내보내기 경로 힌트 모드 — Tag에 파일 경로 저장됨
        if (StatusPath.Tag is string exportPath && File.Exists(exportPath))
        {
            try { System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{exportPath}\""); }
            catch { }
            return;
        }
        if (_activeIndex < 0 || _activeIndex >= _docs.Count) return;
        var doc = _docs[_activeIndex];
        if (doc.IsNew || !File.Exists(doc.FilePath)) return;
        try
        {
            System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{doc.FilePath}\"");
        }
        catch { }
    }

    private void StatusMode_RightClick(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
        var menu = new ContextMenu();
        foreach (var (label, sec) in new[] { ("30초", 30), ("1분", 60), ("2분", 120), ("5분", 300) })
        {
            var s = sec;
            var item = new MenuItem { Header = $"자동 저장: {label}" };
            if (_settings.AutoSaveIntervalSec == s)
                item.Header = $"✓ 자동 저장: {label}";
            item.Click += (_, _) =>
            {
                _settings.AutoSaveIntervalSec = s;
                _settings.Save();
                if (_autoSaveTimer != null)
                {
                    _autoSaveTimer.Stop();
                    _autoSaveTimer.Interval = TimeSpan.FromSeconds(s);
                    _autoSaveTimer.Start();
                }
                ShowStatusHint($"자동 저장 주기: {label}", 2);
            };
            menu.Items.Add(item);
        }
        menu.PlacementTarget = StatusMode;
        menu.Placement = System.Windows.Controls.Primitives.PlacementMode.Top;
        menu.IsOpen = true;
    }

    private static readonly Regex _headingRegex = new(@"^#{1,6}\s", RegexOptions.Compiled | RegexOptions.Multiline);
    private static readonly Regex _imageRegex   = new(@"!\[.*?\]\(.*?\)", RegexOptions.Compiled);
    private static readonly Regex _linkRegex    = new(@"(?<!!)\[.*?\]\(.*?\)", RegexOptions.Compiled);

    private void UpdateStatusBar(MarkDocument doc)
    {
        StatusPath.Text = doc.IsNew ? "새 문서 (저장되지 않음)" : doc.FilePath;
        // 내용이 바뀐 경우에만 통계 재계산 (대용량 문서 성능)
        if (!ReferenceEquals(doc.Content, doc.CachedStatsContent))
        {
            doc.CachedStatsContent = doc.Content;
            if (string.IsNullOrWhiteSpace(doc.Content))
            {
                doc.CachedWordCount = 0; doc.CachedLineCount = 1;
                doc.CachedHeadingCount = 0; doc.CachedImageCount = 0; doc.CachedLinkCount = 0;
            }
            else
            {
                doc.CachedWordCount    = _wordRegex.Matches(doc.Content).Count;
                doc.CachedLineCount    = doc.Content.Split('\n').Length;
                doc.CachedHeadingCount = _headingRegex.Matches(doc.Content).Count;
                doc.CachedImageCount   = _imageRegex.Matches(doc.Content).Count;
                doc.CachedLinkCount    = _linkRegex.Matches(doc.Content).Count;
            }
        }
        StatusLines.Text = $"{doc.CachedLineCount}줄";
        var readMin = Math.Max(1, (int)Math.Ceiling(doc.CachedWordCount / 200.0));
        StatusWords.Text = doc.CachedWordCount > 0 ? $"{doc.CachedWordCount}단어 · 약 {readMin}분" : "0단어";
        StatusWords.ToolTip = doc.CachedWordCount > 0
            ? $"헤딩: {doc.CachedHeadingCount}개  이미지: {doc.CachedImageCount}개  링크: {doc.CachedLinkCount}개"
            : null;
        StatusMode.Text = _isEditMode ? "편집" : "뷰";
        StatusUndo.Visibility = _isEditMode && Editor.CanUndo ? Visibility.Visible : Visibility.Collapsed;
    }

    // 상태바에 임시 메시지 표시 후 복원
    private DispatcherTimer? _statusHintTimer;
    private void ShowStatusHint(string message, int seconds = 3, string? clickPath = null)
    {
        var prev = _activeIndex >= 0 && _activeIndex < _docs.Count
            ? (_docs[_activeIndex].IsNew ? "새 문서 (저장되지 않음)" : _docs[_activeIndex].FilePath)
            : "파일을 열어주세요";
        StatusPath.Text = message;

        // 내보낸 파일 경로 클릭으로 탐색기 열기
        if (clickPath != null)
        {
            StatusPath.Tag = clickPath;
            StatusPath.ToolTip = $"클릭하여 폴더 열기: {Path.GetDirectoryName(clickPath)}";
            StatusPath.Cursor = Cursors.Hand;
        }

        _statusHintTimer?.Stop();
        _statusHintTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(seconds) };
        _statusHintTimer.Tick += (_, _) =>
        {
            _statusHintTimer?.Stop();
            StatusPath.Text = prev;
            if (clickPath != null)
            {
                StatusPath.Tag = null;
                StatusPath.ToolTip = "클릭하여 파일 위치 열기";
                StatusPath.Cursor = Cursors.Arrow;
            }
        };
        _statusHintTimer.Start();
    }

    // ── 찾기 ────────────────────────────────────────────────────────────

    private void FindInPreview(string keyword)
    {
        if (!_webViewReady) return;
        if (string.IsNullOrEmpty(keyword))
        {
            _findCurrentIndex = 0;
            _findMatchCount = 0;
            StatusFind.Visibility = Visibility.Collapsed;
            _ = Viewer.ExecuteScriptAsync("window.getSelection()?.removeAllRanges()");
            _ = ApplySearchHighlightAsync("");
            return;
        }
        _findCurrentIndex = 0; // 검색어 변경 시 초기화
        _ = ApplySearchHighlightAsync(keyword);
        if (_isEditMode) FindInEditor(keyword);
    }

    private void FindInEditor(string keyword)
    {
        if (string.IsNullOrEmpty(keyword) || _activeIndex < 0) return;
        var text = Editor.Text;
        var cmp = _caseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
        // 현재 선택 위치 이후부터 검색 → 없으면 wrap-around (처음부터 재검색)
        var searchStart = Editor.SelectionStart + Editor.SelectionLength;
        var idx = searchStart < text.Length ? text.IndexOf(keyword, searchStart, cmp) : -1;
        if (idx < 0) idx = text.IndexOf(keyword, cmp); // wrap-around
        if (idx < 0) return;
        Editor.Select(idx, keyword.Length);
        Editor.ScrollToLine(Editor.GetLineIndexFromCharacterIndex(idx));
    }

    private async Task FindInPreviewAsync(string keyword, bool reverse)
    {
        if (!_webViewReady || string.IsNullOrEmpty(keyword)) return;
        try
        {
            if (_findMatchCount <= 0) return;
            if (reverse)
                _findCurrentIndex = _findCurrentIndex <= 1 ? _findMatchCount : _findCurrentIndex - 1;
            else
                _findCurrentIndex = _findCurrentIndex >= _findMatchCount ? 1 : _findCurrentIndex + 1;
            StatusFind.Text = $"{_findCurrentIndex} / {_findMatchCount}";
            StatusFind.Visibility = Visibility.Visible;
            await Viewer.ExecuteScriptAsync($"window.__searchFocus && window.__searchFocus({_findCurrentIndex - 1})");
        }
        catch { }
    }

    // ── Quick Open (Ctrl+P) ──────────────────────────────────────────────

    private record QuickOpenItem(string Label, string SubLabel, string? FilePath, int? TabIndex);

    private void OpenQuickOpen()
    {
        TxtQuickOpen.Text = "";
        RefreshQuickOpenList("");
        QuickOpenPopup.IsOpen = true;
        Dispatcher.InvokeAsync(() => TxtQuickOpen.Focus(), System.Windows.Threading.DispatcherPriority.Input);
    }

    private void RefreshQuickOpenList(string keyword)
    {
        QuickOpenList.Items.Clear();
        var items = new List<QuickOpenItem>();

        // 열린 탭
        for (int i = 0; i < _docs.Count; i++)
        {
            var d = _docs[i];
            var label = d.TabTitle.TrimEnd('•').Trim();
            var sub = d.IsNew ? "(새 문서)" : d.FilePath;
            items.Add(new QuickOpenItem(label, sub, d.IsNew ? null : d.FilePath, i));
        }

        // 최근 파일 (탭에 이미 열린 것 제외)
        var openPaths = new HashSet<string>(_docs.Where(d => !d.IsNew).Select(d => d.FilePath), StringComparer.OrdinalIgnoreCase);
        foreach (var path in _settings.RecentFiles.Where(File.Exists).Where(p => !openPaths.Contains(p)))
            items.Add(new QuickOpenItem(Path.GetFileName(path), path, path, null));

        // 검색어 필터
        var filtered = string.IsNullOrEmpty(keyword)
            ? items
            : items.Where(it => it.Label.Contains(keyword, StringComparison.OrdinalIgnoreCase)
                              || it.SubLabel.Contains(keyword, StringComparison.OrdinalIgnoreCase)).ToList();

        foreach (var item in filtered)
        {
            var border = new Border
            {
                Padding = new Thickness(12, 6, 12, 6),
                Cursor = Cursors.Hand,
                Tag = item,
            };
            var stack = new StackPanel();
            var labelBlock = new TextBlock
            {
                Text = (item.TabIndex.HasValue ? "⬤ " : "📄 ") + item.Label,
                FontSize = 12,
                Foreground = (SolidColorBrush)FindResource("TextBrush"),
            };
            var subBlock = new TextBlock
            {
                Text = "    " + item.SubLabel,
                FontSize = 10,
                Foreground = (SolidColorBrush)FindResource("TextDimBrush"),
                TextTrimming = TextTrimming.CharacterEllipsis,
            };
            stack.Children.Add(labelBlock);
            stack.Children.Add(subBlock);
            border.Child = stack;
            border.MouseEnter += (s, _) => ((Border)s).Background = (SolidColorBrush)FindResource("HoverBrush");
            border.MouseLeave += (s, _) => ((Border)s).Background = Brushes.Transparent;
            border.MouseLeftButtonDown += (_, _) => { ExecuteQuickOpenItem(item); };
            QuickOpenList.Items.Add(new ListBoxItem { Content = border, Tag = item, Padding = new Thickness(0) });
        }

        if (QuickOpenList.Items.Count > 0)
            QuickOpenList.SelectedIndex = 0;
    }

    private void ExecuteQuickOpenItem(QuickOpenItem item)
    {
        QuickOpenPopup.IsOpen = false;
        if (item.TabIndex.HasValue)
            SwitchTo(item.TabIndex.Value);
        else if (item.FilePath != null)
            OpenFile(item.FilePath);
    }

    private void TxtQuickOpen_TextChanged(object sender, TextChangedEventArgs e)
    {
        RefreshQuickOpenList(TxtQuickOpen.Text);
    }

    private void TxtQuickOpen_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape) { QuickOpenPopup.IsOpen = false; e.Handled = true; }
        else if (e.Key == Key.Enter)
        {
            if (QuickOpenList.SelectedItem is ListBoxItem li && li.Tag is QuickOpenItem item)
                ExecuteQuickOpenItem(item);
            e.Handled = true;
        }
        else if (e.Key == Key.Down)
        {
            if (QuickOpenList.Items.Count > 0)
            {
                QuickOpenList.SelectedIndex = Math.Min(QuickOpenList.SelectedIndex + 1, QuickOpenList.Items.Count - 1);
                QuickOpenList.ScrollIntoView(QuickOpenList.SelectedItem);
            }
            e.Handled = true;
        }
        else if (e.Key == Key.Up)
        {
            if (QuickOpenList.Items.Count > 0)
            {
                QuickOpenList.SelectedIndex = Math.Max(QuickOpenList.SelectedIndex - 1, 0);
                QuickOpenList.ScrollIntoView(QuickOpenList.SelectedItem);
            }
            e.Handled = true;
        }
    }

    private void QuickOpenList_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && QuickOpenList.SelectedItem is ListBoxItem li && li.Tag is QuickOpenItem item)
        { ExecuteQuickOpenItem(item); e.Handled = true; }
        else if (e.Key == Key.Escape)
        { QuickOpenPopup.IsOpen = false; e.Handled = true; }
    }

    private void QuickOpenList_DoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (QuickOpenList.SelectedItem is ListBoxItem li && li.Tag is QuickOpenItem item)
            ExecuteQuickOpenItem(item);
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
        if (_activeIndex < 0) return;
        if (!_isEditMode) { ShowReplaceStatus("편집 모드에서만 가능"); return; }
        var find = TxtFind.Text;
        var replace = TxtReplace.Text;
        if (string.IsNullOrEmpty(find)) return;

        var text = Editor.Text;
        var cmp = _caseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
        // 커서 위치 이후부터 검색, 없으면 처음부터 랩어라운드
        var searchFrom = Math.Min(Editor.CaretIndex, text.Length);
        var idx = text.IndexOf(find, searchFrom, cmp);
        if (idx < 0) idx = text.IndexOf(find, cmp); // 랩어라운드
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
        if (_activeIndex < 0) return;
        if (!_isEditMode) { ShowReplaceStatus("편집 모드에서만 가능"); return; }
        var find = TxtFind.Text;
        var replace = TxtReplace.Text;
        if (string.IsNullOrEmpty(find)) return;

        var text = Editor.Text;
        int count = 0;
        var sb = new StringBuilder();
        int pos = 0;
        var cmpAll = _caseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
        while (pos <= text.Length)
        {
            var idx = text.IndexOf(find, pos, cmpAll);
            if (idx < 0) { sb.Append(text.AsSpan(pos)); break; }
            sb.Append(text.AsSpan(pos, idx - pos));
            sb.Append(replace);
            pos = idx + find.Length;
            count++;
        }
        if (count > 0)
        {
            Editor.Select(0, Editor.Text.Length);
            Editor.SelectedText = sb.ToString();
            Editor.CaretIndex = Editor.Text.Length;
        }
        ShowReplaceStatus(count > 0 ? $"{count}개 변경" : "없음");
    }

    private void ShowReplaceStatus(string msg)
    {
        StatusReplace.Text = msg;
        StatusReplace.Visibility = Visibility.Visible;
        _replaceStatusTimer?.Stop();
        _replaceStatusTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        _replaceStatusTimer.Tick += (_, _) => { _replaceStatusTimer.Stop(); StatusReplace.Visibility = Visibility.Collapsed; };
        _replaceStatusTimer.Start();
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

    private void BtnCase_Click(object sender, RoutedEventArgs e)
    {
        _caseSensitive = !_caseSensitive;
        TxtCaseIcon.Foreground = _caseSensitive
            ? (SolidColorBrush)FindResource("AccentBrush")
            : (SolidColorBrush)FindResource("TextDimBrush");
        BtnCase.ToolTip = _caseSensitive ? "대소문자 구분 켜짐 (Alt+C)" : "대소문자 구분 (Alt+C)";
        if (!string.IsNullOrEmpty(TxtFind.Text))
            FindInPreview(TxtFind.Text);
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

    private void BtnWordWrap_Click(object sender, RoutedEventArgs e)
    {
        SetWordWrap(!_isWordWrap);
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
        var initDir = !string.IsNullOrEmpty(_settings.ExportDir) && Directory.Exists(_settings.ExportDir)
            ? _settings.ExportDir
            : doc.Directory;
        if (initDir != null) dlg.InitialDirectory = initDir;
        if (dlg.ShowDialog() != true) return;
        _settings.ExportDir = System.IO.Path.GetDirectoryName(dlg.FileName) ?? "";
        _settings.Save();

        await ShowLoadingAsync("PDF 내보내기 중...");
        try
        {
            var printSettings = Viewer.CoreWebView2.Environment.CreatePrintSettings();
            switch (_settings.PdfPageSize)
            {
                case "letter":
                    printSettings.PageWidth = 21.59; printSettings.PageHeight = 27.94; break;
                case "legal":
                    printSettings.PageWidth = 21.59; printSettings.PageHeight = 35.56; break;
                default: // A4
                    printSettings.PageWidth = 21.0; printSettings.PageHeight = 29.7; break;
            }
            printSettings.MarginTop = _settings.PdfMarginCm;
            printSettings.MarginBottom = _settings.PdfMarginCm;
            printSettings.MarginLeft = _settings.PdfMarginCm;
            printSettings.MarginRight = _settings.PdfMarginCm;
            printSettings.ShouldPrintBackgrounds = true;
            await Viewer.CoreWebView2.PrintToPdfAsync(dlg.FileName, printSettings);
            HideLoading();
            ShowStatusHint($"PDF 저장 완료 — {Path.GetFileName(dlg.FileName)}", 5, dlg.FileName);
        }
        catch (Exception ex)
        {
            HideLoading();
            MessageBox.Show($"PDF 내보내기 실패:\n{ex.Message}", "오류",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void BtnExportPdf_RightClick(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
        var menu = new ContextMenu();

        var sizes = new[] { ("a4", "A4 (21×29.7cm)"), ("letter", "Letter (21.6×27.9cm)"), ("legal", "Legal (21.6×35.6cm)") };
        foreach (var (tag, label) in sizes)
        {
            var item = new MenuItem { Header = (_settings.PdfPageSize == tag ? "  " : "    ") + label };
            if (_settings.PdfPageSize == tag)
                item.FontWeight = FontWeights.Bold;
            var t = tag;
            item.Click += (_, _) => { _settings.PdfPageSize = t; _settings.Save(); };
            menu.Items.Add(item);
        }

        menu.Items.Add(new Separator());

        var margins = new[] { (0.5, "0.5cm"), (1.0, "1.0cm"), (1.5, "1.5cm"), (2.0, "2.0cm") };
        foreach (var (val, label) in margins)
        {
            var item = new MenuItem { Header = (Math.Abs(_settings.PdfMarginCm - val) < 0.01 ? "  " : "    ") + $"여백: {label}" };
            if (Math.Abs(_settings.PdfMarginCm - val) < 0.01)
                item.FontWeight = FontWeights.Bold;
            var v = val;
            item.Click += (_, _) => { _settings.PdfMarginCm = v; _settings.Save(); };
            menu.Items.Add(item);
        }

        menu.PlacementTarget = (UIElement)sender;
        menu.Placement = PlacementMode.Bottom;
        menu.IsOpen = true;
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
        var initDir = !string.IsNullOrEmpty(_settings.ExportDir) && Directory.Exists(_settings.ExportDir)
            ? _settings.ExportDir
            : doc.Directory;
        if (initDir != null) dlg.InitialDirectory = initDir;
        if (dlg.ShowDialog() != true) return;

        _settings.ExportDir = System.IO.Path.GetDirectoryName(dlg.FileName) ?? "";
        _settings.Save();

        await ShowLoadingAsync("HTML 내보내기 중...");
        var content = doc.Content;
        var filePath = doc.IsNew ? null : doc.FilePath;
        var theme = _currentTheme;
        try
        {
            await Task.Run(() =>
            {
                var html = _renderer.RenderToHtml(content, filePath, theme);
                File.WriteAllText(dlg.FileName, html, new UTF8Encoding(true));
            });
            HideLoading();
            ShowStatusHint($"HTML 저장 완료 — {Path.GetFileName(dlg.FileName)}", 5, dlg.FileName);
        }
        catch (Exception ex)
        {
            HideLoading();
            MessageBox.Show($"HTML 내보내기 실패:\n{ex.Message}", "오류",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void BtnCopyHtml_Click(object sender, RoutedEventArgs e)
    {
        if (_activeIndex < 0) return;
        var doc = _docs[_activeIndex];
        var content = doc.Content;
        var filePath = doc.IsNew ? null : doc.FilePath;
        var theme = _currentTheme;
        await ShowLoadingAsync("HTML 생성 중...");
        try
        {
            var html = await Task.Run(() => _renderer.RenderToHtml(content, filePath, theme));
            Clipboard.SetText(html);
            HideLoading();
            var prev = StatusPath.Text;
            StatusPath.Text = "HTML 클립보드 복사 완료";
            var t = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
            t.Tick += (_, _) => { t.Stop(); StatusPath.Text = prev; };
            t.Start();
        }
        catch (Exception ex)
        {
            HideLoading();
            MessageBox.Show($"클립보드 복사 실패:\n{ex.Message}", "오류",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
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
        if (_isEditMode) UpdateLineNumbers();
        _previewTimer?.Stop();
        _previewTimer?.Start();
        // 자동 저장 타이머 재시작 (30초 비활동 후 저장)
        _autoSaveTimer?.Stop();
        _autoSaveTimer?.Start();
    }

    // 텍스트 변경 시 호출 — 레이아웃 완료 후 줄 번호 동기화 예약
    private void UpdateLineNumbers()
    {
        if (LineNumColumn.Width.Value <= 0) return;
        Dispatcher.InvokeAsync(SyncLineNumberOffset, System.Windows.Threading.DispatcherPriority.Loaded);
    }

    // 각 논리 줄을 GetRectFromCharacterIndex 로 정확한 Y에 개별 TextBlock 배치
    // → 워드랩 여부와 무관하게 줄 번호가 해당 줄 첫 행에 정확히 맞춤
    private static readonly Regex _wordRegex = new(@"\S+", RegexOptions.Compiled);
    private static readonly System.Windows.Media.SolidColorBrush _lineNumFg =
        new(System.Windows.Media.Color.FromRgb(0x5A, 0x7A, 0x9A));
    private static readonly System.Windows.Media.FontFamily _lineNumFont =
        new("Cascadia Code, Consolas, monospace");

    private void SyncLineNumberOffset()
    {
        if (LineNumColumn.Width.Value <= 0 || !IsLoaded) return;
        try
        {
            var text = Editor.Text;
            int totalLines = string.IsNullOrEmpty(text) ? 1 : text.Split('\n').Length;

            // 컬럼 너비 (자릿수 기준)
            var digits = totalLines.ToString().Length;
            var colWidth = Math.Max(52, digits * 9 + 24);
            if (Math.Abs(LineNumColumn.Width.Value - colWidth) > 1)
                LineNumColumn.Width = new GridLength(colWidth);
            double numW = colWidth - 1;

            double leftX = Editor.Padding.Left + 1;

            // 뷰포트 최상단 문자 → 첫 논리 줄
            int topCharIdx = Editor.GetCharacterIndexFromPoint(new Point(leftX, 1), snapToText: true);
            int firstLine  = CountNewlinesBefore(text, topCharIdx) + 1;

            // 표시 가능 줄 수 = Canvas 높이 / 줄 높이, 최소 60 보장 (최대화·4K 대응)
            int lineStartIdx0 = topCharIdx > 0 ? text.LastIndexOf('\n', topCharIdx - 1) + 1 : 0;
            double measuredLH = Editor.GetRectFromCharacterIndex(lineStartIdx0).Height;
            double canvasH    = LineNumCanvas.ActualHeight > 0 ? LineNumCanvas.ActualHeight : 800;
            int    bufLines   = Math.Max(60, (int)Math.Ceiling(canvasH / (measuredLH > 0 ? measuredLH : 18.0)) + 5);

            int lastLine   = Math.Min(totalLines, firstLine + bufLines);
            int needed     = lastLine - firstLine + 1;

            // Canvas.Children 재사용 풀 확장
            while (LineNumCanvas.Children.Count < needed)
            {
                LineNumCanvas.Children.Add(new TextBlock
                {
                    FontFamily    = _lineNumFont,
                    FontSize      = Editor.FontSize,
                    Foreground    = _lineNumFg,
                    TextAlignment = TextAlignment.Right,
                    Padding       = new Thickness(0, 0, 8, 0)
                });
            }
            for (int i = needed; i < LineNumCanvas.Children.Count; i++)
                ((TextBlock)LineNumCanvas.Children[i]).Visibility = Visibility.Collapsed;

            // 각 논리 줄의 시작 문자 인덱스 (위에서 계산한 lineStartIdx0 재사용)
            int charIdx = lineStartIdx0;
            for (int i = 0; i < needed; i++)
            {
                var tb = (TextBlock)LineNumCanvas.Children[i];
                tb.Visibility = Visibility.Visible;
                tb.Text  = (firstLine + i).ToString();
                tb.Width = numW;
                // 해당 줄 첫 글자의 실제 Y → 워드랩에서도 첫 행에 정확히 배치
                Canvas.SetTop(tb,  Editor.GetRectFromCharacterIndex(charIdx).Top);
                Canvas.SetLeft(tb, 0);
                int nl = charIdx < text.Length ? text.IndexOf('\n', charIdx) : -1;
                charIdx = nl >= 0 ? nl + 1 : text.Length;
            }
        }
        catch { }
    }

    // text[0..index) 안의 \n 개수 — MemoryExtensions.Count 활용(O(n) SIMD 최적화)
    private static int CountNewlinesBefore(string text, int index)
    {
        var limit = Math.Min(index, text.Length);
        return System.MemoryExtensions.Count(text.AsSpan(0, limit), '\n');
    }

    // Enter 키 → 자동 목록 계속 (unordered, ordered, blockquote)
    private bool TryAutoListContinue()
    {
        var caret = Editor.CaretIndex;
        var text  = Editor.Text;
        if (caret <= 0) return false;

        var lineStart = caret > 0 ? text.LastIndexOf('\n', caret - 1) + 1 : 0;
        var lineText  = text[lineStart..caret];

        // 순서 있는 목록: 1. 또는 1)
        var om = _orderedListLineRegex.Match(lineText);
        if (om.Success)
        {
            var indent  = om.Groups[1].Value;
            var num     = int.Parse(om.Groups[2].Value);
            var sep     = om.Groups[3].Value;
            var content = lineText[om.Length..];
            if (string.IsNullOrWhiteSpace(content))
            {
                Editor.Select(lineStart, caret - lineStart);
                Editor.SelectedText = "\n";
                Editor.CaretIndex = lineStart + 1;
            }
            else
            {
                var next = $"{indent}{num + 1}{sep} ";
                Editor.Select(caret, 0);
                Editor.SelectedText = $"\n{next}";
                Editor.CaretIndex = caret + 1 + next.Length;
            }
            return true;
        }

        // 순서 없는 목록: - / * / +
        var um = _unorderedListLineRegex.Match(lineText);
        if (um.Success)
        {
            var indent  = um.Groups[1].Value;
            var marker  = um.Groups[2].Value;
            var content = lineText[um.Length..];
            if (string.IsNullOrWhiteSpace(content))
            {
                Editor.Select(lineStart, caret - lineStart);
                Editor.SelectedText = "\n";
                Editor.CaretIndex = lineStart + 1;
            }
            else
            {
                var next = $"{indent}{marker} ";
                Editor.Select(caret, 0);
                Editor.SelectedText = $"\n{next}";
                Editor.CaretIndex = caret + 1 + next.Length;
            }
            return true;
        }

        // 인용문: >
        var bm = _blockquoteLineRegex.Match(lineText);
        if (bm.Success)
        {
            var indent  = bm.Groups[1].Value;
            var content = lineText[bm.Length..];
            if (string.IsNullOrWhiteSpace(content))
            {
                Editor.Select(lineStart, caret - lineStart);
                Editor.SelectedText = "\n";
                Editor.CaretIndex = lineStart + 1;
            }
            else
            {
                var next = $"{indent}> ";
                Editor.Select(caret, 0);
                Editor.SelectedText = $"\n{next}";
                Editor.CaretIndex = caret + 1 + next.Length;
            }
            return true;
        }

        return false;
    }

    private void WrapSelection(string before, string after)
    {
        var start = Editor.SelectionStart;
        var sel = Editor.SelectedText;

        // 인라인 서식 토글: 이미 같은 마커로 감싸진 텍스트 선택 시 제거
        if (!before.Contains('\n') && sel.Length > before.Length + after.Length &&
            sel.StartsWith(before, StringComparison.Ordinal) &&
            sel.EndsWith(after, StringComparison.Ordinal))
        {
            var inner = sel[before.Length..(sel.Length - after.Length)];
            // "**" vs "*" 혼동 방지: inner가 동일 구분자로 시작/끝나는지 확인
            bool isSameChar = inner.Length > 0 && before.Length > 0 &&
                              inner[0] == before[^1] && inner[^1] == after[0];
            if (!isSameChar)
            {
                Editor.SelectedText = inner;
                Editor.Select(start, inner.Length);
                return;
            }
        }

        var wrapped = before + sel + after;
        Editor.SelectedText = wrapped;
        Editor.CaretIndex = sel.Length == 0 ? start + before.Length : start + wrapped.Length;
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

    private static readonly Regex _headingLineRegex = new(@"^(#{1,6})\s+(.+)", RegexOptions.Multiline | RegexOptions.Compiled);
    private static readonly Regex _orderedListLineRegex   = new(@"^(\s*)(\d+)([.)]) ", RegexOptions.Compiled);
    private static readonly Regex _unorderedListLineRegex = new(@"^(\s*)([-*+]) ", RegexOptions.Compiled);
    private static readonly Regex _blockquoteLineRegex    = new(@"^(\s*)(>) ", RegexOptions.Compiled);

    private void Editor_SelectionChanged(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded || _activeIndex < 0 || _activeIndex >= _docs.Count) return;
        var text = Editor.Text;
        var caret = Editor.CaretIndex;
        if (caret < 0 || caret > text.Length) return;
        // O(n) LINQ 대신 Span 기반 순회로 최적화
        var span = text.AsSpan(0, caret);
        int line = 1, lastNl = -1;
        for (int i = 0; i < span.Length; i++)
        {
            if (span[i] == '\n') { line++; lastNl = i; }
        }
        var col = caret - (lastNl + 1) + 1;
        StatusCursor.Text = $"{line}:{col}";
        StatusCursor.Visibility = Visibility.Visible;

        // 현재 줄 하이라이트
        UpdateCurrentLineHighlight(caret);

        // Breadcrumb: 커서 앞 마지막 헤딩 표시
        UpdateBreadcrumb(text, caret);

        // 선택 영역이 있으면 상태바에 선택 통계 표시
        if (Editor.SelectionLength > 0)
        {
            var sel = Editor.SelectedText;
            var selWords = _wordRegex.Matches(sel).Count;
            StatusWords.Text = $"선택: {Editor.SelectionLength}자 · {selWords}단어";
        }
        else if (_activeIndex >= 0 && _activeIndex < _docs.Count)
        {
            // 선택 해제 시 문서 전체 통계로 복원
            var doc = _docs[_activeIndex];
            var docWords = string.IsNullOrWhiteSpace(doc.Content) ? 0 : _wordRegex.Matches(doc.Content).Count;
            var readMin = Math.Max(1, (int)Math.Ceiling(docWords / 200.0));
            StatusWords.Text = docWords > 0 ? $"{docWords}단어 · 약 {readMin}분" : "0단어";
        }
    }

    private void UpdateCurrentLineHighlight(int caret)
    {
        if (!_isEditMode)
        {
            EditorLineHighlight.Visibility = Visibility.Collapsed;
            return;
        }
        try
        {
            var rect = Editor.GetRectFromCharacterIndex(caret);
            if (rect.IsEmpty || double.IsInfinity(rect.Top))
            {
                EditorLineHighlight.Visibility = Visibility.Collapsed;
                return;
            }
            EditorLineHighlight.Height = rect.Height > 0 ? rect.Height : 20;
            EditorLineHighlight.Margin = new Thickness(0, rect.Top, 8, 0);
            EditorLineHighlight.Visibility = Visibility.Visible;
        }
        catch
        {
            EditorLineHighlight.Visibility = Visibility.Collapsed;
        }
    }

    private void UpdateBreadcrumb(string text, int caret)
    {
        // 커서 위치까지의 텍스트에서 마지막 헤딩 탐색
        var before = text.AsSpan(0, Math.Min(caret, text.Length));
        int h1 = -1, h2 = -1, h3 = -1;
        string h1Text = "", h2Text = "", h3Text = "";
        int lineStart = 0;
        for (int i = 0; i <= before.Length; i++)
        {
            if (i == before.Length || before[i] == '\n')
            {
                var lineSpan = before[lineStart..i];
                if (lineSpan.StartsWith("#".AsSpan()))
                {
                    int level = 0;
                    while (level < lineSpan.Length && lineSpan[level] == '#') level++;
                    if (level <= 6 && level < lineSpan.Length && lineSpan[level] == ' ')
                    {
                        var title = lineSpan[(level + 1)..].TrimEnd().ToString();
                        if (level == 1) { h1 = i; h1Text = title; h2 = -1; h2Text = ""; h3 = -1; h3Text = ""; }
                        else if (level == 2) { h2 = i; h2Text = title; h3 = -1; h3Text = ""; }
                        else if (level == 3) { h3 = i; h3Text = title; }
                    }
                }
                lineStart = i + 1;
            }
        }
        // 헤딩이 없으면 툴팁 제거
        if (h1 < 0 && h2 < 0 && h3 < 0) { StatusCursor.ToolTip = null; return; }
        var parts = new System.Text.StringBuilder();
        if (h1 >= 0) parts.Append(h1Text);
        if (h2 >= 0) { if (parts.Length > 0) parts.Append(" › "); parts.Append(h2Text); }
        if (h3 >= 0) { if (parts.Length > 0) parts.Append(" › "); parts.Append(h3Text); }
        StatusCursor.ToolTip = parts.ToString();
    }

    // 자동 닫기 쌍: 입력 키 → 삽입 쌍 (앞 마크, 뒤 마크)
    private static readonly Dictionary<Key, (string Open, string Close)> _autoPairs = new()
    {
        { Key.OemOpenBrackets, ("[", "]") },      // [
        { Key.D9,              ("(", ")") },       // (  (Shift 없이)
        { Key.OemQuotes,       ("\"", "\"") },     // "
    };

    private void Editor_KeyDown(object sender, KeyEventArgs e)
    {
        // Ctrl+Shift+V: 서식 없는 붙여넣기 (탭→공백 4개, 연속공백 정리)
        if (e.Key == Key.V && Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift))
        {
            try
            {
                var text = Clipboard.GetText();
                if (!string.IsNullOrEmpty(text))
                {
                    text = text.Replace("\t", "    ")
                               .Replace("\r\n", "\n").Replace("\r", "\n");
                    var start = Editor.SelectionStart;
                    Editor.SelectedText = text;
                    Editor.CaretIndex = start + text.Length;
                }
            }
            catch { }
            e.Handled = true;
            return;
        }

        // 서식 단축키 — 에디터 포커스 시에만 적용
        if (e.Key == Key.B && Keyboard.Modifiers == ModifierKeys.Control)
        { WrapSelection("**", "**"); e.Handled = true; return; }
        if (e.Key == Key.I && Keyboard.Modifiers == ModifierKeys.Control)
        { WrapSelection("*", "*"); e.Handled = true; return; }
        if (e.Key == Key.K && Keyboard.Modifiers == ModifierKeys.Control)
        { WrapAsLink(); e.Handled = true; return; }

        // 자동 닫기 마크 — 선택 영역이 있을 때만 (감싸기), 없으면 기본 동작
        if (Keyboard.Modifiers == ModifierKeys.None && Editor.SelectionLength > 0
            && _autoPairs.TryGetValue(e.Key, out var pair))
        {
            WrapSelection(pair.Open, pair.Close);
            e.Handled = true;
            return;
        }
        // * 키: 선택 영역 있으면 *로 감싸기 (Shift+8)
        if (e.Key == Key.D8 && Keyboard.Modifiers == ModifierKeys.Shift && Editor.SelectionLength > 0)
        {
            WrapSelection("*", "*");
            e.Handled = true;
            return;
        }
        // ` 키: 선택 영역 있으면 `로 감싸기
        if (e.Key == Key.OemTilde && Keyboard.Modifiers == ModifierKeys.None && Editor.SelectionLength > 0)
        {
            WrapSelection("`", "`");
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Return && Keyboard.Modifiers == ModifierKeys.None)
        {
            if (TryAutoListContinue()) { e.Handled = true; return; }
        }

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
        if (TocList.SelectedItem is not ListBoxItem item) return;
        // 프리뷰 스크롤
        if (item.Tag is string id && !string.IsNullOrEmpty(id))
        {
            var jsId2 = System.Text.Json.JsonSerializer.Serialize(id);
            _ = Viewer.ExecuteScriptAsync(
                $"window.__sp&&window.__sp();document.getElementById({jsId2})?.scrollIntoView({{behavior:'smooth'}})");
        }
        // 편집 모드: 에디터도 해당 헤딩 줄로 이동
        if (_isEditMode && item.Content is string headingText && _activeIndex >= 0)
        {
            var text = Editor.Text;
            var lines = text.Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                var trimmed = lines[i].TrimStart('#').TrimStart();
                if (trimmed.Equals(headingText, StringComparison.OrdinalIgnoreCase))
                {
                    try { Editor.ScrollToLine(i); Editor.Focus(); } catch { }
                    break;
                }
            }
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
        else if (e.Key == Key.E && Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift))
        { BtnExportHtml_Click(this, new RoutedEventArgs()); e.Handled = true; }
        else if (e.Key == Key.T && Keyboard.Modifiers == ModifierKeys.Control)
        { SetTocVisible(!_isTocVisible); e.Handled = true; }
        else if (e.Key == Key.Tab && Keyboard.Modifiers == ModifierKeys.Control && _docs.Count > 1)
        { SwitchTo((_activeIndex + 1) % _docs.Count); e.Handled = true; }
        else if (e.Key == Key.Tab && Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift) && _docs.Count > 1)
        { SwitchTo((_activeIndex - 1 + _docs.Count) % _docs.Count); e.Handled = true; }
        else if (e.Key == Key.P && Keyboard.Modifiers == ModifierKeys.Control)
        { OpenQuickOpen(); e.Handled = true; }
        else if (e.Key == Key.P && Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift))
        { BtnExportPdf_Click(this, new RoutedEventArgs()); e.Handled = true; }
        else if (e.Key == Key.OemPlus && Keyboard.Modifiers == ModifierKeys.Control)
        { AdjustEditorFontSize(1); e.Handled = true; }
        else if (e.Key == Key.OemMinus && Keyboard.Modifiers == ModifierKeys.Control)
        { AdjustEditorFontSize(-1); e.Handled = true; }
        else if (e.Key == Key.D0 && Keyboard.Modifiers == ModifierKeys.Control)
        { ResetFontSize(); e.Handled = true; }
        else if (e.Key == Key.F11)
        { SetFocusMode(!_isFocusMode); e.Handled = true; }
        else if (e.Key == Key.F1)
        { ShowHelp(); e.Handled = true; }
        else if (e.Key == Key.H && Keyboard.Modifiers == ModifierKeys.Control)
        { ToggleReplaceBar(); e.Handled = true; }
        else if (e.Key == Key.C && Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift))
        { BtnCopyHtml_Click(this, new RoutedEventArgs()); e.Handled = true; }
        else if (e.Key == Key.V && Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift))
        { PasteClipboardAsNewTab(); e.Handled = true; }
        else if (e.Key == Key.M && Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift))
        { CopyMarkdownToClipboard(); e.Handled = true; }
        else if (e.Key == Key.Z && Keyboard.Modifiers == ModifierKeys.Alt)
        { SetWordWrap(!_isWordWrap); e.Handled = true; }
        else if (e.Key == Key.C && Keyboard.Modifiers == ModifierKeys.Alt)
        { BtnCase_Click(this, new RoutedEventArgs()); e.Handled = true; }
        else if (e.Key == Key.Enter && Keyboard.Modifiers == ModifierKeys.Control && _isEditMode)
        { _previewTimer?.Stop(); RenderPreview(saveScroll: true); e.Handled = true; }
        else if (e.Key == Key.G && Keyboard.Modifiers == ModifierKeys.Control)
        { ShowGotoLinePopup(); e.Handled = true; }
        else if (e.Key == Key.Escape && !string.IsNullOrEmpty(TxtFind.Text))
        { TxtFind.Text = ""; e.Handled = true; }
        else if (e.Key == Key.Escape && GotoLinePopup.IsOpen)
        { GotoLinePopup.IsOpen = false; e.Handled = true; }
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

    private void CopyMarkdownToClipboard()
    {
        if (_activeIndex < 0 || _activeIndex >= _docs.Count) return;
        var text = _docs[_activeIndex].Content;
        if (string.IsNullOrEmpty(text)) return;
        try
        {
            Clipboard.SetText(text);
            ShowStatusHint("Markdown 원문 복사 완료", 2);
        }
        catch { }
    }

    private void PasteClipboardAsNewTab()
    {
        var text = Clipboard.GetText();
        if (string.IsNullOrEmpty(text)) return;
        var doc = new MarkDocument { Content = text };
        OpenDocument(doc);
        if (!_isEditMode) SetEditMode(true);
    }

    // ── 줄 이동 (Ctrl+G) ────────────────────────────────────────────────

    private void ShowGotoLinePopup()
    {
        if (!_isEditMode || _activeIndex < 0) return;
        TxtGotoLine.Text = "";
        TxtGotoLineHint.Visibility = Visibility.Collapsed;
        GotoLinePopup.PlacementTarget = this;
        GotoLinePopup.Placement = System.Windows.Controls.Primitives.PlacementMode.Center;
        GotoLinePopup.IsOpen = true;
        Dispatcher.InvokeAsync(() => TxtGotoLine.Focus(),
            System.Windows.Threading.DispatcherPriority.Input);
    }

    private void TxtGotoLine_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            if (int.TryParse(TxtGotoLine.Text.Trim(), out int lineNum) && lineNum > 0)
            {
                GoToLine(lineNum);
                GotoLinePopup.IsOpen = false;
            }
            else
            {
                TxtGotoLineHint.Text = "숫자를 입력하세요";
                TxtGotoLineHint.Visibility = Visibility.Visible;
            }
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            GotoLinePopup.IsOpen = false;
            e.Handled = true;
        }
    }

    private void GoToLine(int lineNumber)
    {
        var text = Editor.Text;
        var totalLines = string.IsNullOrEmpty(text) ? 1 : text.Split('\n').Length;
        lineNumber = Math.Clamp(lineNumber, 1, totalLines);
        var idx = Editor.GetCharacterIndexFromLineIndex(lineNumber - 1);
        if (idx < 0) idx = 0;
        Editor.CaretIndex = idx;
        Editor.ScrollToLine(lineNumber - 1);
        Editor.Focus();
        ShowStatusHint($"{lineNumber}줄로 이동", 2);
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

    private static readonly HashSet<string> _imageExtensions =
        new(StringComparer.OrdinalIgnoreCase) { ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".webp", ".svg" };

    private void Window_Drop(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return;
        var files = (string[])e.Data.GetData(DataFormats.FileDrop);
        foreach (var f in files)
        {
            if (!File.Exists(f)) continue;
            var ext = Path.GetExtension(f);
            if (_isEditMode && _activeIndex >= 0 && _imageExtensions.Contains(ext))
            {
                // 이미지 파일 → 에디터에 Markdown 이미지 문법 삽입
                var doc = _docs[_activeIndex];
                var docDir = doc.IsNew ? null : Path.GetDirectoryName(doc.FilePath);
                var imgPath = docDir != null
                    ? Path.GetRelativePath(docDir, f).Replace('\\', '/')
                    : f.Replace('\\', '/');
                var altText = Path.GetFileNameWithoutExtension(f);
                var markdown = $"![{altText}]({imgPath})";
                var caret = Editor.CaretIndex;
                Editor.Select(caret, 0);
                Editor.SelectedText = markdown;
                Editor.CaretIndex = caret + markdown.Length;
                Editor.Focus();
            }
            else
            {
                OpenFile(f);
            }
        }
    }

    private void OpenFile(string path) => _ = OpenFileAsync(path);

    private async Task OpenFileAsync(string path)
    {
        // 대용량 파일 경고 (5만 줄 이상 or 10MB 이상)
        var fileInfo = new FileInfo(path);
        if (fileInfo.Exists && fileInfo.Length > 10 * 1024 * 1024)
        {
            var mb = fileInfo.Length / (1024.0 * 1024.0);
            var result = MessageBox.Show(
                $"'{Path.GetFileName(path)}' 파일이 {mb:F1}MB로 큽니다.\n열기 시 응답이 느려질 수 있습니다. 계속하시겠습니까?",
                "대용량 파일",
                MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (result != MessageBoxResult.Yes) return;
        }

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

    private DispatcherTimer? _replaceStatusTimer;
    private DispatcherTimer? _fileChangedDebounce;

    private void OnWatchedFileChanged(object sender, FileSystemEventArgs e)
    {
        Dispatcher.InvokeAsync(() =>
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
        // 자체 저장 직후 발생한 이벤트는 무시
        if (_recentlySavedPaths.Contains(path)) return;
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
            {
                var errMsg = $"파일 읽기 실패: {ex.Message}";
                StatusPath.Text = errMsg;
                var t = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
                t.Tick += (_, _) =>
                {
                    t.Stop();
                    if (StatusPath.Text == errMsg && _activeIndex >= 0 && _activeIndex < _docs.Count)
                        StatusPath.Text = _docs[_activeIndex].IsNew ? "새 문서 (저장되지 않음)" : _docs[_activeIndex].FilePath;
                };
                t.Start();
            }
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
        var validPinned = _settings.PinnedFiles.Where(File.Exists).ToList();
        bool changed = false;
        if (validRecent.Count != _settings.RecentFiles.Count)
        {
            _settings.RecentFiles = validRecent;
            changed = true;
        }
        if (validPinned.Count != _settings.PinnedFiles.Count)
        {
            _settings.PinnedFiles = validPinned;
            changed = true;
        }
        if (changed) _settings.Save();

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

        _allRecentItems = pinned.Concat(rest).ToList();
        if (_allRecentItems.Count == 0)
        {
            RecentFilesPanel.Visibility = Visibility.Collapsed;
            return;
        }
        FilterRecentList(TxtRecentSearch?.Text ?? "");
        RecentFilesPanel.Visibility = Visibility.Visible;
    }

    private void FilterRecentList(string query)
    {
        if (_allRecentItems.Count == 0) return;
        var filtered = string.IsNullOrWhiteSpace(query)
            ? _allRecentItems
            : _allRecentItems
                .Where(i => i.FileName.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                            i.Directory.Contains(query, StringComparison.OrdinalIgnoreCase))
                .ToList();
        RecentFilesList.ItemsSource = filtered;
    }

    private void TxtRecentSearch_TextChanged(object sender, TextChangedEventArgs e)
    {
        FilterRecentList(TxtRecentSearch.Text);
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
        {
            if (Keyboard.Modifiers == ModifierKeys.Control)
                _ = OpenFileForceNewTabAsync(item.FullPath);
            else
                OpenFile(item.FullPath);
        }
    }

    private async Task OpenFileForceNewTabAsync(string path)
    {
        await ShowLoadingAsync("파일 열기 중...");
        try
        {
            var content = await Task.Run(() => File.ReadAllText(path));
            // 중복 체크 없이 새 탭으로 강제 오픈
            var doc = new MarkDocument { FilePath = path, Content = content };
            _docs.Add(doc);
            var tab = CreateTabItem(doc, _docs.Count - 1);
            _tabs.Add(tab);
            TabBar.Children.Add(tab);
            SwitchTo(_docs.Count - 1);
            UpdateEmptyState();
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

    private void RemoveRecentFile_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string path)
        {
            _settings.RecentFiles.Remove(path);
            _settings.PinnedFiles.RemoveAll(p => p.Equals(path, StringComparison.OrdinalIgnoreCase));
            _settings.Save();
            RefreshRecentList();
        }
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
            CleanupAutosaveDir();
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
        CleanupAutosaveDir();
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
        // 현재 활성 탭의 커서·스크롤 위치 저장
        if (_activeIndex >= 0 && _activeIndex < _docs.Count)
        {
            var doc = _docs[_activeIndex];
            if (!doc.IsNew)
            {
                _settings.FileCursorPositions[doc.FilePath] = Editor.CaretIndex;
                try
                {
                    var text = Editor.Text;
                    if (!string.IsNullOrEmpty(text))
                    {
                        int topCharIdx = Editor.GetCharacterIndexFromPoint(
                            new Point(Editor.Padding.Left + 1, 1), snapToText: true);
                        _settings.FileScrollPositions[doc.FilePath] = CountNewlinesBefore(text, topCharIdx);
                    }
                }
                catch { }
            }
        }
    }
}
